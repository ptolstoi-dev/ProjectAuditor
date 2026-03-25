using System.Text.Json.Serialization;

namespace ProjectAuditor.Core.Models;

public record DotNetListPackageResponse(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("parameters")] string Parameters,
    [property: JsonPropertyName("projects")] List<ProjectResult> Projects
);

public record ProjectResult(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("frameworks")] List<FrameworkResult> Frameworks
);

public record FrameworkResult(
    [property: JsonPropertyName("framework")] string Framework,
    [property: JsonPropertyName("topLevelPackages")] List<PackageItem>? TopLevelPackages,
    [property: JsonPropertyName("transitivePackages")] List<PackageItem>? TransitivePackages
);

public record PackageItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("requestedVersion")] string? RequestedVersion,
    [property: JsonPropertyName("resolvedVersion")] string? ResolvedVersion,
    [property: JsonPropertyName("latestVersion")] string? LatestVersion, // For outdated
    [property: JsonPropertyName("deprecationReasons")] List<string>? DeprecationReasons // For deprecated
)
{
    [JsonPropertyName("vulnerabilities")]
    public List<Vulnerability>? Vulnerabilities { get; set; } // For vulnerable
}

public record Vulnerability(
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("advisoryurl")] string AdvisoryUrl
);
