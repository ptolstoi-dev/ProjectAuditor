using ProjectAuditor.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProjectAuditor.Core.Services;

/// <summary>
/// Implementation of <see cref="ISettingsService"/> that saves settings to a local JSON file
/// in the user's Application Data folder.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService()
    {
        // Store settings in %APPDATA%\ProjectAuditor\settings.json
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataFolder, "ProjectAuditor");
        
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<ProjectAuditorSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new ProjectAuditorSettings(); // Return defaults if file doesn't exist
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<ProjectAuditorSettings>(json, _jsonOptions) ?? new ProjectAuditorSettings();
        }
        catch (Exception ex)
        {
            // Fallback to defaults on error
            // Consider injecting an ILogger to log "Failed to load settings"
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return new ProjectAuditorSettings();
        }
    }

    public async Task SaveSettingsAsync(ProjectAuditorSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }
}
