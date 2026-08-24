using Microsoft.JSInterop;

namespace WgpuMaui.Editor.Infrastructure.Concrete.Services;

/// <summary>
/// Implementation of a <see cref="IBrowserStorageService"/> abstraction service.
/// </summary>
/// <param name="js"><see cref="IJSRuntime"/> instance</param>
public sealed class BrowserStorageService(IJSRuntime js) : Application.Abstract.Services.IBrowserStorageService
{
    private const string StorageKey = "wm_user_settings";

    /// <inheritdoc />
    public Domain.Models.UserSettings Settings { get; private set; } = new();

    /// <inheritdoc />
    public event Action? OnSettingsChanged;

    /// <inheritdoc />
    public async Task<string> GetCurrentLanguageAsync(bool hard = false)
    {
        if (hard)
        {
            await ReadAsync();
        }
        return Settings.Language ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<bool> GetIsDarkModeAsync(bool hard = false)
    {
        if (hard)
        {
            await ReadAsync();
        }
        return Settings.DarkMode;
    }

    /// <inheritdoc />
    public async Task<string> GetCurrentThemeAsync(bool hard = false)
    {
        if (hard)
        {
            await ReadAsync();
        }
        return Settings.Theme ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await ReadAsync();

        // If theme was not manually defined, gets it from browser
        if (string.IsNullOrEmpty(Settings.Theme))
        {
            Settings.Theme = "royal";
        }

        await SaveToStorageAsync();

        // If Locale was not manually defined, gets it from browser
        if (string.IsNullOrEmpty(Settings.Language))
        {
            string? browserLang = null;
            try
            {
                browserLang = await js.InvokeAsync<string?>("eval", "navigator.language || navigator.userLanguage");

                if (!string.IsNullOrWhiteSpace(browserLang))
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(browserLang.Trim());
                    Settings.Language = culture.Name; // Salva o nome padronizado (ex: "pt-BR" ou "en-US")
                }
            }
            catch (System.Globalization.CultureNotFoundException cnf)
            {
                if (browserLang != null && browserLang.Length >= 2)
                {
                    try
                    {
                        var shortLang = browserLang.Substring(0, 2);
                        var culture = System.Globalization.CultureInfo.GetCultureInfo(shortLang);
                        Settings.Language = culture.Name;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                        Settings.Language = "pt-BR"; // Fallback se falhar novamente
                        throw;
                    }
                }
                else
                {
                    Console.Error.WriteLine(cnf);
                    Settings.Language = "pt-BR"; // Fallback padrão caso a string seja curta/inválida
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Fallback de segurança para qualquer outro erro de interoperabilidade JS
                Console.Error.WriteLine(ex);
                Settings.Language = "pt-BR";
            }
        }
    }

    private async Task ReadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                Settings = System.Text.Json.JsonSerializer.Deserialize<Domain.Models.UserSettings>(json) ?? new();
            }
            catch
            {
                Settings = new();
                await SaveToStorageAsync();
            }
        }

    }

    /// <inheritdoc />
    public async Task UpdateIsDarkMode(bool isDarkMode)
    {
        Settings.DarkMode = isDarkMode;
        await SaveToStorageAsync();
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public async Task UpdateLanguageAsync(string language)
    {
        Settings.Language = language;
        await SaveToStorageAsync();
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public async Task UpdateThemeAsync(string theme)
    {
        Settings.Theme = theme;
        await SaveToStorageAsync();
        NotifyStateChanged();
    }

    private async Task SaveToStorageAsync()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Settings);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    private void NotifyStateChanged() => OnSettingsChanged?.Invoke();
}