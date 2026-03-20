namespace ProjectAuditor.Core.Models;

public class ProjectFile
{
    public required string FilePath { get; init; }
    public required List<PackageRef> Packages { get; init; }
    
    // True if this is Directory.Packages.props
    public required bool IsCentralPackageManagementFile { get; init; }
}
