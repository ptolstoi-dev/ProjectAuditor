using System.Collections.Generic;

namespace ProjectAuditor.Core.Models;

/// <summary>
/// Represents a grouping of packages that should be updated together.
/// </summary>
public class PackageGroup
{
    public string GroupName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    
    /// <summary>
    /// Specific package IDs assigned to this group.
    /// </summary>
    public List<string> KnownPackages { get; set; } = new();
}
