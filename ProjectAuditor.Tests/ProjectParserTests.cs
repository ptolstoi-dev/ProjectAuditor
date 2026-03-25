using ProjectAuditor.Core.Parsers;

namespace ProjectAuditor.Tests;

public class ProjectParserTests
{
    [Fact]
    public void Parse_CsprojFile_ReturnsPackageReferences()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".csproj";
        var xml = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
            <PackageReference Include="Serilog" Version="3.0.0" />
          </ItemGroup>
        </Project>
        """;
        File.WriteAllText(tempFile, xml);
        var parser = new ProjectParser();

        // Act
        var result = parser.Parse(tempFile);

        // Assert
        Assert.False(result.IsCentralPackageManagementFile);
        Assert.Equal(2, result.Packages.Count);
        Assert.Contains(result.Packages, p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.1" && !p.IsCentralPackageManagement);
        Assert.Contains(result.Packages, p => p.Name == "Serilog" && p.Version == "3.0.0");
        
        // Cleanup
        File.Delete(tempFile);
    }
    
    [Fact]
    public void UpdatePackageVersion_CsprojFile_ChangesVersionNumber()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".csproj";
        var xml = """
        <Project>
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
          </ItemGroup>
        </Project>
        """;
        File.WriteAllText(tempFile, xml);
        var parser = new ProjectParser();

        // Act
        parser.UpdatePackageVersion(tempFile, "Newtonsoft.Json", "13.0.3");
        var result = parser.Parse(tempFile);

        // Assert
        var updatedPackage = result.Packages.First();
        Assert.Equal("13.0.3", updatedPackage.Version);
        
        // Cleanup
        File.Delete(tempFile);
    }
}
