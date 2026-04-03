namespace ProjectAuditor.Core.Models;

/// <summary>
/// Enum für die verschiedenen Phasen einer Progress-Rückmeldung
/// </summary>
public enum ProgressStage
{
    ScanningVulnerable,
    ScanningOutdated,
    ScanningDeprecated,
    FetchingVersions,
    ApplyingUpdates,
    Verifying,
    Complete,
    Failed
}

/// <summary>
/// Klasse für Progress-Updates beim Scanning und Upgraden
/// </summary>
public class ProgressUpdate
{
    /// <summary>
    /// Aktuelle Phase der Operation
    /// </summary>
    public ProgressStage Stage { get; set; }

    /// <summary>
    /// Aktuelles Paket, das verarbeitet wird (optional)
    /// </summary>
    public string? CurrentPackage { get; set; }

    /// <summary>
    /// Aktuelles Projekt, das verarbeitet wird (optional)
    /// </summary>
    public string? CurrentProject { get; set; }

    /// <summary>
    /// Beschreibende Nachricht für die Benutzer
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Fortschritt in Prozent (0-100)
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Ob die Operation einen Fehler enthalten hat
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// Zusätzliche Details (optional)
    /// </summary>
    public string? Details { get; set; }
}
