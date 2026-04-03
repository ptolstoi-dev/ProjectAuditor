using System.Text.Json;

namespace ProjectAuditor.Core.Services;

/// <summary>
/// Service für die Verwaltung von Lokalisierungen (Deutsch, Englisch, Spanisch).
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Setzt die aktuelle Sprache.
    /// </summary>
    void SetLanguage(string languageCode);

    /// <summary>
    /// Ruft die aktuelle Sprache ab.
    /// </summary>
    string GetCurrentLanguage();

    /// <summary>
    /// Ruft einen lokalisierten Text anhand des Pfades ab (z.B. "Settings.Title").
    /// </summary>
    string GetString(string key, string? defaultValue = null);

    /// <summary>
    /// Gibt alle verfügbaren Sprachen zurück.
    /// </summary>
    List<(string Code, string Name)> GetAvailableLanguages();
}

/// <summary>
/// Implementierung des Lokalisierungsservice mit JSON-basierten Ressourcen.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private string _currentLanguage = "de";
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

    public LocalizationService()
    {
        LoadTranslations();
    }

    /// <summary>
    /// Lädt alle Übersetzungsdateien in den Speicher.
    /// </summary>
    private void LoadTranslations()
    {
        var languages = new[] { "de", "en", "es" };

        // Versuche die Ressourcen an verschiedenen Orten zu finden
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        
        var searchPaths = new List<string>
        {
            Path.Combine(basePath, "wwwroot", "Resources"),
            Path.Combine(basePath, "Resources"),
            // Fallback für Entwicklung / Rider / dotnet run
            Path.Combine(basePath, "..", "..", "..", "ProjectAuditor.Gui", "Resources"),
            Path.Combine(basePath, "..", "..", "..", "..", "ProjectAuditor.Gui", "Resources")
        };

        string? resourcePath = null;
        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "Localization.*.json");
                if (files.Any())
                {
                    Console.WriteLine($"INFO: Lokalisierungsverzeichnis gefunden in: {path} mit {files.Length} Dateien.");
                    resourcePath = path;
                    break;
                }
            }
        }

        if (resourcePath == null)
        {
            Console.WriteLine("KRITISCH: Lokalisierungsverzeichnis wurde nirgendwo gefunden!");
            Console.WriteLine("Durchsuchte Pfade:");
            foreach (var path in searchPaths) Console.WriteLine($"  - {path}");
            return;
        }

        foreach (var lang in languages)
        {
            var filePath = Path.Combine(resourcePath, $"Localization.{lang}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var dict = new Dictionary<string, string>();
                    ParseJson(JsonDocument.Parse(json).RootElement, dict, "");
                    _translations[lang] = dict;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"KRITISCH: Fehler beim Parsen der Lokalisierung für {lang} in {filePath}: {ex.Message}");
                    if (ex.InnerException != null) Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    _translations[lang] = new Dictionary<string, string>();
                }
            }
            else
            {
                Console.WriteLine($"Lokalisierungsdatei nicht gefunden: {filePath}");
                _translations[lang] = new Dictionary<string, string>();
            }
        }
    }

    /// <summary>
    /// Parsed das JSON-Dokument in ein flaches Dictionary (mit Punkt-Notation für Pfade).
    /// </summary>
    private void ParseJson(JsonElement element, Dictionary<string, string> dict, string prefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                ParseJson(property.Value, dict, key);
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            dict[prefix] = element.GetString() ?? "";
        }
    }

    public void SetLanguage(string languageCode)
    {
        if (_translations.ContainsKey(languageCode))
        {
            _currentLanguage = languageCode;
        }
    }

    public string GetCurrentLanguage()
    {
        return _currentLanguage;
    }

    public string GetString(string key, string? defaultValue = null)
    {
        if (_translations.TryGetValue(_currentLanguage, out var translations))
        {
            if (translations.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Fallback auf Deutsch, falls die Sprache nicht vorhanden
        if (_translations.TryGetValue("de", out var deTranslations))
        {
            if (deTranslations.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return defaultValue ?? key;
    }

    public List<(string Code, string Name)> GetAvailableLanguages()
    {
        return new List<(string, string)>
        {
            ("de", "Deutsch"),
            ("en", "English"),
            ("es", "Español")
        };
    }
}
