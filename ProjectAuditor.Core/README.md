# ProjectAuditor.Core

Dies ist das Herzstück des Projekt Auditors. Diese plattformunabhängige Klassenbibliothek (.NET 10) enthält die gesamte Kern-Logik.

## Implementierung & Architektur Details
- **`ProjectParser.cs`**: Analysiert und verändert XML-Projektdateien (`.csproj`) und Central Package Management Dateien (`Directory.Packages.props`).
- **`DotNetCliService.cs`**: Führt Befehle wie `dotnet list package --vulnerable --format json` und `dotnet build` auf dem System aus und parst die JSON-Antworten.
- **`AuditorEngine.cs`**: Orchestriert den Audit-Vorgang. Sie sucht nach Schwachstellen, wendet Updates an, führt einen Build zur Verifikation aus und initiiert bei Fehlern automatisch einen Rollback.
- **`AuditLogger.cs`**: Protokolliert alle erfolgreichen Änderungen in einer generierten `nugetAudit_yyMMdd.json`.
