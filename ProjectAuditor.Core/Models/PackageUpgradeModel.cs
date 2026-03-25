using System.Collections.Generic;

namespace ProjectAuditor.Core.Models;

/// <summary>
/// Represents a package that needs an upgrade, aggregating data across multiple projects.
/// </summary>
public class PackageUpgradeModel
{
    public string PackageId { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// A list of available/newer versions fetched from NuGet or from dotnet list.
    /// </summary>
    public List<string> AvailableVersions { get; set; } = new();

    /// <summary>
    /// The version selected by the user to upgrade to.
    /// </summary>
    public string SelectedVersion { get; set; } = string.Empty;

    /// <summary>
    /// Information about vulnerabilities associated with the current version.
    /// </summary>
    public string VulnerabilityInfo { get; set; } = string.Empty;

    /// <summary>
    /// The projects that rely on this package and version.
    /// </summary>
    public List<ProjectUsageModel> Projects { get; set; } = new();

    /// <summary>
    /// Reasons why this package needs an upgrade (e.g., "Vulnerable", "Outdated", "Deprecated").
    /// </summary>
    public List<string> Reasons { get; set; } = new();

    /// <summary>
    /// List of vulnerabilities for this package.
    /// </summary>
    public List<Vulnerability> Vulnerabilities { get; set; } = new();

    /// <summary>
    /// Alternative package name if this package is deprecated.
    /// </summary>
    public string? AlternativePackage { get; set; }
}

public class ProjectUsageModel
{
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName => System.IO.Path.GetFileNameWithoutExtension(ProjectPath);
    
    /// <summary>
    /// Whether the user has selected this project to apply the upgrade.
    /// </summary>
    public bool IsSelectedForUpgrade { get; set; } = true;
}
