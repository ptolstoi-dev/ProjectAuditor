namespace ProjectAuditor.Core.Models;

/// <summary>
/// Specifies the operation mode for NuGet audit.
/// </summary>
public enum NuGetAuditMode
{
    /// <summary>
    /// Audits only direct, top-level dependencies.
    /// </summary>
    Direct,

    /// <summary>
    /// Audits both direct and all transitive dependencies.
    /// </summary>
    All
}
