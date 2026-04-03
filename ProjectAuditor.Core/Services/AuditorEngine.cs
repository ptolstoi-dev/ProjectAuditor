using ProjectAuditor.Core.Models;
using ProjectAuditor.Core.Parsers;
using Microsoft.Extensions.Logging;

namespace ProjectAuditor.Core.Services;

public class AuditorEngine(
    DotNetCliService cliService,
    ProjectParser projectParser,
    PackageGroupService? packageGroupService = null,
    ILogger<AuditorEngine>? logger = null,
    ILocalizationService? localizationService = null)
{
    private readonly ILocalizationService _localizationService = localizationService ?? new LocalizationService();

    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Event für Progress-Rückmeldungen bei Scan und Upgrade
    /// </summary>
    public event Action<ProgressUpdate>? OnProgress;

    /// <summary>
    /// Function called when a group update fails. 
    /// Returns true if the user wants to fallback to individual updates.
    /// Param 1: Group Name, Param 2: Num Packages in Group.
    /// </summary>
    public Func<string, int, Task<bool>>? OnGroupFallback;

    /// <summary>
    /// Hilfsmethode um Progress zu melden
    /// </summary>
    private void RaiseProgress(ProgressUpdate update)
    {
        OnProgress?.Invoke(update);
        logger?.LogInformation($"[{update.Stage}] {update.Message}");
    }

    public async Task<List<PackageUpgradeModel>> GetUpgradablePackagesAsync(string path)
    {
        logger?.LogInformation($"Scanning for upgradable packages in {path}");

        var results = new Dictionary<string, PackageUpgradeModel>();

        try
        {
            // 1. Get Vulnerable packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningVulnerable,
                Message = _localizationService.GetString("Scanning.ScanningVulnerable", "Scanning for vulnerable packages..."),
                PercentComplete = 10
            });
            var vulnerableResponse = await cliService.ListPackagesAsync(path, "vulnerable");
            await ProcessResponseAsync(vulnerableResponse, results, "Vulnerable");

            // 2. Get Outdated packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningOutdated,
                Message = _localizationService.GetString("Scanning.ScanningOutdated", "Scanning for outdated packages..."),
                PercentComplete = 30
            });
            var outdatedResponse = await cliService.ListPackagesAsync(path, "outdated");
            await ProcessResponseAsync(outdatedResponse, results, "Outdated");

            // 3. Get Deprecated packages
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ScanningDeprecated,
                Message = _localizationService.GetString("Scanning.ScanningDeprecated", "Scanning for deprecated packages..."),
                PercentComplete = 50
            });
            var deprecatedResponse = await cliService.ListPackagesAsync(path, "deprecated");
            await ProcessResponseAsync(deprecatedResponse, results, "Deprecated");

            // 4. Fetch versions for all packages
            int totalPackages = results.Count;
            int currentPackageIndex = 0;
            foreach (var package in results.Values)
            {
                currentPackageIndex++;
                int percent = 50 + (int)((double)currentPackageIndex / totalPackages * 40);
                var versionMessage = _localizationService.GetString("Scanning.FetchingVersions", "Fetching versions for {packageId}... ({current}/{total})")
                    .Replace("{packageId}", package.PackageId)
                    .Replace("{current}", currentPackageIndex.ToString())
                    .Replace("{total}", totalPackages.ToString());
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.FetchingVersions,
                    CurrentPackage = package.PackageId,
                    Message = versionMessage,
                    PercentComplete = percent
                });
                // Versions already fetched in ProcessResponse, but this provides feedback
            }

            var completedMessage = totalPackages == 1
                ? _localizationService.GetString("Scanning.ScanCompletedSingle", "Scan completed. 1 package requires updates.")
                : _localizationService.GetString("Scanning.ScanCompletedMulti", "Scan completed. {count} packages require updates.")
                    .Replace("{count}", totalPackages.ToString());

            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Complete,
                Message = completedMessage,
                PercentComplete = 100
            });
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error during scan: {ex.Message}");
            var errorMsg = _localizationService.GetString("Scanning.ErrorDuringScan", "Error during scan: {error}")
                .Replace("{error}", ex.Message);
            logger?.LogError(errorMsg);
            throw;
        }

        var values = results.Values.ToList();

        if (packageGroupService != null)
        {
            var ids = values.Select(v => v.PackageId).ToList();
            await packageGroupService.DetectAndSaveGroupsAsync(ids, path);
            var knownGroups = await packageGroupService.LoadGroupsAsync(path);
            
            foreach (var pkg in values)
            {
                pkg.GroupName = packageGroupService.GetGroupName(pkg.PackageId, knownGroups);
            }
        }

        return values;
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
                    if (reason == "Deprecated" && pkg.DeprecationReasons != null && pkg.DeprecationReasons.Count != 0)
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
            var response = await HttpClient.GetAsync(url);
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
            logger?.LogWarning($"Could not fetch versions for {packageId}: {ex.Message}");
        }
        return [];
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
            if (NuGet.Versioning.NuGetVersion.TryParse(versionString, out var nugetVersion) && nugetVersion != null)
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
        return lowerVersion.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
               lowerVersion.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
               lowerVersion.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
               lowerVersion.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
               lowerVersion.Contains("pre", StringComparison.OrdinalIgnoreCase) ||
               lowerVersion.Contains('-', StringComparison.OrdinalIgnoreCase);  // NuGet pre-release versions contain a hyphen
    }

    public async Task ApplyUpgradesAsync(string basePath, List<PackageUpgradeModel> packagesToUpgrade)
    {
        logger?.LogInformation($"Starting targeted upgrades in {basePath}");

        var cpmPath = Path.Combine(basePath, "Directory.Packages.props");
        var usesCpm = File.Exists(cpmPath);

        // Filter selectable
        var applicablePackages = packagesToUpgrade.Where(pkg => !string.IsNullOrEmpty(pkg.SelectedVersion) && pkg.SelectedVersion != pkg.CurrentVersion && pkg.Projects.Any(p => p.IsSelectedForUpgrade)).ToList();

        if (!applicablePackages.Any())
        {
            var noUpgradesMsg = _localizationService.GetString("Upgrading.NoUpgradesSelected", "No upgrades selected to apply.");
            RaiseProgress(new ProgressUpdate { Stage = ProgressStage.Complete, Message = noUpgradesMsg, PercentComplete = 100 });
            return;
        }

        int failedCount = 0;
        try
        {
            var startingMsg = _localizationService.GetString("Upgrading.StartingUpgrades", "Starting targeted upgrades...");
            RaiseProgress(new ProgressUpdate { Stage = ProgressStage.ApplyingUpdates, Message = startingMsg, PercentComplete = 0 });

            // Group packages by GroupName (or uniquely if null)
            var groups = applicablePackages.GroupBy(p => p.GroupName ?? Guid.NewGuid().ToString()).ToList();

            int totalGroups = groups.Count;
            int currentGroupIndex = 0;

            foreach (var group in groups)
            {
                currentGroupIndex++;
                int percent = (int)((double)currentGroupIndex / totalGroups * 90);

                var packagesInGroup = group.ToList();
                var groupDisplayName = !string.IsNullOrEmpty(packagesInGroup.First().GroupName) ? packagesInGroup.First().GroupName! : packagesInGroup.First().PackageId;

                var groupMsg = _localizationService.GetString("Upgrading.ApplyingGroupUpgrade", "Applying upgrade for group '{group}' ({count} packages) ({current}/{total})")
                    .Replace("{group}", groupDisplayName)
                    .Replace("{count}", packagesInGroup.Count.ToString())
                    .Replace("{current}", currentGroupIndex.ToString())
                    .Replace("{total}", totalGroups.ToString());

                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.ApplyingUpdates,
                    Message = groupMsg,
                    PercentComplete = percent
                });

                if (usesCpm)
                {
                    // 1. Update all packages in CPM file
                    foreach (var pkg in packagesInGroup)
                        projectParser.UpdatePackageVersion(cpmPath, pkg.PackageId, pkg.SelectedVersion);

                    // 2. Build once
                    var buildDir = Path.GetDirectoryName(cpmPath) ?? string.Empty;
                    var buildResult = await cliService.BuildAsync(buildDir);

                        if (buildResult.Success)
                        {
                            foreach (var pkg in packagesInGroup)
                                await SaveAuditLogAsync(cpmPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion);

                            var successMsg = _localizationService.GetString("Upgrading.GroupUpdateSuccess", "✓ Group '{group}' updated successfully.")
                                .Replace("{group}", groupDisplayName);
                            RaiseProgress(new ProgressUpdate { Stage = ProgressStage.ApplyingUpdates, Message = successMsg, PercentComplete = percent });
                        }
                        else
                        {
                            // 3. Rollback
                            foreach (var pkg in packagesInGroup)
                                projectParser.UpdatePackageVersion(cpmPath, pkg.PackageId, pkg.CurrentVersion);

                            var errorMsg = buildResult.ErrorMessage ?? "Unknown build error";
                            logger?.LogError($"Group '{groupDisplayName}' build failed: {errorMsg}");

                            // Speichere fehlgeschlagenen Build im Log für jedes Paket der Gruppe
                            foreach (var pkg in packagesInGroup)
                                await SaveAuditLogAsync(cpmPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion, errorMsg);

                            // Verify rollback - aber speichere auch den Rollback-Result
                            var rollbackResult = await cliService.BuildAsync(buildDir);

                        if (!rollbackResult.Success)
                        {
                            var rollbackErrorMsg = rollbackResult.ErrorMessage ?? "Unknown rollback error";
                            var criticalMsg = _localizationService.GetString("Upgrading.GroupRollbackFailed", "CRITICAL: Rollback build also failed for group '{group}'. Rollback error: {error}")
                                .Replace("{group}", groupDisplayName)
                                .Replace("{error}", rollbackErrorMsg);
                            logger?.LogError(criticalMsg);
                            errorMsg = $"{errorMsg} (CRITICAL: Rollback also failed: {rollbackErrorMsg})";
                        }
                        else
                        {
                            var successMsg = _localizationService.GetString("Upgrading.GroupRollbackSuccess", "Rollback successful for group '{group}'.")
                                .Replace("{group}", groupDisplayName);
                            logger?.LogInformation(successMsg);
                        }

                        var failMsg = _localizationService.GetString("Upgrading.GroupUpdateFailed", "✗ Group '{group}' failed. Rolled back. {error}")
                            .Replace("{group}", groupDisplayName)
                            .Replace("{error}", errorMsg);
                        RaiseProgress(new ProgressUpdate { Stage = ProgressStage.ApplyingUpdates, Message = failMsg, PercentComplete = percent, IsError = true });

                        // 4. Fallback prompt
                        if (OnGroupFallback != null && packagesInGroup.Count > 1)
                        {
                            bool userWantsFallback = await OnGroupFallback(groupDisplayName, packagesInGroup.Count);
                            if (userWantsFallback)
                            {
                                var fallbackMsg = _localizationService.GetString("Upgrading.GroupFallbackMessage", "Fallback: Applying updates for '{group}' individually...")
                                    .Replace("{group}", groupDisplayName);
                                RaiseProgress(new ProgressUpdate { Stage = ProgressStage.ApplyingUpdates, Message = fallbackMsg, PercentComplete = percent });
                                foreach (var pkg in packagesInGroup)
                                {
                                    bool indSuccess = await TryUpdateAndVerifyAsync(cpmPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion, percent);
                                    if (!indSuccess) failedCount++;
                                }
                            }
                            else failedCount += packagesInGroup.Count;
                        }
                        else failedCount += packagesInGroup.Count;
                    }
                }
                else
                {
                    // Non-CPM logic: fallback to individual upgrades per project to keep it simple, or group by project
                    // Here we simply reuse TryUpdateAndVerifyAsync for individual ones.
                    foreach (var pkg in packagesInGroup)
                    {
                        var selectedProjects = pkg.Projects.Where(p => p.IsSelectedForUpgrade).ToList();
                        foreach (var proj in selectedProjects)
                        {
                            bool success = await TryUpdateAndVerifyAsync(proj.ProjectPath, pkg.PackageId, pkg.CurrentVersion, pkg.SelectedVersion, percent);
                            if (!success) failedCount++;
                        }
                    }
                }
            }

            if (failedCount > 0)
            {
                var failureMsg = _localizationService.GetString("Upgrading.CompletedWithFailures", "Completed with failures. {count} packages were rolled back.")
                    .Replace("{count}", failedCount.ToString());
                RaiseProgress(new ProgressUpdate { Stage = ProgressStage.Failed, Message = failureMsg, PercentComplete = 100, IsError = true });
            }
            else
            {
                var successMsg = _localizationService.GetString("Upgrading.CompletedSuccessfully", "All upgrades completed successfully!");
                RaiseProgress(new ProgressUpdate { Stage = ProgressStage.Complete, Message = successMsg, PercentComplete = 100 });
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error during upgrade: {ex.Message}");
            var criticalErrorMsg = _localizationService.GetString("Upgrading.CriticalError", "Critical error: {error}")
                .Replace("{error}", ex.Message);
            RaiseProgress(new ProgressUpdate { Stage = ProgressStage.Failed, Message = criticalErrorMsg, PercentComplete = 100, IsError = true });
            throw;
        }
    }

    private async Task<bool> TryUpdateAndVerifyAsync(string projectOrPropsPath, string packageId, string? oldVersion, string newVersion, int currentPercent)
    {
        if (oldVersion == null || newVersion == "Unknown") return false;

        try
        {
            // Apply update
            var updateMsg = _localizationService.GetString("Upgrading.UpdatingPackage", "Updating {packageId} to {version}...")
                .Replace("{packageId}", packageId)
                .Replace("{version}", newVersion);
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.ApplyingUpdates,
                CurrentPackage = packageId,
                Message = updateMsg,
                PercentComplete = currentPercent
            });
            projectParser.UpdatePackageVersion(projectOrPropsPath, packageId, newVersion);

            // Verify build
            var verifyMsg = _localizationService.GetString("Upgrading.VerifyingBuild", "Verifying build for {packageId}...")
                .Replace("{packageId}", packageId);
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Verifying,
                CurrentPackage = packageId,
                Message = verifyMsg,
                PercentComplete = currentPercent
            });
            var buildDir = Path.GetDirectoryName(projectOrPropsPath) ?? string.Empty;
            var buildResult = await cliService.BuildAsync(buildDir);

            if (buildResult.Success)
            {
                var verifiedMsg = _localizationService.GetString("Upgrading.BuildVerified", "✓ Build verified for {packageId} {version}")
                    .Replace("{packageId}", packageId)
                    .Replace("{version}", newVersion);
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.Verifying,
                    CurrentPackage = packageId,
                    Message = verifiedMsg,
                    PercentComplete = currentPercent
                });
                await SaveAuditLogAsync(projectOrPropsPath, packageId, oldVersion, newVersion);
                return true;
            }
            else
            {
                var errorMsg = buildResult.ErrorMessage ?? "Unknown build error";
                logger?.LogWarning($"Build failed after updating {packageId}. Rolling back to {oldVersion}. Error: {errorMsg}");

                // Speichere fehlgeschlagenen Build im Log
                await SaveAuditLogAsync(projectOrPropsPath, packageId, oldVersion, newVersion, errorMsg);

                var failedMsg = _localizationService.GetString("Upgrading.BuildVerifyFailed", "Build failed for {packageId}. Rolling back to {version}... Error: {error}")
                    .Replace("{packageId}", packageId)
                    .Replace("{version}", oldVersion)
                    .Replace("{error}", errorMsg);
                RaiseProgress(new ProgressUpdate
                {
                    Stage = ProgressStage.Verifying,
                    CurrentPackage = packageId,
                    Message = failedMsg,
                    PercentComplete = currentPercent,
                    IsError = true
                });

                // Rollback
                projectParser.UpdatePackageVersion(projectOrPropsPath, packageId, oldVersion);

                // Verify rollback - aber speichere auch den Rollback-Result
                var rollbackResult = await cliService.BuildAsync(buildDir);

                if (!rollbackResult.Success)
                {
                    var rollbackErrorMsg = rollbackResult.ErrorMessage ?? "Unknown rollback error";
                    var criticalMsg = _localizationService.GetString("Upgrading.PackageRollbackFailed", "CRITICAL: Rollback build also failed for {packageId}. Rollback error: {error}")
                        .Replace("{packageId}", packageId)
                        .Replace("{error}", rollbackErrorMsg);
                    logger?.LogError(criticalMsg);
                }
                else
                {
                    var successMsg = _localizationService.GetString("Upgrading.PackageRollbackSuccess", "Rollback successful for {packageId}.")
                        .Replace("{packageId}", packageId);
                    logger?.LogInformation(successMsg);
                }

                return false;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error updating {packageId}: {ex.Message}");

            var errorUpdateMsg = _localizationService.GetString("Upgrading.ErrorUpdatingPackage", "Error updating {packageId}: {error}")
                .Replace("{packageId}", packageId)
                .Replace("{error}", ex.Message);
            RaiseProgress(new ProgressUpdate
            {
                Stage = ProgressStage.Verifying,
                CurrentPackage = packageId,
                Message = errorUpdateMsg,
                PercentComplete = currentPercent,
                IsError = true
            });
            // Attempt to restore original version if parser failed mid-save (unlikely but safe)
            if (oldVersion != null)
            {
                try { projectParser.UpdatePackageVersion(projectOrPropsPath, packageId, oldVersion); } catch { }
            }
            return false;
        }
    }

    private async Task SaveAuditLogAsync(string projectOrPropsPath, string packageId, string? oldVersion, string newVersion, string? errorMessage = null)
    {
        var logDir = Path.GetDirectoryName(projectOrPropsPath) ?? string.Empty;
        var dateStr = DateTime.Now.ToString("yyMMdd");
        var logFileName = $"nugetAudit_{dateStr}.json";
        var logFilePath = Path.Combine(logDir, logFileName);

        var logEntry = new AuditLogModel
        {
            Timestamp = DateTime.Now,
            PackageId = packageId,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            FileUpdated = Path.GetFileName(projectOrPropsPath),
            Status = string.IsNullOrEmpty(errorMessage) ? "Success" : "Failed",
            ErrorMessage = errorMessage
        };

        var logEntries = new List<AuditLogModel>();

        if (File.Exists(logFilePath))
        {
            try
            {
                var existingJson = await File.ReadAllTextAsync(logFilePath);
                var existingLogs = System.Text.Json.JsonSerializer.Deserialize<List<AuditLogModel>>(existingJson);
                if (existingLogs != null)
                {
                    logEntries.AddRange(existingLogs);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"Failed to read existing audit log {logFilePath}: {ex.Message}");
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
             logger?.LogError($"Failed to write audit log {logFilePath}: {ex.Message}");
        }
    }
}
