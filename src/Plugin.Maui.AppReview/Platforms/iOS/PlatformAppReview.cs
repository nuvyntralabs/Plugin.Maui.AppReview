#if IOS
using StoreKit;
using UIKit;

namespace Plugin.Maui.AppReview;

sealed class PlatformAppReview : IAppReviewPlatform
{
    public static IAppReviewPlatform Create() => new PlatformAppReview();

    public Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            {
                var scene = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>().FirstOrDefault();
                if (scene is not null)
                    SKStoreReviewController.RequestReview(scene);
                else
                    SKStoreReviewController.RequestReview();
            }
            else
            {
                SKStoreReviewController.RequestReview();
            }
        });
        return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.Shown, "SKStoreReviewController"));
    }

    public async Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.iOSAppStoreId))
            return AppReviewOutcome.Of(AppReviewKind.Unavailable, "iOSAppStoreId is required.");
        var url = $"itms-apps://itunes.apple.com/app/id{options.iOSAppStoreId}?action=write-review";
        await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred).ConfigureAwait(false);
        return AppReviewOutcome.Of(AppReviewKind.Shown, "App Store listing");
    }
}
#endif
