# Walkthrough: Project Auditor - Einstellungen und Gezielte Upgrades

Diese Dokumentation beschreibt die neuen Funktionen und Anpassungen im Project Auditor (sowohl GUI als auch CLI) basierend auf den jüngsten Implementierungen.

## Änderungen und Neue Funktionen

### 1. Verbesserte Benutzeroberfläche (GUI)
- **Bessere Lesbarkeit:** Die Anzeige der aktuellen Paketversion in der Blazor-Oberfläche (`Index.razor`) wurde von schwarzer Schrift auf grauem Hintergrund zu weißer Schrift auf dunklem Hintergrund geändert, um den Kontrast und die Lesbarkeit deutlich zu erhöhen.
- **Umfassende Versionsauswahl:** Das Dropdown für Zielversionen enthält nun nicht mehr nur die aktuellste Version, sondern **alle verfügbaren Versionen** eines Pakets (z. B. `10.0.1`, `10.0.2`, `10.0.3`, `10.0.4`). Die empfohlene neueste Version (z. B. `10.0.5`) ist standardmäßig vorausgewählt. Dies wurde durch eine direkte Abfrage der NuGet V3 API umgesetzt.

### 2. Kommandozeilen-Erweiterungen (CLI)
- Das CLI-Tool (`ProjectAuditor.Cli`) wurde um zwei neue Parameter erweitert, um Konsistenz mit den globalen GUI-Einstellungen zu gewährleisten:
  - `--mode <mode>`: Definiert den NuGet-Audit-Modus (`Direct` oder `All`). **Standardwert: `All`**.
  - `--level <level>`: Legt den Mindestschweregrad für das Finden von Verwundbarkeiten fest (`Low`, `Moderate`, `High`, `Critical`). **Standardwert: `High`**.
- Beispiel für die Nutzung: 
  `auditor --path C:\MeinProjekt --mode all --level high --auto-fix`

### 3. Logik und Architektur (Core)
- **NuGet API Integration:** In der `AuditorEngine.cs` wurde die Logik erweitert. Für Pakete, die im Audit auffallen, werden nun über `api.nuget.org/v3-flatcontainer` alle veröffentlichten Versionen abgerufen.
- **Zentrale Einstellungen:** Die Einstellungen für `AuditMode` und `AuditLevel` werden nun sauber sowohl vom GUI (`SettingsService` über `%APPDATA%`) als auch vom CLI (`CliSettingsService` via Start-Parameter) an die Kernlogik und das native `dotnet list package` übergeben.
- **Gezielte Upgrades & Audit Logging:** Nach der Auswahl von Versionen und Ziel-Projekten werden Paket-Updates präzise in `.csproj` oder `Directory.Packages.props` geschrieben. Nach erfolgreicher Bestätigung mittels `dotnet build` wird ein lokales Audit-Protokoll (`nugetAudit_yyMMdd.json`) erstellt, in dem Alt-Version und Neu-Version dokumentiert sind.

## Validierung
- Die neuen CLI-Parameter wurden nahtlos integriert.
- GUI-Fehler (Kontrast) wurden in der Blazor View behoben.
- Alle Projekte (Core, Cli, Gui, Tests) kompilieren sicher und fehlerfrei.
