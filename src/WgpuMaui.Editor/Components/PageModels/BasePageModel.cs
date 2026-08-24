namespace WgpuMaui.Editor.Components.PageModels;

public partial class BasePageModel : ObservableObject
{
    [ObservableProperty] public partial bool IsBusy { get; set; } = false;
    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
}
