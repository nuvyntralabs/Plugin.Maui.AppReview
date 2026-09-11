using Microsoft.Extensions.Logging;
using Plugin.Maui.AppReview;

namespace Plugin.Maui.AppReview.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.Services.AddSingleton<MainPage>();
        builder.UseMauiApp<App>()
            .UseAppReview(o => { o.MinimumLaunchCount = 1; o.MinimumDaysSinceFirstLaunch = 0; o.Cooldown = TimeSpan.FromSeconds(30); o.iOSAppStoreId = "1234567890"; });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
