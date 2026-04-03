using System;

namespace ProjectAuditor.Core.Models;

public class AuditLogModel
{
    public DateTime Timestamp { get; set; }
    public required string PackageId { get; set; }
    public string? OldVersion { get; set; }
    public required string NewVersion { get; set; }
    public string? FileUpdated { get; set; }
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
}
