using ProjectAuditor.Core.Models;
using ProjectAuditor.Core.Services;
using ProjectAuditor.Core.Parsers;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

namespace ProjectAuditor.Tests;

public class AuditorEngineTests
{
    [Fact]
    public async Task ApplyUpgradesAsync_HandlesBuildFailureAndRollsBack()
    {
        // Arrange
        var mockCli = new Mock<DotNetCliService>(new Mock<ISettingsService>().Object);
        var mockParser = new Mock<ProjectParser>();
        var mockLogger = new Mock<ILogger<AuditorEngine>>();
        var engine = new AuditorEngine(mockCli.Object, mockParser.Object, null, mockLogger.Object);
        
        var tempFile = Path.GetTempFileName() + ".csproj";
        File.WriteAllText(tempFile, "<Project />");
        
        var upgrade = new PackageUpgradeModel
        {
            PackageId = "TestPkg",
            CurrentVersion = "1.0.0",
            SelectedVersion = "2.0.0",
            AvailableVersions = new List<string> { "2.0.0" },
            Reasons = new List<string> { "Outdated" },
            Vulnerabilities = new List<Vulnerability>(),
            Projects = new List<ProjectUsageModel> 
            { 
                new ProjectUsageModel { ProjectPath = tempFile, IsSelectedForUpgrade = true } 
            }
        };

        // Mock build failure
        mockCli.Setup(c => c.BuildAsync(It.IsAny<string>())).ReturnsAsync(false);
        
        // Track progress updates
        var updates = new List<ProgressUpdate>();
        engine.OnProgress += (u) => updates.Add(u);

        // Act
        await engine.ApplyUpgradesAsync(Path.GetDirectoryName(tempFile)!, new List<PackageUpgradeModel> { upgrade });

        // Assert
        // Should have called update twice: once for 2.0.0 and once for 1.0.0 (rollback)
        mockParser.Verify(p => p.UpdatePackageVersion(tempFile, "TestPkg", "2.0.0"), Times.Once);
        mockParser.Verify(p => p.UpdatePackageVersion(tempFile, "TestPkg", "1.0.0"), Times.Once);
        
        // Should have a Failed stage in progress
        Assert.Contains(updates, u => u.Stage == ProgressStage.Failed);
        Assert.Contains(updates, u => u.IsError);
        
        // Cleanup
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}
