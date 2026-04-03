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
        var mockCli = new Mock<DotNetCliService>(new Mock<ISettingsService>().Object, new Mock<ILocalizationService>().Object);
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
        mockCli.Setup(c => c.BuildAsync(It.IsAny<string>())).ReturnsAsync(new BuildResult 
        { 
            Success = false, 
            ErrorMessage = "Test build failure" 
        });
        
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

    [Fact]
    public async Task ApplyUpgradesAsync_CreatesLogOnError()
    {
        // Arrange
        var mockCli = new Mock<DotNetCliService>(new Mock<ISettingsService>().Object, new Mock<ILocalizationService>().Object);
        var mockParser = new Mock<ProjectParser>();
        var engine = new AuditorEngine(mockCli.Object, mockParser.Object, null, null);
        
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test.csproj");
        File.WriteAllText(tempFile, "<Project />");
        
        var upgrade = new PackageUpgradeModel
        {
            PackageId = "ErrorPkg",
            CurrentVersion = "1.0.0",
            SelectedVersion = "2.0.0",
            AvailableVersions = new List<string> { "2.0.0" },
            Reasons = new List<string> { "Test" },
            Vulnerabilities = new List<Vulnerability>(),
            Projects = new List<ProjectUsageModel> 
            { 
                new ProjectUsageModel { ProjectPath = tempFile, IsSelectedForUpgrade = true } 
            }
        };

        // Build failure
        mockCli.Setup(c => c.BuildAsync(It.IsAny<string>())).ReturnsAsync(new BuildResult 
        { 
            Success = false, 
            ErrorMessage = "CRITICAL_BUILD_ERROR_TEXT" 
        });

        // Act
        await engine.ApplyUpgradesAsync(tempDir, new List<PackageUpgradeModel> { upgrade });

        // Assert
        var dateStr = DateTime.Now.ToString("yyMMdd");
        var logFile = Path.Combine(tempDir, $"nugetAudit_{dateStr}.json");
        
        Assert.True(File.Exists(logFile), $"Audit log file {logFile} should exist.");
        
        var json = File.ReadAllText(logFile);
        Assert.Contains("Failed", json);
        Assert.Contains("CRITICAL_BUILD_ERROR_TEXT", json);
        Assert.Contains("ErrorPkg", json);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
