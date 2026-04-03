using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProjectAuditor.Core.Models;

namespace ProjectAuditor.Core.Services;

public class PackageGroupService
{
    private readonly ILogger<PackageGroupService>? _logger;
    private readonly string _globalConfigDir;
    private readonly string _globalConfigPath;

    public PackageGroupService(ILogger<PackageGroupService>? logger = null)
    {
        _logger = logger;
        _globalConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProjectAuditor");
        _globalConfigPath = Path.Combine(_globalConfigDir, "global-groups.json");
    }

    /// <summary>
    /// Loads the combined known groups from global logic and project-specific logic.
    /// </summary>
    public async Task<List<PackageGroup>> LoadGroupsAsync(string targetProjectOrSolutionRoot)
    {
        var groups = new Dictionary<string, PackageGroup>();

        // 1. Load global
        var globalGroups = await LoadFromFileAsync(_globalConfigPath);
        foreach (var g in globalGroups)
            groups[g.GroupName] = g;

        // 2. Load project specific
        var projectConfigPath = Path.Combine(targetProjectOrSolutionRoot, "nuget-groups.json");
        var projectGroups = await LoadFromFileAsync(projectConfigPath);
        foreach (var g in projectGroups)
            groups[g.GroupName] = g;

        return groups.Values.ToList();
    }

    /// <summary>
    /// Detects groups from a given list of package IDs and saves them if they are new.
    /// Default logic: group by first 2 or 3 segments (e.g., Microsoft.Extensions.*).
    /// </summary>
    public async Task DetectAndSaveGroupsAsync(IEnumerable<string> packageIds, string targetProjectOrSolutionRoot)
    {
        var existingGroups = await LoadGroupsAsync(targetProjectOrSolutionRoot);
        var newlyDetected = DetectGroups(packageIds, existingGroups);

        if (newlyDetected.Any())
        {
            _logger?.LogInformation($"Detected {newlyDetected.Count} new package groups.");
            
            // Save newly detected to project specific file
            var projectConfigPath = Path.Combine(targetProjectOrSolutionRoot, "nuget-groups.json");
            var projectGroups = await LoadFromFileAsync(projectConfigPath);
            
            foreach (var g in newlyDetected)
            {
                var existing = projectGroups.FirstOrDefault(x => x.GroupName == g.GroupName);
                if (existing != null)
                {
                    existing.KnownPackages = existing.KnownPackages.Union(g.KnownPackages).Distinct().ToList();
                }
                else
                {
                    projectGroups.Add(g);
                }
            }

            try
            {
                var json = JsonSerializer.Serialize(projectGroups, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(projectConfigPath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to save project groups to {projectConfigPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Maps a package to a group name based on known groups.
    /// </summary>
    public string? GetGroupName(string packageId, List<PackageGroup> knownGroups)
    {
        // Direct match in known packages
        var group = knownGroups.FirstOrDefault(g => g.KnownPackages.Contains(packageId, StringComparer.OrdinalIgnoreCase));
        if (group != null) return group.GroupName;

        // Prefix match
        group = knownGroups.OrderByDescending(g => g.Prefix.Length)
                           .FirstOrDefault(g => packageId.StartsWith(g.Prefix, StringComparison.OrdinalIgnoreCase) && g.Prefix.Contains("."));
        
        return group?.GroupName;
    }

    private List<PackageGroup> DetectGroups(IEnumerable<string> packageIds, List<PackageGroup> knownGroups)
    {
        var newGroups = new List<PackageGroup>();
        var unassignedPackages = packageIds.Where(p => GetGroupName(p, knownGroups) == null).ToList();

        // Very basic grouping: if packages share the first two segments and there are more than 1 sharing it
        var prefixGroups = unassignedPackages
            .Select(p => new { Id = p, Parts = p.Split('.') })
            .Where(x => x.Parts.Length >= 2)
            .GroupBy(x => string.Join(".", x.Parts.Take(Math.Min(2, x.Parts.Length - 1))))
            .Where(g => g.Count() > 1)
            .ToList();

        // For Microsoft.Extensions we want 3 segments
        var msExtensions = unassignedPackages
            .Where(p => p.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        if (msExtensions.Count > 1)
        {
            var grp = new PackageGroup
            {
                GroupName = "Microsoft.Extensions",
                Prefix = "Microsoft.Extensions.",
                KnownPackages = msExtensions
            };
            newGroups.Add(grp);
            // Remove them from prefix groups processing
            prefixGroups.RemoveAll(g => g.Key.StartsWith("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase));
        }

        foreach (var pg in prefixGroups)
        {
            newGroups.Add(new PackageGroup
            {
                GroupName = pg.Key,
                Prefix = pg.Key + ".",
                KnownPackages = pg.Select(x => x.Id).ToList()
            });
        }

        return newGroups;
    }

    private async Task<List<PackageGroup>> LoadFromFileAsync(string path)
    {
        if (!File.Exists(path)) return new List<PackageGroup>();

        try
        {
            var content = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(content)) return new List<PackageGroup>();
            return JsonSerializer.Deserialize<List<PackageGroup>>(content) ?? new List<PackageGroup>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error loading groups from {path}: {ex.Message}");
            return new List<PackageGroup>();
        }
    }
}
