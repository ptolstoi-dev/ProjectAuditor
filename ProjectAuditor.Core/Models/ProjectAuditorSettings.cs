namespace ProjectAuditor.Core.Models;

/// <summary>
/// Stellt die Konfigurationseinstellungen für die Project Auditor Anwendung dar.
/// </summary>
public class ProjectAuditorSettings
{
    /// <summary>
    /// Ruft ab oder legt fest, welcher NuGet Audit Modus bevorzugt ist (Direct oder All Abhängigkeiten).
    /// </summary>
    public NuGetAuditMode AuditMode { get; set; } = NuGetAuditMode.Direct;

    /// <summary>
    /// Ruft ab oder legt fest, welche NuGet Audit Stufe bevorzugt ist (Low, Moderate, High, Critical).
    /// </summary>
    public NuGetAuditLevel AuditLevel { get; set; } = NuGetAuditLevel.Low;

    /// <summary>
    /// Ruft ab oder legt fest, welche Sprache für die Benutzeroberfläche verwendet werden soll (de, en, es).
    /// </summary>
    public string Language { get; set; } = "de";
}
