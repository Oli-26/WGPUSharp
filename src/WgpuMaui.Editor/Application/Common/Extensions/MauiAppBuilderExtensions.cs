using MudBlazor.Services;
using WgpuMaui.Editor.Application.Abstract.Services;
using WgpuMaui.Editor.Components.PageModels;
using WgpuMaui.Editor.Infrastructure.Concrete.Services;
namespace WgpuMaui.Editor.Application.Common;
public static class MauiAppBuilderExtensions
{
    extension(MauiAppBuilder builder)
    {
        public MauiAppBuilder AddEditor()
        {
            builder.Services.AddMudServices();
            builder.Services.AddScoped<HttpClient>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.AddSingleton<ILocalizer, Localizer>();
            builder.Services.AddSingleton<IBrowserStorageService, BrowserStorageService>();
            return builder;
        }

        public MauiAppBuilder AddPageModels()
        {
            builder.Services.AddTransient<BasePageModel>();
            builder.Services.AddTransient<MainLayoutModel>();
            builder.Services.AddTransient<CubePageModel>();
            return builder;
        }
    }
}
