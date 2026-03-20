# Meine Skills
Ich bin eine erfahrene .NET Entwickler mit Schwerpunkt Web-Applikationen, C#, Typescript, Entity Framework Core. Als IDE habe ich Visual Studio Community, VS Code, Rider, Antigravity, Cursor im Einsatz.

# Ziel
Ziel ist eine Applikation, die auf einem Windows-Rechner oder Linux WSL / Docker läuft und .NET Projekte hinsichtlich Aktualität und Sicherheitslücke Nuget überprüft, zu erstellen. 

## Technologie
Es kann eine Visual Studio und/oder VS Code Extension oder eine Avalonia UI / Blazor Hybrid Applikation sein. Mögliche Lösung mit Python wäre zum Einsteigen auch denkbar.  

## Ablauf
Die Applikation schlägt den Benutzer vor, eine vorher bearbeitete Projektmappe oder neue aus einem Verzeichnis auszuwählen. Die Applikation oder VS Extension prüft den Nuget-Bestand hinsichtlich Anfälligkeit, Veralten oder neuere Version und erarbeitet eine Lösung für Upgrade der betroffenen Pakete. Es sollten auch die transitiven Pakete geprüft werden und Ggfs. ein direktes Verweis auf transitives Paket im Projekt gesetzt werden. Projekt wird erstellt. Läuft Erstellen auf einen Fehler, wird weiter eine Lösung mit anderen Nuget-Versionen gesucht. Gelingt das nicht, werden original Versionen wiederherstellt.  
Die Applikation / Extension speichert den Projektmappe Pfad und die durchgeführten Änderungen im Fall der zentralen Paketverwaltung in Form Nuget-Name, alte Version, neue Version. Beim alten projektbezogenen Paketverwaltung kommt noch das Projektname dazu. Extension speichert die Datei direkt im Projektordner. Dateiname ist nugetAudit_yyMMdd.json.
