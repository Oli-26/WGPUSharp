using WgpuMaui.Core;
using WgpuMaui.Editor.Application.Abstract.Services;

namespace WgpuMaui.Editor.Components.PageModels;

public partial class CubePageModel : BasePageModel
{
    public readonly ILocalizer Localizer;
    [ObservableProperty]
    public partial GpuLoop? Loop { get; set; }
    public CubePageModel(ILocalizer localizer)
    {
        Localizer = localizer;
        Title = Localizer["CubePageTitle"];
    }
}
