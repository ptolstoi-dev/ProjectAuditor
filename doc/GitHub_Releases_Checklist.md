# Checkliste für GitHub Releases & NuGet Packages

Hier ist die Schritt-für-Schritt Checkliste, wie man .NET Projekte (CLI/GUI) automatisiert als GitHub Release publiziert.

## 1. Vorbereitungen im Code (`.csproj` / `Directory.Packages.props`)
- [ ] **NuGet-Metadaten hinzufügen:** In dem Projekt, das als NuGet-Package publiziert werden soll (vermutlich `ProjectAuditor.Core`), Metadaten (`<Authors>`, `<Description>`, `<PackageId>`, `<Version>`) einfügen.
- [ ] **NuGet Pack sicherstellen:** Die Eigenschaften `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` für das Core-Projekt evaluieren (oder explizit packen via Action).

## 2. GitHub Actions Workflow erstellen (CI/CD)
Eine Datei unter `.github/workflows/release.yml` erstellen, die getriggert wird, sobald du einen Release-Tag setzt (z.B. `v1.0.0`).
- [ ] **Workflow Trigger definieren:** Setze Trigger auf `push` mit tags `v*`.
- [ ] **Build & Test Schritt:** Standard `.NET Setup` durchführen, gefolgt von `dotnet restore` und `dotnet test` um zu sichern, dass nichts kaputt released wird.

## 3. Assets für das GitHub Release generieren
- [ ] **CLI Binaries packen:** `dotnet publish ProjectAuditor.Cli/ProjectAuditor.Cli.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/cli-windows`
- [ ] **GUI Binaries packen:** Abhängig von Plattform (Windows z.B. `-r win-x64`).
- [ ] Ordner anschließend als `.zip` komprimieren (Linux `zip`, Windows CLI).

## 4. Release auf GitHub anlegen
- [ ] **Action nutzen (`softprops/action-gh-release`):** Diese Action nimmt die erstellten `.zip` Dateien und hängt sie direkt an die GitHub Release Page an.
- [ ] **Release Notes:** Automatisch aus Commits generieren lassen.

## 5. Pakete auf GitHub Packages (oder NuGet.org) publizieren
- [ ] **Erstellen (Packen):** `dotnet pack ProjectAuditor.Core/ProjectAuditor.Core.csproj -c Release -o ./nupkg`
- [ ] **Pushen:** Verwende `dotnet nuget push` um das Paket mit dem `${{ secrets.GITHUB_TOKEN }}` zu pushen.

## 6. Dein zukünftiger Workflow (Zusammenfassung)
1. Features in `main` mergen.
2. Lokal neuen Tag erstellen: `git tag v1.0.1`
3. Tag pushen: `git push origin v1.0.1`
4. GitHub Action erledigt den Rest!
