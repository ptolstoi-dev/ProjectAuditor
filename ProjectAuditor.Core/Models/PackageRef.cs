namespace ProjectAuditor.Core.Models;

public class PackageRef
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    
    // Determine if it's from a PackageReference or a PackageVersion
    public required bool IsCentralPackageManagement { get; init; }

    public override string ToString() => $"{Name} ({Version})";
}
