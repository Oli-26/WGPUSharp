namespace WgpuMaui.Editor.Application.Abstract.Services;

/// <summary>
/// Abstraction of a simple browser local storage service
/// </summary>
public interface IBrowserStorageService
{

    /// <summary>
    /// Retrieves the current theme mode saved in browser settings
    /// </summary>
    /// <returns>a string representing the theme: e.g. : 'light', 'dark'</returns>
    Task<bool> GetIsDarkModeAsync(bool hard = false);

    /// <summary>
    /// Retrieves the current theme saved in browser settings
    /// </summary>
    /// <returns>a string representing the theme: e.g. : 'light', 'dark'</returns>
    Task<string> GetCurrentThemeAsync(bool hard = false);

    /// <summary>
    /// Retrieves the language stored in browser settings
    /// </summary>
    /// <returns>string representing language of pages</returns>
    Task<string> GetCurrentLanguageAsync(bool hard = false);

    /// <summary>
    /// Init the settings searching
    /// from localstorage LocalStorage or detecting system preferences
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Notification of UI 
    /// </summary>
    event Action? OnSettingsChanged;
    
    /// <summary>
    /// Stored object of settings
    /// </summary>
    Domain.Models.UserSettings Settings { get; }
    
    /// <summary>
    /// Sets or Updates if theme is dark o light mode.
    /// </summary>
    /// <param name="isDarkMode">bool indicating if it dark mode.</param>
    Task UpdateIsDarkMode(bool isDarkMode);

    /// <summary>
    /// Sets or updates a user defined locale.
    /// </summary>
    /// <param name="language">a 5 digit culture string</param>
    Task UpdateLanguageAsync(string language);
    /// <summary>
    /// Sets or updates a user defined theme.
    /// </summary>
    /// <param name="theme">a theme mark string of desired theme (e.g. 'royal', 'sky').</param>
    Task UpdateThemeAsync(string theme);
}