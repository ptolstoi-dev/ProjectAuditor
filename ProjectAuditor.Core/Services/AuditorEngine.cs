using ProjectAuditor.Core.Models;
using ProjectAuditor.Core.Parsers;
using Microsoft.Extensions.Logging;

namespace ProjectAuditor.Core.Services;

public class AuditorEngine
{
    private readonly DotNetCliService _cliService;
    private readonly ProjectParser _projectParser;
    private readonly ILogger<AuditorEngine>? _logger;

    public AuditorEngine(DotNetCliService cliService, ProjectParser projectParser, ILogger<AuditorEngine>? logger = null)
    {
        _cliService = cliService;
        _projectParser = projectParser;
        _logger = logger;
    }

    private static readonly HttpClient _httpClient = new HttpClient();

    /// <summary>
    /// Event für Progress-Rückmeldungen bei Scan und Upgrade
    /// </summary>
    public event Action<ProgressUpdate>? OnProgress;

    /// <summary>
    /// Hilfsmethode um Progress zu melden
    /// </summary>
    private void RaiseProgress(ProgressUpdate update)
    {
        OnProgress?.Invoke(update);
        _logger?.LogInformation($"[{update.Stage}] {update.Message}");
    }

    public async Task<List<PackageUpgradeModel>> GetUpgradablePackagesAsync(string path)
    {
        _logger?.LogInformation($"Scanning for upgradable packages in {path}");

        var results = new Dictionary<string, PackageUpgradeModel>();

        try
        {
            // 1. Get Vulnerable packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningVulnerable,
                Message = "Scanning for vulnerable packages...",
                PercentComplete = 10
            });
            var vulnerableResponse = await _cliService.ListPackagesAsync(path, "vulnerable");
            await ProcessResponseAsync(vulnerableResponse, results, "Vulnerable");

            // 2. Get Outdated packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningOutdated,
                Message = "Scanning for outdated packages...",
                PercentComplete = 30
            });
            var outdatedResponse = await _cliService.ListPackagesAsync(path, "outdated");
            await ProcessResponseAsync(outdatedResponse, results, "Outdated");

            // 3. Get Deprecated packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningDeprecated,
                Message = "Scanning for deprecated packages...",
                PercentComplete = 50
            });
            var deprecatedResponse = await _cliService.ListPackagesAsync(path, "deprecated");
            await ProcessResponseAsync(deprecatedResponse, results, "Deprecated");

            // 4. Fetch versions for all packages
            int totalPackages = results.Count;
            int currentPackageIndex = 0;
            foreach (var package in results.Values)
            {
                currentPackageIndex++;
                int percent = 50 + (int)((double)currentPackageIndex / totalPackages * 40);
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.FetchingVersions,
                    CurrentPackage = package.PackageId,
                    Message = $"Fetching versions for {package.PackageId} ({currentPackageIndex}/{totalPackages})...",
                    PercentComplete = percent
                });
                // Versions already fetched in ProcessResponse, but this provides feedback
            }

            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Complete,
                Message = $"Scan completed. Found {totalPackages} packages requiring updates.",
                PercentComplete = 100
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during scan: {ex.Message}");
            throw;
        }

        return results.Values.ToList();
    }

    private async Task ProcessResponseAsync(DotNetListPackageResponse? response, Dictionary<string, PackageUpgradeModel> results, string reason)
    {
        if (response?.Projects == null) return;

        foreach (var project in response.Projects)
        {
            if (project.Frameworks == null) continue;

            foreach (var fx in project.Frameworks)
            {
                var allPackagesToUpdate = new List<PackageItem>();
                if (fx.TopLevelPackages != null) allPackagesToUpdate.AddRange(fx.TopLevelPackages);
                if (fx.TransitivePackages != null) allPackagesToUpdate.AddRange(fx.TransitivePackages);

                foreach (var pkg in allPackagesToUpdate)
                {
                    // Generate a unique key per package AND version
                    var resolvedVer = pkg.ResolvedVersion ?? "Unknown";
                    var key = $"{pkg.Id}_{resolvedVer}";

                    if (!results.TryGetValue(key, out var model))
                    {
                        var allVersions = await GetAllNugetVersionsAsync(pkg.Id);

                        // Filter: only newer versions than current, exclude preview/pre-release
                        var filteredVersions = FilterVersions(allVersions, resolvedVer);

                        model = new PackageUpgradeModel
                        {
                            PackageId = pkg.Id,
                            CurrentVersion = resolvedVer,
                            AvailableVersions = filteredVersions
                        };

                        // Set the default selected version to the latest one found by dotnet list, or the newest in the filtered array
                        if (!string.IsNullOrEmpty(pkg.LatestVersion) && filteredVersions.Contains(pkg.LatestVersion))
                        {
                            model.SelectedVersion = pkg.LatestVersion;
                        }
                        else if (filteredVersions.Any())
                        {
                            model.SelectedVersion = filteredVersions.Last(); // newest stable version
                        }

                        results[key] = model;
                    }

                    // Add reason if not already present
                    if (!model.Reasons.Contains(reason))
                    {
                        model.Reasons.Add(reason);
                    }

                    // Append reason/vulnerability info (legacy field)
                    if (reason == "Vulnerable" && pkg.Vulnerabilities != null)
                    {
                        // Add vulnerabilities to the new Vulnerabilities list
                        foreach (var vuln in pkg.Vulnerabilities)
                        {
                            if (!model.Vulnerabilities.Any(v => v.AdvisoryUrl == vuln.AdvisoryUrl))
                            {
                                model.Vulnerabilities.Add(vuln);
                            }
                        }

                        var vulns = string.Join(", ", pkg.Vulnerabilities.Select(v => v.Severity));
                        if (!model.VulnerabilityInfo.Contains(vulns))
                        {
                            model.VulnerabilityInfo += string.IsNullOrEmpty(model.VulnerabilityInfo) ? $"Vulnerable: {vulns}" : $", {vulns}";
                        }
                    }
                    else if (!string.IsNullOrEmpty(reason) && !model.VulnerabilityInfo.Contains(reason))
                    {
                        model.VulnerabilityInfo += string.IsNullOrEmpty(model.VulnerabilityInfo) ? reason : $", {reason}";
                    }

                    // Handle deprecated packages - extract alternative package if available
                    if (reason == "Deprecated" && pkg.DeprecationReasons != null && pkg.DeprecationReasons.Any())
                    {
                        // Try to extract alternative package from deprecation reasons
                        // Format is usually something like "Legacy", or can include alternative package info
                        var altPackage = pkg.DeprecationReasons.FirstOrDefault(r => r.Contains("alternative", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(altPackage))
                        {
                            model.AlternativePackage = altPackage;
                        }
                    }

                    // Add project usage if not already present
                    if (!model.Projects.Any(p => p.ProjectPath == project.Path))
                    {
                        model.Projects.Add(new ProjectUsageModel { ProjectPath = project.Path });
                    }
                }
            }
        }
    }

    private async Task<List<string>> GetAllNugetVersionsAsync(string packageId)
    {
        try
        {
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("versions", out var versionsArray))
                {
                    return versionsArray.EnumerateArray().Select(v => v.GetString()!).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Could not fetch versions for {packageId}: {ex.Message}");
        }
        return new List<string>();
    }

    private List<string> FilterVersions(List<string> allVersions, string currentVersion)
    {
        if (string.IsNullOrEmpty(currentVersion) || currentVersion == "Unknown")
        {
            // If current version is unknown, return all stable versions
            return allVersions.Where(v => !IsPreviewVersion(v)).ToList();
        }

        // Parse current version
        if (!NuGet.Versioning.NuGetVersion.TryParse(currentVersion, out var currentNuGetVersion))
        {
            // If we can't parse current version, return all stable versions
            return allVersions.Where(v => !IsPreviewVersion(v)).ToList();
        }

        var filteredVersions = new List<string>();

        foreach (var versionString in allVersions)
        {
            // Skip preview/pre-release versions
            if (IsPreviewVersion(versionString))
                continue;

            // Try to parse version
            if (NuGet.Versioning.NuGetVersion.TryParse(versionString, out var nugetVersion))
            {
                // Only include versions newer than current
                if (nugetVersion > currentNuGetVersion)
                {
                    filteredVersions.Add(versionString);
                }
            }
        }

        return filteredVersions;
    }

    private bool IsPreviewVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return false;

        // Check for common preview indicators
        var lowerVersion = version.ToLowerInvariant();
        return lowerVersion.Contains("preview") ||
               lowerVersion.Contains("alpha") ||
               lowerVersion.Contains("beta") ||
               lowerVersion.Contains("rc") ||
               lowerVersion.Contains("pre") ||
               lowerVersion.Contains("-");  // NuGet pre-release versions contain a hyphen
    }

    public async Task ApplyUpgradesAsync(string basePath, List<PackageUpgradeModel> packagesToUpgrade)
    {
        _logger?.LogInformation($"Starting targeted upgrades in {basePath}");

        var cpmPath = Path.Combine(basePath, "Directory.Packages.props");
        var usesCpm = File.Exists(cpmPath);

        // Count total upgrades needed
        int totalUpgrades = 0;
        foreach (var pkg in packagesToUpgrade)
        {
            if (string.IsNullOrEmpty(pkg.SelectedVersion) || pkg.SelectedVersion == pkg.CurrentVersion)
                continue;

            var selectedProjects = pkg.Projects.Where(p => p.IsSelectedForUpgrade).ToList();
            if (usesCpm)
                totalUpgrades += 1; // CPM: one update per package
            else
                totalUpgrades += selectedProjects.Count; // Individual: one update per project
        }

        int currentUpgradeIndex = 0;

        try
        {
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ApplyingUpdates,
                Message = $"Starting to apply {totalUpgrades} upgrades...",
                PercentComplete = 0
            });

            foreach (var pkg in packagesToUpgrade)
            {
                if (string.IsNullOrEmpty(pkg.SelectedVersion) || pkg.SelectedVersion == pkg.CurrentVersion)
                {
                    continue; // Skip if no upgrade selected
                }

                // Get selected projects
                var selectedProjects = pkg.Projects.Where(p => p.IsSelectedForUpgrade).ToList();
                if (!selectedProjects.Any()) continue;

                // Apply updates
                if (usesCpm)
                {
                    // If using CPM, we update the central props file once
                    currentUpgradeIndex++;
                    int percent = (int)((double)currentUpgradeIndex / totalUpgrades * 90);
                    RaiseProgress(new ProgressUpdate
                    {
                        Stage = ProgressStage.ApplyingUpdates,
                        CurrentPackage = pkg.PackageId,
                        Message = $"Applying upgrade: {pkg.PackageId} {pkg.CurrentVersion} → {pkg.SelectedVersion} (CPM) ({currentUpgradeIndex}/{totalUpgrades})",
                        PercentComplete = percent
                    });
                    _logger?.LogInformation($"Updating CPM for {pkg.PackageId} to {pkg.SelectedVersion}");
                    await TryUpdateAndVerifyAsync(cpmPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion);
                }
                else
                {
                    // Otherwise, update each selected project individually
                    foreach (var proj in selectedProjects)
                    {
                        currentUpgradeIndex++;
                        int percent = (int)((double)currentUpgradeIndex / totalUpgrades * 90);
                        RaiseProgress(new ProgressUpdate
                        {
                            Stage = ProgressStage.ApplyingUpdates,
                            CurrentPackage = pkg.PackageId,
                            CurrentProject = proj.ProjectName,
                            Message = $"Applying upgrade: {pkg.PackageId} {pkg.CurrentVersion} → {pkg.SelectedVersion} in {proj.ProjectName} ({currentUpgradeIndex}/{totalUpgrades})",
                            PercentComplete = percent
                        });
                        _logger?.LogInformation($"Updating Project {proj.ProjectName} for {pkg.PackageId} to {pkg.SelectedVersion}");
                        await TryUpdateAndVerifyAsync(proj.ProjectPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion);
                    }
                }
            }

            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Complete,
                Message = "All upgrades completed successfully!",
                PercentComplete = 100
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error during upgrade: {ex.Message}");
            throw;
        }
    }

    private async Task TryUpdateAndVerifyAsync(string projectOrPropsPath, string packageId, string? oldVersion, string newVersion)
    {
        if (oldVersion == null || newVersion == "Unknown") return;

        try
        {
            // Apply update
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ApplyingUpdates,
                CurrentPackage = packageId,
                Message = $"Updating {packageId} to {newVersion}...",
                PercentComplete = -1 // Indeterminate
            });
            _projectParser.UpdatePackageVersion(projectOrPropsPath, packageId, newVersion);

            // Verify build
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Verifying,
                CurrentPackage = packageId,
                Message = $"Verifying build for {packageId}...",
                PercentComplete = -1 // Indeterminate
            });
            var buildDir = Path.GetDirectoryName(projectOrPropsPath) ?? string.Empty;
            var success = await _cliService.BuildAsync(buildDir);

            if (success)
            {
                _logger?.LogInformation($"Success: {packageId} updated to {newVersion}.");
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.Verifying,
                    CurrentPackage = packageId,
                    Message = $"✓ Build verified for {packageId} {newVersion}",
                    PercentComplete = -1
                });
                await SaveAuditLogAsync(projectOrPropsPath, packageId, oldVersion, newVersion);
            }
            else
            {
                _logger?.LogWarning($"Build failed after updating {packageId}. Rolling back to {oldVersion}.");
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.Verifying,
                    CurrentPackage = packageId,
                    Message = $"Build failed for {packageId}. Rolling back to {oldVersion}...",
                    PercentComplete = -1
                });
                // Rollback
                _projectParser.UpdatePackageVersion(projectOrPropsPath, packageId, oldVersion);
                // Verify rollback
                await _cliService.BuildAsync(buildDir);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error updating {packageId}: {ex.Message}");
            throw;
        }
    }

    private async Task SaveAuditLogAsync(string projectOrPropsPath, string packageId, string oldVersion, string newVersion)
    {
        var logDir = Path.GetDirectoryName(projectOrPropsPath) ?? string.Empty;
        var dateStr = DateTime.Now.ToString("yyMMdd");
        var logFileName = $"nugetAudit_{dateStr}.json";
        var logFilePath = Path.Combine(logDir, logFileName);

        var logEntry = new
        {
            Timestamp = DateTime.Now,
            PackageId = packageId,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            FileUpdated = Path.GetFileName(projectOrPropsPath)
        };

        var logEntries = new List<object>();

        if (File.Exists(logFilePath))
        {
            try
            {
                var existingJson = await File.ReadAllTextAsync(logFilePath);
                var existingLogs = System.Text.Json.JsonSerializer.Deserialize<List<object>>(existingJson);
                if (existingLogs != null)
                {
                    logEntries.AddRange(existingLogs);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to read existing audit log {logFilePath}: {ex.Message}");
            }
        }

        logEntries.Add(logEntry);

        try
        {
            var newJson = System.Text.Json.JsonSerializer.Serialize(logEntries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(logFilePath, newJson);
        }
        catch (Exception ex)
        {
             _logger?.LogError($"Failed to write audit log {logFilePath}: {ex.Message}");
        }
    }
}
