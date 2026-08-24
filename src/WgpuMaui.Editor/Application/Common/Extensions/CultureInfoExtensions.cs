namespace WgpuMaui.Editor.Application.Common.Extensions;
using System.Globalization;
public static class CultureInfoExtensions
{
    public static string[] SupportedCultures { get => ["pt-BR", "en-US"]; }
    public static CultureInfo[]? CurrentCultures { get => [CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture, CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.DefaultThreadCurrentCulture!]; }
    extension(CultureInfo ci)
    {
        public string[] SupportedCultures { get => ["pt-BR", "en-US"]; }
        public string DefaultCulture => "pt-BR";
        internal void SetCurrentCulture(string userCulture)
        {

            CultureInfo culture = CultureInfo.DefaultThreadCurrentCulture ?? CultureInfo.DefaultThreadCurrentUICulture ?? CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
            if (SupportedCultures.Any(sc => culture.Name.Contains(sc, StringComparison.OrdinalIgnoreCase)))
                culture = CurrentCultures?.FirstOrDefault(x => x.Name == SupportedCultures.FirstOrDefault(sc => culture.Name.Contains(sc, StringComparison.OrdinalIgnoreCase)))!;
            else if (SupportedCultures.Any(sc => userCulture.Contains(sc, StringComparison.OrdinalIgnoreCase)))
                culture  = new (userCulture);
            CultureInfo info = new(userCulture);
            CultureInfo.DefaultThreadCurrentCulture = info;
            CultureInfo.DefaultThreadCurrentUICulture = info;
            CultureInfo.CurrentCulture = info;
            CultureInfo.CurrentUICulture = info;
        }
    }
}
