using ProjectAuditor.Core.Services;
using ProjectAuditor.Core.Parsers;
using ProjectAuditor.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("===================");
Console.WriteLine("Projekt Auditor CLI");
Console.WriteLine("===================\n");

string path = Directory.GetCurrentDirectory();
bool autoFix = false;
var cliSettings = new ProjectAuditorSettings { AuditMode = NuGetAuditMode.All, AuditLevel = NuGetAuditLevel.High };

for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--path" || args[i] == "-p") && i + 1 < args.Length)
    {
        path = args[++i];
    }
    else if (args[i] == "--auto-fix" || args[i] == "-f")
    {
        autoFix = true;
    }
    else if ((args[i] == "--mode" || args[i] == "-m") && i + 1 < args.Length)
    {
        if (Enum.TryParse<NuGetAuditMode>(args[++i], true, out var m)) cliSettings.AuditMode = m;
    }
    else if ((args[i] == "--level" || args[i] == "-l") && i + 1 < args.Length)
    {
        if (Enum.TryParse<NuGetAuditLevel>(args[++i], true, out var l)) cliSettings.AuditLevel = l;
    }
    else if (args[i] == "--help" || args[i] == "-h")
    {
        Console.WriteLine("Usage: auditor [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --path, -p <path>      Pfad zum Projekt oder Solution Ordner. Standard: Aktuelles Verzeichnis.");
        Console.WriteLine("  --mode, -m <mode>      Audit Modus (Direct, All). Standard: All.");
        Console.WriteLine("  --level, -l <level>    Audit Schweregrad (Low, Moderate, High, Critical). Standard: High.");
        Console.WriteLine("  --auto-fix, -f         Führt automatische Updates und Rollbacks durch.");
        Console.WriteLine("  --help, -h             Zeigt diese Hilfe an.");
        return;
    }
}

Console.WriteLine($"Prüfe Verzeichnis: {path}");
Console.WriteLine($"Audit Modus: {cliSettings.AuditMode}");
Console.WriteLine($"Audit Level: {cliSettings.AuditLevel}");
Console.WriteLine($"Auto-Fix aktiviert: {autoFix}\n");

var settingsService = new CliSettingsService(cliSettings);
var cliService = new DotNetCliService(settingsService);
var projectParser = new ProjectParser();

// Use a simple console logger for the engine if auto-fix is on, or a null logger if not.
ILogger<AuditorEngine> logger = NullLogger<AuditorEngine>.Instance;
using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
});
if (autoFix)
{
    logger = loggerFactory.CreateLogger<AuditorEngine>();
}

var engine = new AuditorEngine(cliService, projectParser, logger);

try
{
    if (autoFix)
    {
        Console.WriteLine("Starte automatische Prüfung und Reparatur...");
        var upgradable = await engine.GetUpgradablePackagesAsync(path);
        
        // Auto-fix everything that has a newer version available
        foreach(var pkg in upgradable)
        {
            if (pkg.AvailableVersions.Any())
            {
                pkg.SelectedVersion = pkg.AvailableVersions.First();
                foreach(var proj in pkg.Projects)
                {
                    proj.IsSelectedForUpgrade = true;
                }
            }
        }

        await engine.ApplyUpgradesAsync(path, upgradable);
        Console.WriteLine("Vorgang abgeschlossen!");
    }
    else
    {
        Console.WriteLine("Analysiere Pakete...\n");

        // Get all upgradable packages (vulnerable, outdated, deprecated)
        var upgradable = await engine.GetUpgradablePackagesAsync(path);

        if (upgradable.Any())
        {
            // Group by reason
            var vulnerablePackages = upgradable.Where(p => p.Reasons.Contains("Vulnerable")).ToList();
            var outdatedPackages = upgradable.Where(p => p.Reasons.Contains("Outdated") && !p.Reasons.Contains("Vulnerable")).ToList();
            var deprecatedPackages = upgradable.Where(p => p.Reasons.Contains("Deprecated")).ToList();

            // Display vulnerable packages first
            if (vulnerablePackages.Any())
            {
                Console.WriteLine("=== SICHERHEITSLÜCKEN (Vulnerable) ===\n");
                foreach (var pkg in vulnerablePackages)
                {
                    Console.WriteLine($"Paket: {pkg.PackageId}");
                    Console.WriteLine($"  Aktuelle Version: {pkg.CurrentVersion}");
                    Console.WriteLine($"  Verfügbare Versionen: {string.Join(", ", pkg.AvailableVersions)}");
                    Console.WriteLine($"  Betroffene Projekte: {string.Join(", ", pkg.Projects.Select(p => System.IO.Path.GetFileName(p.ProjectPath)))}");

                    if (pkg.Vulnerabilities.Any())
                    {
                        Console.WriteLine("  Sicherheitslücken:");
                        foreach (var vuln in pkg.Vulnerabilities)
                        {
                            Console.WriteLine($"    - {vuln.Severity}: {vuln.AdvisoryUrl}");
                        }
                    }
                    Console.WriteLine();
                }
            }

            // Display outdated packages
            if (outdatedPackages.Any())
            {
                Console.WriteLine("=== VERALTETE PAKETE (Outdated) ===\n");
                foreach (var pkg in outdatedPackages)
                {
                    Console.WriteLine($"Paket: {pkg.PackageId}");
                    Console.WriteLine($"  Aktuelle Version: {pkg.CurrentVersion}");
                    Console.WriteLine($"  Verfügbare Versionen: {string.Join(", ", pkg.AvailableVersions)}");
                    Console.WriteLine($"  Betroffene Projekte: {string.Join(", ", pkg.Projects.Select(p => System.IO.Path.GetFileName(p.ProjectPath)))}");
                    Console.WriteLine();
                }
            }

            // Display deprecated packages
            if (deprecatedPackages.Any())
            {
                Console.WriteLine("=== VERALTETE/ABGELAUFENE PAKETE (Deprecated) ===\n");
                foreach (var pkg in deprecatedPackages)
                {
                    Console.WriteLine($"Paket: {pkg.PackageId}");
                    Console.WriteLine($"  Aktuelle Version: {pkg.CurrentVersion}");
                    Console.WriteLine($"  Alternative: {pkg.AlternativePackage ?? "Keine Alternative angegeben"}");
                    Console.WriteLine($"  Betroffene Projekte: {string.Join(", ", pkg.Projects.Select(p => System.IO.Path.GetFileName(p.ProjectPath)))}");
                    Console.WriteLine();
                }
            }

            Console.WriteLine($"\nGesamt: {vulnerablePackages.Count} vulnerable, {outdatedPackages.Count} outdated, {deprecatedPackages.Count} deprecated");
            Console.WriteLine("\nFühre den Befehl mit '--auto-fix' oder '-f' aus, um automatisch Updates durchzuführen.");
        }
        else
        {
            Console.WriteLine("Keine Paket-Updates verfügbar. Alle Pakete sind aktuell und sicher.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ein Fehler ist aufgetreten: {ex.Message}");
}

class CliSettingsService : ISettingsService
{
    private readonly ProjectAuditorSettings _settings;
    public CliSettingsService(ProjectAuditorSettings settings) => _settings = settings;
    public Task<ProjectAuditorSettings> LoadSettingsAsync() => Task.FromResult(_settings);
    public Task SaveSettingsAsync(ProjectAuditorSettings settings) => Task.CompletedTask;
}
