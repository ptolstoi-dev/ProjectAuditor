# ProjectAuditor.Cli

Die Kommandozeilenschnittstelle (CLI) für den Projekt Auditor. Diese .NET 10 Konsolenanwendung integriert die `ProjectAuditor.Core` Bibliothek und kann als .NET Global Tool paketiert und installiert werden.

Sie eignet sich besonders für die Automatisierung in CI/CD-Pipelines oder als schnelles Konsolen-Werkzeug für Entwickler.

## Verwendung

Öffne ein Terminal in diesem Verzeichnis:

```bash
# Nur überprüfen und Befunde auflisten
dotnet run -- --path "C:\Pfad\zur\Solution"

# Überprüfen UND versuchen erkannte Schwachstellen automatisch zu beheben
dotnet run -- --path "C:\Pfad\zur\Solution" --auto-fix
```

## Argumente
- `--path <path>`: Pfad zum Projekt oder Solution Ordner. Standard: Aktuelles Verzeichnis.
- `--auto-fix`: Führt automatische Updates und Rollbacks durch.
- `--help, -h`: Zeigt die Hilfe an.
