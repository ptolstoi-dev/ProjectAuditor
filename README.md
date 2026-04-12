# Project Auditor

Ein Tool zur automatisierten Überprüfung und Aktualisierung von NuGet-Paketen in .NET-Projekten hinsichtlich Aktualität und Sicherheitslücken.

## Zweck
Die Applikation überprüft eine gegebene .NET-Projektmappe (Solution) oder einzelne Projekte auf anfällige (vulnerable), veraltete (deprecated) oder aktualisierbare NuGet-Pakete (inklusive transitiver Abhängigkeiten). Bevor Updates in die Dateien geschrieben werden, analysiert der **DependencyResolver** den Abhängigkeitsgraphen (Pre-Flight Check) um Versionskonflikte frühzeitig zu erkennen. Dadurch werden fehlgeschlagene Builds und langwierige Rollbacks vermieden. Unterstützte Builds verifizieren im Anschluss die Integrität.

## Technologie-Stack & Architektur
*   **Sprache:** C#
*   **Framework:** .NET 10
*   **Kern-Logik (Engine):** .NET 10 Klassenbibliothek (plattformunabhängig, wiederverwendbar).
*   **CLI (Command Line Interface):** .NET 10 Console Application, installierbar als .NET Global Tool (ideal für CI/CD, WSL, Docker).
*   **GUI (Graphical User Interface):** Photino.Blazor App (für eine leichtgewichtige, plattformübergreifende Desktop-Oberfläche basierend auf Web-Technologien).
*   **Datenspeicherung:** Lokale JSON-Datei (`nugetAudit_yyMMdd.json`) im jeweiligen Projektverzeichnis zur Protokollierung der Änderungen (unterstützt Central Package Management und projektbezogene Paketverwaltung).

## Roadmap & Checkliste

### Phase 1: Projekt-Setup & Infrastruktur
- [x] Erstellen der Projektstruktur (Solution, Class Library, Console App, Photino.Blazor App) für .NET 10.
- [x] Einrichten von grundlegenden Tooling- und Build-Parametern.

### Phase 2: Core-Engine - Analyse
- [x] Implementieren des Parsers für `.csproj` und `Directory.Packages.props` (Central Package Management).
- [x] Anbindung an die NuGet API (oder Programmgesteuerte Nutzung der `dotnet list package`-Befehle), um Paket-Metadaten (Vulnerabilities, Upgrades) abzurufen.
- [x] Logik zur Analyse von transitiven Paketen und Entscheidungsfindung zum Hinzufügen von direkten Verweisen.

### Phase 3: Core-Engine - Aktion & Validierung
- [x] Implementieren der Update-Logik für NuGet-Pakete in den Projekt- bzw. Props-Dateien.
- [x] Programmatisches Ausführen des Build-Prozesses (`dotnet build`) zur Überprüfung der Kompatibilität.
- [x] Implementieren der Fehlerbehandlung: Schleife zum Ausprobieren älterer, aber sicherer Versionen bei Build-Fehlern.
- [x] Implementieren des zuverlässigen Rollbacks auf die ursprüngliche Paketversion, falls kein Update zu einem erfolgreichen Build führt.

### Phase 4: Speicherung & Protokollierung
- [x] Erstellen der Logik zum Generieren und Speichern der `nugetAudit_yyMMdd.json` (Inhalt: NuGet-Name, alte Version, neue Version, Projektname) direkt im Projektordner.

### Phase 5: User Interfaces
- [x] **CLI:** Ausbauen der Konsolenanwendung mit Argument-Parsing (z.B. `--path`, `--auto-fix`). Funktion als Global Tool vorbereiten.
- [x] **GUI:** Entwickeln der Photino.Blazor-Oberfläche (Ordner/Solution-Auswahl, Fortschrittsbalken, tabellarische Ergebnis- und interaktive Update-Ansicht).

### Phase 6: Testing & Deployment
- [x] Schreiben von dedizierten Tests für die Projektmanipulation und die Rollback-Funktionalität.
- [x] Testen der Ausführung unter Windows, Linux (WSL) und in Docker-Umgebungen.
- [x] Fertigstellen der Paketierung (NuGet Package für das Global Tool und Standalone-Executable für die Blazor App).

---

## Implementierung & Architektur Details

* **`ProjectAuditor.Core`**: Beinhaltet die Logik. `ProjectParser.cs` analysiert und verändert die Projektdateien oder `Directory.Packages.props`. Vor Änderungen prüft der `NuGetDependencyResolver` (basierend auf der offiziellen NuGet-API) den Kompatibilitätsgraphen ab, sodass Fehlbildungen vermieden werden. Die `AuditorEngine.cs` koordiniert die Updates mithilfe des `DotNetCliService.cs` (`dotnet list package --vulnerable`), initiiert anschließende Build-Verifikationen. Die Änderungen werden mit dem `AuditLogger.cs` protokolliert.
* **`ProjectAuditor.Cli`**: Das Global Tool mit Argument-Parsing. Integriert das Core-Backend.
* **`ProjectAuditor.Gui`**: Die Desktop Blazor-Basis. Die UI sitzt in `Pages/Index.razor`, in der die Core-Services via Dependency Injection (DI) zur Verfügung stehen.

## Verwendung der Applikationen

### 1. Kommandozeile (CLI)
Das CLI-Tool eignet sich besonders für die Automatisierung (CI/CD) oder als schnelles Konsolen-Werkzeug.

Öffne ein Terminal in `ProjectAuditor.Cli`:
```bash
# Nur überprüfen und Befunde auflisten
dotnet run -- --path "C:\Pfad\zur\Solution"

# Überprüfen UND versuchen erkannte Schwachstellen automatisch Updates auszuführen / Fixen
dotnet run -- --path "C:\Pfad\zur\Solution" --auto-fix
```

### 2. Desktop-Anwendung (Photino.Blazor GUI)
Eine saubere Desktop-Oberfläche, um den Bericht visuell darzustellen und Updates per Knopfdruck auszulösen.

Öffne ein Terminal in `ProjectAuditor.Gui`:
```bash
# Startet die Desktop App
dotnet run
```
Gib dort den Pfad zum Projekt in der Oberfläche ein und nutze die Schaltflächen "Scannen" und "Auto-Fix starten".
