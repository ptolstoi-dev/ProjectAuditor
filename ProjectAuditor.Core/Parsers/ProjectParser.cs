using System.Xml.Linq;
using ProjectAuditor.Core.Models;

namespace ProjectAuditor.Core.Parsers;

public class ProjectParser
{
    /// <summary>
    /// Parses a .csproj or Directory.Packages.props file and returns all package references and their version numbers.
    /// </summary>
    public ProjectFile Parse(string filePath)
    {
        var isCpmFile = Path.GetFileName(filePath).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);
        var doc = XDocument.Load(filePath);
        var packages = new List<PackageRef>();

        // Find <PackageReference> tags (typically in .csproj)
        var packageReferences = doc.Descendants("PackageReference")
            .Where(e => e.Attribute("Include") != null && e.Attribute("Version") != null)
            .Select(e => new PackageRef
            {
                Name = e.Attribute("Include")!.Value,
                Version = e.Attribute("Version")!.Value,
                IsCentralPackageManagement = false
            });

        // Find <PackageVersion> tags (typically in Directory.Packages.props)
        var packageVersions = doc.Descendants("PackageVersion")
            .Where(e => e.Attribute("Include") != null && e.Attribute("Version") != null)
            .Select(e => new PackageRef
            {
                Name = e.Attribute("Include")!.Value,
                Version = e.Attribute("Version")!.Value,
                IsCentralPackageManagement = true
            });

        packages.AddRange(packageReferences);
        packages.AddRange(packageVersions);

        return new ProjectFile
        {
            FilePath = filePath,
            Packages = packages,
            IsCentralPackageManagementFile = isCpmFile
        };
    }

    /// <summary>
    /// Updates the version of a specific package in the file and saves it.
    /// </summary>
    public void UpdatePackageVersion(string filePath, string packageName, string newVersion)
    {
        var isCpmFile = Path.GetFileName(filePath).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase);
        var doc = XDocument.Load(filePath);
        var elementName = isCpmFile ? "PackageVersion" : "PackageReference";

        var elementsToUpdate = doc.Descendants(elementName)
            .Where(e => e.Attribute("Include")?.Value.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true);

        bool updated = false;
        foreach (var element in elementsToUpdate)
        {
            var versionAttr = element.Attribute("Version");
            if (versionAttr != null)
            {
                versionAttr.Value = newVersion;
                updated = true;
            }
        }

        if (updated)
        {
            doc.Save(filePath);
        }
    }
}
