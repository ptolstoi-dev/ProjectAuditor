# ProjectAuditor.Gui

Die Desktop-Anwendung für den Projekt Auditor. Dieses Projekt verwendet **Photino.Blazor**, um eine leichtgewichtige, plattformübergreifende Benutzeroberfläche (Windows, Linux, macOS) mit Web-Technologien (HTML, CSS, C#) bereitzustellen.

Es greift direkt auf die Dienste der `ProjectAuditor.Core` Engine via Dependency Injection zurück.

## Verwendung

Eine saubere Desktop-Oberfläche, um den Bericht visuell darzustellen und Updates per Knopfdruck auszulösen.

Öffne ein Terminal in diesem Verzeichnis und starte die App:

```bash
dotnet run
```

Gib in der Oberfläche den Pfad zum zu prüfenden Projekt ein und nutze die Schaltflächen **"Scannen"**, um Sicherheitslücken zu finden, und **"Auto-Fix starten"**, um sie automatisch beheben und verifizieren zu lassen.
