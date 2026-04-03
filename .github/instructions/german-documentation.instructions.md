---
description: Dokumentation und Code-Kommentare in deutscher Sprache
applyTo: '**'
---

# Deutsche Dokumentation und Kommentare

## Sprachregel für Code
- **Alle XML-Dokumentation** (///) muss auf Deutsch sein
- **Alle Code-Kommentare** müssen auf Deutsch sein
- **Alle ProgressUpdate.Message** sollten auf Deutsch sein
- Public API-Dokumentation kann bilingual sein (Deutsch/English)

## XML-Dokumentation Format
Verwende immer deutsche Beschreibungen:

```csharp
/// <summary>
/// Erstellt ein neues Build-Ergebnis mit Erfolgs- oder Fehlerstatus.
/// </summary>
/// <param name="targetPath">Pfad zum Build-Ziel.</param>
/// <returns>Ein BuildResult-Objekt mit Details zum Erfolg oder Fehler.</returns>
public async Task<BuildResult> BuildAsync(string targetPath)
```

## Kommentare
- Erkläre komplexe Logik auf Deutsch
- Nutze aussagekräftige Variablennamen (auf Englisch, aber deutsche Kommentare)

Beispiel:
```csharp
// Lese stderr ab, um Fehlerdetails zu erfassen
string error = await process.StandardError.ReadToEndAsync();

// Unterscheide zwischen verschiedenen Fehlertypen
if (process.ExitCode != 0)
{
    // Build fehlgeschlagen - fehlerdetails erfassen
}
```

## Fehlermeldungen
- Alle Fehlermeldungen für den Benutzer müssen auf Deutsch sein
- Protokollmeldungen (Logger) sollten auf Deutsch sein

Beispiel:
```csharp
_logger?.LogError($"Build für '{groupDisplayName}' fehlgeschlagen: {errorMsg}");
RaiseProgress(new ProgressUpdate { Message = $"✗ Fehler bei Update. {errorMsg}" });
```

## Ausnahmen
- Technische Stack Traces dürfen auf Englisch bleiben
- Third-Party-Fehler werden unverändert übernommen
