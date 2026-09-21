#if ANDROID
using Android.Content;
using Application = Android.App.Application;
using Google.Android.Play.Core.Review;
using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.AppReview;

sealed class PlatformAppReview : IAppReviewPlatform
{
    public static IAppReviewPlatform Create() => new PlatformAppReview();

    public async Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity is null)
                    return AppReviewOutcome.Of(AppReviewKind.Unavailable, "No current Android activity.");

                var manager = ReviewManagerFactory.Create(activity);
                var info = await AwaitPlayTask(manager.RequestReviewFlow(), cancellationToken).ConfigureAwait(true);
                if (info is not ReviewInfo reviewInfo)
                    return AppReviewOutcome.Of(AppReviewKind.Unavailable, "Play ReviewManager returned no ReviewInfo.");

                await AwaitPlayTask(manager.LaunchReviewFlow(activity, reviewInfo), cancellationToken).ConfigureAwait(true);
                return AppReviewOutcome.Of(AppReviewKind.Shown, "ReviewManager");
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return AppReviewOutcome.Of(AppReviewKind.Unavailable, ex.Message);
        }
    }

    public async Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        var package = options.AndroidPackageName ?? AppInfo.Current.PackageName;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var uri = Android.Net.Uri.Parse($"market://details?id={package}");
                var intent = new Intent(Intent.ActionView, uri);
                intent.AddFlags(ActivityFlags.NewTask);
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? Application.Context;
                context.StartActivity(intent);
            }).ConfigureAwait(false);
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

    static Task<Java.Lang.Object?> AwaitPlayTask(Android.Gms.Tasks.Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Java.Lang.Object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        task.AddOnSuccessListener(new SuccessListener(result => tcs.TrySetResult(result)));
        task.AddOnFailureListener(new FailureListener(error => tcs.TrySetException(error)));
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return tcs.Task;
    }

    sealed class SuccessListener(Action<Java.Lang.Object?> callback) : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener
    {
        public void OnSuccess(Java.Lang.Object? result) => callback(result);
    }

    sealed class FailureListener(Action<Exception> callback) : Java.Lang.Object, Android.Gms.Tasks.IOnFailureListener
    {
        public void OnFailure(Java.Lang.Exception error) => callback(error);
    }
}
#endif
