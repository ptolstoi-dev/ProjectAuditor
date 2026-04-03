using ProjectAuditor.Core.Services;
using Xunit;

namespace ProjectAuditor.Tests;

public class LocalizationTests
{
    [Fact]
    public void LocalizationService_LoadsAllTranslations()
    {
        // Arrange
        var service = new LocalizationService();

        // Act
        var titleDe = service.GetString("Index.Title");
        service.SetLanguage("en");
        var titleEn = service.GetString("Index.Title");
        service.SetLanguage("es");
        var titleEs = service.GetString("Index.Title");

        // Assert
        // Wenn die Dateien nicht geladen werden konnten, wird der Key zurückgegeben
        Assert.NotEqual("Index.Title", titleDe);
        Assert.NotEqual("Index.Title", titleEn);
        Assert.NotEqual("Index.Title", titleEs);
        
        Assert.Equal("Project Auditor", titleDe);
        Assert.Equal("Project Auditor", titleEn);
        Assert.Equal("Project Auditor", titleEs);
    }

    [Fact]
    public void LocalizationService_NestedKeys_Work()
    {
        // Arrange
        var service = new LocalizationService();
        service.SetLanguage("de");

        // Act
        var settingsTitle = service.GetString("Settings.Title");

        // Assert
        Assert.Equal("Einstellungen", settingsTitle);
    }

    [Fact]
    public void LocalizationService_AuditKeys_LoadCorrect()
    {
        // Arrange
        var service = new LocalizationService();
        
        // Act & Assert (DE)
        service.SetLanguage("de");
        Assert.Equal("Audit Log", service.GetString("Audit.LogTitle"));
        Assert.Equal("Keine Log-Einträge gefunden.", service.GetString("Audit.NoLogsFound"));

        // Act & Assert (EN)
        service.SetLanguage("en");
        Assert.Equal("Audit Log", service.GetString("Audit.LogTitle"));
        Assert.Equal("No log entries found.", service.GetString("Audit.NoLogsFound"));

        // Act & Assert (ES)
        service.SetLanguage("es");
        Assert.Equal("Registro de Auditoría", service.GetString("Audit.LogTitle"));
        Assert.Equal("No se encontraron entradas en el registro.", service.GetString("Audit.NoLogsFound"));
    }
}
