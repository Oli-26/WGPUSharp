using Microsoft.Extensions.Localization;
using WgpuMaui.Editor.Application.Abstract.Services;

namespace WgpuMaui.Editor.Infrastructure.Concrete.Services;


/// <summary>
/// Localizer instantiation implementation
/// </summary>
/// <param name="factory">
/// <see cref="IStringLocalizerFactory"/> factory instance.
/// </param>
public sealed class Localizer(IStringLocalizerFactory factory) : ILocalizer
{
    private readonly IStringLocalizer _localizer = factory.Create(
            "Localization",
            typeof(Localizer).Assembly.FullName!
        );

    public string this[string key] => _localizer[key];
}
