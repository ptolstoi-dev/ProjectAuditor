namespace ProjectAuditor.Core.Models;

/// <summary>
/// Stellt das Ergebnis einer dotnet build-Operation dar, inklusive Erfolgs- oder Fehlerstatus und Fehlerdetails.
/// </summary>
public class BuildResult
{
    /// <summary>
    /// Gibt an, ob der Build erfolgreich abgeschlossen wurde.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Die Fehlermeldung, wenn der Build fehlgeschlagen ist. Kann Standard-Ausgabe, Fehlerausgabe oder Timeout-Details enthalten.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Die vollständige Fehlerausgabe (stderr) des dotnet-Prozesses, falls verfügbar.
    /// </summary>
    public string? StdError { get; set; }

    /// <summary>
    /// Der Exit-Code des dotnet-Prozesses.
    /// </summary>
    public int? ExitCode { get; set; }
}
