using WgpuMaui.Editor.Application.Abstract.Services;
using WgpuMaui.Editor.Application.Common.Extensions;

namespace WgpuMaui.Editor.Components.PageModels;
public partial class MainLayoutModel(IBrowserStorageService storage) : ObservableObject
{

    [ObservableProperty]
    public partial string Language { get; set; } = "pt-BR";
    [ObservableProperty]
    public partial string CurrentTheme { get; set; } = "royal";
    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;
    [ObservableProperty]
    public partial bool IsDarkMode { get; set; } = true;
    [ObservableProperty]
    public partial bool DrawerOpen { get; set; } = false;
    [ObservableProperty]
    public partial MudThemeProvider MudThemeProvider { get; set; } = default!;
    [ObservableProperty]
    public partial MudTheme Theme { get; set; } = Editor.Theme.AppTheme.RoyalBlueTheme;

    [RelayCommand]
    public void DrawerToggle() => DrawerOpen = !DrawerOpen;

    [RelayCommand]
    public async Task ThemeMenuItemClickedAsync(string theme)
    {
        (CurrentTheme, storage.Settings.Theme, Theme) = (theme, theme, theme == "royal" ? Editor.Theme.AppTheme.RoyalBlueTheme : Editor.Theme.AppTheme.SkyBlueTheme);
        Theme = theme == "royal" ? Editor.Theme.AppTheme.RoyalBlueTheme : Editor.Theme.AppTheme.SkyBlueTheme;
        await storage.UpdateThemeAsync(theme);
    }

    [RelayCommand]
    public async Task LanguageMenuItemClickedAsync(string language)
    {
        Language = language ??= "pt-BR";
        await storage.UpdateLanguageAsync(language);
        new System.Globalization.CultureInfo(Language).SetCurrentCulture(Language);
    }

    [RelayCommand]
    public async Task ToggleDarkModeAsync()
    {
        IsDarkMode = !IsDarkMode;
        await storage.UpdateIsDarkMode(IsDarkMode);
    } 
}
