using System.Text.Json;

namespace ProjectAuditor.Core.Services;

public class AuditRecord
{
    public required string PackageName { get; init; }
    public required string OldVersion { get; init; }
    public required string NewVersion { get; init; }
    public required string TargetProject { get; init; }
}

public class AuditLogger
{
    private readonly List<AuditRecord> _records = new();

    public void AddRecord(string packageName, string oldVersion, string newVersion, string targetProject)
    {
        _records.Add(new AuditRecord
        {
            PackageName = packageName,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            TargetProject = targetProject
        });
    }

    public void SaveToFile(string solutionDirectory)
    {
        if (_records.Count == 0) return;

        var filename = $"nugetAudit_{DateTime.Now:yyMMdd}.json";
        var json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
        
        var fullPath = Path.Combine(solutionDirectory, filename);
        File.WriteAllText(fullPath, json);
    }
}
