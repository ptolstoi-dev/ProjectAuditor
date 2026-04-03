using System.Diagnostics;
using System.Text.Json;
using ProjectAuditor.Core.Models;

namespace ProjectAuditor.Core.Services;

public class DotNetCliService
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DotNetCliService(ISettingsService settingsService, ILocalizationService? localizationService = null)
    {
        _settingsService = settingsService;
        _localizationService = localizationService ?? new LocalizationService();
    }

    /// <summary>
    /// Evaluates the given solution or project for outdated, vulnerable, or deprecated packages.
    /// Uses `dotnet list package --format json`.
    /// </summary>
    /// <param name="targetPath">Path to .sln, .csproj, or directory.</param>
    /// <param name="checkType">"outdated", "vulnerable", or "deprecated".</param>
    public async Task<DotNetListPackageResponse?> ListPackagesAsync(string targetPath, string checkType)
    {
        targetPath = targetPath.TrimEnd('\\', '/');
        var settings = await _settingsService.LoadSettingsAsync();

        // Ensure the project is restored first, otherwise dotnet list outputs invalid JSON "problems" arrays
        await RestoreAsync(targetPath);

        // Valid types are: outdated, vulnerable, deprecated
        var arguments = $"list \"{targetPath}\" package --{checkType} --format json";
        
        // For outdated or vulnerable, apply the selected mode
        if (checkType is "outdated" or "vulnerable")
        {
            if (settings.AuditMode == NuGetAuditMode.All)
            {
                arguments += " --include-transitive";
            }
        }

        // For vulnerable, apply the severity
        if (checkType == "vulnerable")
        {
            // Note: The dotnet CLI doesn't natively support filtering by severity through parameters.
            // We'll fetch all vulnerabilities and filter them post-parsing based on settings.AuditLevel,
            // or see if the user configures NuGet.Config for AuditLevel.
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start dotnet CLI process.");

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
        {
            throw new Exception($"dotnet CLI failed with exit code {process.ExitCode}: {error}");
        }

        try
        {
            // The JSON might be prefixed or suffixed with warnings, so we should look for the JSON object
            var jsonStart = output.IndexOf('{');
            var jsonEnd = output.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var cleanJson = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var response = JsonSerializer.Deserialize<DotNetListPackageResponse>(cleanJson, JsonOptions);
                return FilterVulnerabilitiesByLevel(response, settings.AuditLevel);
            }
            return null;
        }
        catch (JsonException ex)
        {
            throw new Exception($"Failed to parse dotnet CLI JSON output: {ex.Message}\nRaw Output: {output}");
        }
    }

    private DotNetListPackageResponse? FilterVulnerabilitiesByLevel(DotNetListPackageResponse? response, NuGetAuditLevel configLevel)
    {
        if (response?.Projects == null) return response;

        // Map enum to severity priority
        int TargetLevelValue = SeverityToValue(configLevel.ToString());

        foreach (var project in response.Projects)
        {
            if (project.Frameworks == null) continue;
            foreach (var framework in project.Frameworks)
            {
                FilterPackageVulnerabilities(framework.TopLevelPackages, TargetLevelValue);
                FilterPackageVulnerabilities(framework.TransitivePackages, TargetLevelValue);
            }
        }

        return response;
    }

    private void FilterPackageVulnerabilities(List<PackageItem>? packages, int targetLevelValue)
    {
        if (packages == null) return;
        
        foreach (var pkg in packages)
        {
            if (pkg.Vulnerabilities == null) continue;
            
            // Keep only vulnerabilities that meet or exceed the target level
            pkg.Vulnerabilities = pkg.Vulnerabilities
                .Where(v => SeverityToValue(v.Severity) >= targetLevelValue)
                .ToList();
        }
    }

    private int SeverityToValue(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "low" => 1,
            "moderate" => 2,
            "high" => 3,
            "critical" => 4,
            _ => 0
        };
    }

    /// <summary>
    /// Builds the target project or solution to test if changes are valid.
    /// Returns a BuildResult containing success status and detailed error messages.
    /// </summary>
    public virtual async Task<BuildResult> BuildAsync(string targetPath)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{targetPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2 minutes timeout
            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                var errorMsg = _localizationService.GetString("Errors.CliProcessStartFailed", "Failed to start dotnet CLI process.");
                return new BuildResult
                {
                    Success = false,
                    ErrorMessage = errorMsg
                };
            }

            // Lese StandardOutput und StandardError PARALLEL, um Deadlocks zu vermeiden
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cts.Token);

                // Warte auf beide Streams
                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode == 0)
                {
                    return new BuildResult { Success = true };
                }
                else
                {
                    // Kombiniere Output und Error für aussagekräftige Fehlermeldung
                    var fullOutput = $"{output}\n{error}".Trim();

                    // Extrahiere wichtige Fehlerinformationen
                    var errorMessage = ExtractBuildErrorMessage(fullOutput);

                    return new BuildResult
                    {
                        Success = false,
                        ExitCode = process.ExitCode,
                        StdError = error,
                        ErrorMessage = errorMessage
                    };
                }
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
                var timeoutMsg = _localizationService.GetString("Errors.BuildTimeout", "Build operation timed out after 2 minutes.");
                return new BuildResult
                {
                    Success = false,
                    ErrorMessage = timeoutMsg
                };
            }
        }
        catch (Exception ex)
        {
            var errorMsg = _localizationService.GetString("Errors.UnexpectedBuildError", "Unexpected error during build: {error}")
                .Replace("{error}", ex.Message);
            return new BuildResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
    }

    /// <summary>
    /// Extrahiert die wichtigsten Fehlerinformationen aus der Build-Ausgabe.
    /// Sucht nach Fehlerzeilen, die mit "error" beginnen.
    /// </summary>
    private string ExtractBuildErrorMessage(string fullOutput)
    {
        if (string.IsNullOrWhiteSpace(fullOutput))
        {
            return _localizationService.GetString("Errors.BuildFailedUnknown", "Build failed with unknown error.");
        }

        var lines = fullOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var errorLines = new List<string>();

        // Sammle alle Zeilen, die mit "error" beginnen
        foreach (var line in lines)
        {
            if (line.Contains("error ", StringComparison.OrdinalIgnoreCase) || 
                line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                errorLines.Add(line.Trim());
            }
        }

        // Falls spezifische Fehler gefunden wurden, nutze diese
        if (errorLines.Count > 0)
        {
            // Nimm die ersten 3 Fehler
            var significantErrors = string.Join("\n", errorLines.Take(3));
            var prefix = _localizationService.GetString("Errors.BuildFailedWithErrors", "Build failed with errors:");
            return $"{prefix}\n{significantErrors}";
        }

        // Falls keine spezifischen Fehler gefunden, gib die letzten Zeilen zurück
        var lastLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(5);
        if (lastLines.Any())
        {
            var prefix = _localizationService.GetString("Errors.BuildFailed", "Build failed:");
            return $"{prefix}\n{string.Join("\n", lastLines)}";
        }

        return _localizationService.GetString("Errors.BuildFailedUnknown", "Build failed with unknown error.");
    }

    /// <summary>
    /// Restores the target project or solution. Required for dotnet list package to output valid JSON.
    /// </summary>
    public async Task<bool> RestoreAsync(string targetPath)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"restore \"{targetPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2 minutes timeout
            using var process = Process.Start(processStartInfo);
            if (process == null) return false;

            try
            {
                // Lese StandardOutput und StandardError PARALLEL, um Deadlocks zu vermeiden
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cts.Token);

                // Warte auf beide Streams (obwohl wir sie nicht nutzen, verhindert dies Deadlocks)
                _ = await outputTask;
                _ = await errorTask;

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }
}
