using ProjectAuditor.Core.Models;
using System.Threading.Tasks;

namespace ProjectAuditor.Core.Services;

/// <summary>
/// Service responsible for loading and saving the application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the current application settings.
    /// </summary>
    /// <returns>A loaded instance of <see cref="ProjectAuditorSettings"/>.</returns>
    Task<ProjectAuditorSettings> LoadSettingsAsync();

    /// <summary>
    /// Saves the provided application settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    Task SaveSettingsAsync(ProjectAuditorSettings settings);
}
