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
    public async Task ApplyUpgradesAsync_SkipsUpdateWhenDependencyCheckFails()
    {
        // Arrange
        var mockCli = new Mock<DotNetCliService>(new Mock<ISettingsService>().Object, new Mock<ILocalizationService>().Object);
        var mockParser = new Mock<ProjectParser>();
        var mockLogger = new Mock<ILogger<AuditorEngine>>();
        var mockResolver = new Mock<INuGetDependencyResolver>();

        var engine = new AuditorEngine(mockCli.Object, mockParser.Object, null, mockLogger.Object, null, mockResolver.Object);
        
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

        // Mock dependency check failure for single updates (using non-CPM path)
        mockResolver.Setup(r => r.AnalyzeUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new ResolutionResult { IsSafe = false, ErrorMessage = "Test resolution failure" });

        // Track progress updates
        var updates = new List<ProgressUpdate>();
        engine.OnProgress += (u) => updates.Add(u);

        // Act
        await engine.ApplyUpgradesAsync(Path.GetDirectoryName(tempFile)!, new List<PackageUpgradeModel> { upgrade });

        // Assert
        // Should NEVER have called update because the dependency check failed pre-flight
        mockParser.Verify(p => p.UpdatePackageVersion(tempFile, "TestPkg", "2.0.0"), Times.Never);
        mockParser.Verify(p => p.UpdatePackageVersion(tempFile, "TestPkg", "1.0.0"), Times.Never);
        
        // Should have a Verifying stage with IsError = true
        Assert.Contains(updates, u => u.Stage == ProgressStage.Verifying && u.IsError == true);
        
        // Cleanup
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }

    [Fact]
    public async Task ApplyUpgradesAsync_CreatesLogOnError()
    {
        // Arrange
        var mockCli = new Mock<DotNetCliService>(new Mock<ISettingsService>().Object, new Mock<ILocalizationService>().Object);
        var mockParser = new Mock<ProjectParser>();
        var mockResolver = new Mock<INuGetDependencyResolver>();

        var engine = new AuditorEngine(mockCli.Object, mockParser.Object, null, null, null, mockResolver.Object);
        
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

        // Resolution failure
        mockResolver.Setup(r => r.AnalyzeUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new ResolutionResult { IsSafe = false, ErrorMessage = "CRITICAL_RESOLUTION_ERROR_TEXT" });

        // Act
        await engine.ApplyUpgradesAsync(tempDir, new List<PackageUpgradeModel> { upgrade });

        // Assert
        var dateStr = DateTime.Now.ToString("yyMMdd");
        var logFile = Path.Combine(tempDir, $"nugetAudit_{dateStr}.json");
        
        Assert.True(File.Exists(logFile), $"Audit log file {logFile} should exist.");
        
        var json = File.ReadAllText(logFile);
        Assert.Contains("Failed", json);
        Assert.Contains("CRITICAL_RESOLUTION_ERROR_TEXT", json);
        Assert.Contains("ErrorPkg", json);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
