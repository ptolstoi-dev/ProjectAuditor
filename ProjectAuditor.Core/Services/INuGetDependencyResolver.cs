using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectAuditor.Core.Models;

namespace ProjectAuditor.Core.Services;

public interface INuGetDependencyResolver
{
    /// <summary>
    /// Analyzes an update to check if the new version introduces any dependency conflicts.
    /// </summary>
    Task<ResolutionResult> AnalyzeUpdateAsync(string projectOrPropsPath, string packageId, string targetVersion);

    /// <summary>
    /// Analyzes a group update to ensure all packages in the group can be updated together safely.
    /// </summary>
    Task<ResolutionResult> AnalyzeGroupUpdateAsync(string projectOrPropsPath, IEnumerable<PackageUpgradeModel> packages);
}

public class ResolutionResult
{
    public bool IsSafe { get; set; } = true;
    public List<string> ConflictingPackages { get; set; } = new();
    public List<string> RequiredAdditionalUpdates { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
