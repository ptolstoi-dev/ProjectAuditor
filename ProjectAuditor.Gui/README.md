# ProjectAuditor.Gui

Die Desktop-Anwendung für den Projekt Auditor. Dieses Projekt verwendet **Photino.Blazor**, um eine leichtgewichtige, plattformübergreifende Benutzeroberfläche (Windows, Linux, macOS) mit Web-Technologien (HTML, CSS, C#) bereitzustellen.

Es greift direkt auf die Dienste der `ProjectAuditor.Core` Engine via Dependency Injection zurück.

## Funktionalitäten

### 🔍 Paket-Audit
- **Outdated Packages**: Findet veraltete NuGet-Pakete
- **Vulnerable Packages**: Erkennt Pakete mit bekannten Sicherheitslücken
- **Deprecated Packages**: Identifiziert veraltete (deprecated) Pakete

### 🛠️ Automatische Updates
- **Intelligente Gruppierung**: Pakete werden intelligent gruppiert, um Abhängigkeitskonflikte zu minimieren
- **Build-Verifikation**: Jedes Update wird durch einen automatischen Build verifiziert
- **Rollback-Mechanismus**: Bei fehlgeschlagenen Updates werden Änderungen automatisch rückgängig gemacht
- **Fehlerdetails**: Aussagekräftige Fehlermeldungen zeigen genau, warum ein Update fehlgeschlagen ist (z.B. gesperrte Dateien beim Build)

### 📊 Fehlerbehandlung & Logging
- **Detaillierte Fehlermeldungen**: Exit-Codes, stderr-Output und Timeout-Benachrichtigungen
- **Gruppenfallback**: Bei Gruppenfehler können Updates einzeln durchgeführt werden
- **Vollständiges Audit-Log**: Alle durchgeführten Updates werden dokumentiert

### ⚙️ Einstellungen
- **Audit-Modus**: Wähle zwischen allen Paketen oder nur direkt verwendeten Paketen
- **Sicherheitsstufe**: Filtere Schwachstellen nach Severity (Low, Moderate, High, Critical)
- **Automatische Updates**: Konfiguriere, ob Updates automatisch angewendet werden sollen

## Verwendung

Eine saubere Desktop-Oberfläche, um den Bericht visuell darzustellen und Updates per Knopfdruck auszulösen.

Öffne ein Terminal in diesem Verzeichnis und starte die App:

```bash
dotnet run
```

Gib in der Oberfläche den Pfad zum zu prüfenden Projekt ein und nutze die Schaltflächen:
- **"Scannen"** - Findet Sicherheitslücken, veraltete und deprecated Pakete
- **"Auto-Fix starten"** - Aktualisiert Pakete mit intelligenter Gruppierung, Verifikation und automatischem Rollback bei Fehlern

## 📝 Hinweis zur Dokumentation

Die Projektdokumentation sollte **immer aktuell gehalten werden**. Bei neuen Funktionalitäten bitte:
- Diese README.md aktualisieren
- Die Inline-Code-Dokumentation (XML-Comments) aktualisieren
- Änderungen im Changelog dokumentieren (falls vorhanden)

Dies hilft anderen Entwicklern und zukünftigen Maintenance-Tasks.
