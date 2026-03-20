using System.Diagnostics;
using System.Text.Json;
using ProjectAuditor.Core.Models;

namespace ProjectAuditor.Core.Services;

public class DotNetCliService
{
    private readonly ISettingsService _settingsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DotNetCliService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
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
    /// </summary>
    public async Task<bool> BuildAsync(string targetPath)
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

        using var process = Process.Start(processStartInfo);
        if (process == null) return false;

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    /// <summary>
    /// Restores the target project or solution. Required for dotnet list package to output valid JSON.
    /// </summary>
    public async Task<bool> RestoreAsync(string targetPath)
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

        using var process = Process.Start(processStartInfo);
        if (process == null) return false;

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}
