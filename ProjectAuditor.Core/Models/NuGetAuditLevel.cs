namespace ProjectAuditor.Core.Models;

/// <summary>
/// Specifies the severity level for NuGet audit.
/// </summary>
public enum NuGetAuditLevel
{
    /// <summary>
    /// Audits vulnerabilities with Low severity and higher.
    /// </summary>
    Low,

    /// <summary>
    /// Audits vulnerabilities with Moderate severity and higher.
    /// </summary>
    Moderate,

    /// <summary>
    /// Audits vulnerabilities with High severity and higher.
    /// </summary>
    High,

    /// <summary>
    /// Audits only vulnerabilities with Critical severity.
    /// </summary>
    Critical
}
