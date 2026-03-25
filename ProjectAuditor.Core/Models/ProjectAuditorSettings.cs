namespace ProjectAuditor.Core.Models;

/// <summary>
/// Represents the configuration settings for the Project Auditor application.
/// </summary>
public class ProjectAuditorSettings
{
    /// <summary>
    /// Gets or sets the preferred NuGet Audit Mode (Direct or All dependencies).
    /// </summary>
    public NuGetAuditMode AuditMode { get; set; } = NuGetAuditMode.Direct;

    /// <summary>
    /// Gets or sets the preferred NuGet Audit Level (Low, Moderate, High, Critical).
    /// </summary>
    public NuGetAuditLevel AuditLevel { get; set; } = NuGetAuditLevel.Low;
}
