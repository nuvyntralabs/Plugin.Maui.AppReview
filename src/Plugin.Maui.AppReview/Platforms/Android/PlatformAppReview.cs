#if ANDROID
using Android.Content;
using Application = Android.App.Application;

namespace Plugin.Maui.AppReview;

sealed class PlatformAppReview : IAppReviewPlatform
{
    public static IAppReviewPlatform Create() => new PlatformAppReview();

    public Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        // Play Core ReviewManager is optional. 1.0 opens the listing when in-app review is unavailable (sideload / emulator).
        return OpenStoreListingAsync(options, cancellationToken);
    }

    public async Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        var package = options.AndroidPackageName ?? AppInfo.Current.PackageName;
        try
        {
            var uri = Android.Net.Uri.Parse($"market://details?id={package}");
            var intent = new Intent(Intent.ActionView, uri);
            intent.AddFlags(ActivityFlags.NewTask);
            var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? Application.Context;
            context.StartActivity(intent);
            return AppReviewOutcome.Of(AppReviewKind.Shown, "store listing");
        }
        catch
        {
            try
            {
                await Browser.Default.OpenAsync($"https://play.google.com/store/apps/details?id={package}", BrowserLaunchMode.SystemPreferred).ConfigureAwait(false);
                return AppReviewOutcome.Of(AppReviewKind.Shown, "https listing");
            }
            catch (Exception ex)
            {
                return AppReviewOutcome.Of(AppReviewKind.Unavailable, ex.Message);
            }
        }
    }
}
#endif
