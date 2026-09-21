using Plugin.Maui.AppReview;

namespace Plugin.Maui.AppReview.Tests;

sealed class FakeClock : IAppReviewClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

sealed class FakePlatform : IAppReviewPlatform
{
    public AppReviewKind RequestKind { get; set; } = AppReviewKind.Shown;
    public AppReviewKind ListingKind { get; set; } = AppReviewKind.Shown;
    public int Requests { get; private set; }
    public int Listings { get; private set; }
    public AppReviewOptions? LastOptions { get; private set; }

    public Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        LastOptions = options;
        return Task.FromResult(AppReviewOutcome.Of(RequestKind, RequestKind.ToString()));
    }

    public Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Listings++;
        LastOptions = options;
        if (string.IsNullOrWhiteSpace(options.iOSAppStoreId) && string.IsNullOrWhiteSpace(options.AndroidPackageName))
            return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.Unavailable, "missing id"));
        return Task.FromResult(AppReviewOutcome.Of(ListingKind, "listing"));
    }
}

public sealed class AppReviewTests
{
    static (AppReviewImplementation Api, MemoryAppReviewStore Store, FakeClock Clock, FakePlatform Platform) Create(
        Action<AppReviewOptions>? configure = null)
    {
        var options = new AppReviewOptions { MinimumLaunchCount = 3, MinimumDaysSinceFirstLaunch = 2, Cooldown = TimeSpan.FromDays(10) };
        configure?.Invoke(options);
        var store = new MemoryAppReviewStore();
        var clock = new FakeClock();
        var platform = new FakePlatform();
        var api = new AppReviewImplementation(options, store, clock, platform);
        return (api, store, clock, platform);
    }

    static void MakeEligible(MemoryAppReviewStore store, FakeClock clock, int launches = 3)
    {
        store.LaunchCount = launches;
        store.FirstLaunchUtc = clock.UtcNow;
        clock.UtcNow += TimeSpan.FromDays(3);
    }

    [Fact]
    public async Task Not_eligible_until_launches_and_days()
    {
        var (api, store, clock, platform) = Create();
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
        store.LaunchCount = 3;
        store.FirstLaunchUtc = clock.UtcNow;
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
        clock.UtcNow += TimeSpan.FromDays(3);
        Assert.Equal(AppReviewKind.Shown, (await api.GetEligibilityAsync()).Kind);
        var shown = await api.RequestAsync();
        Assert.Equal(AppReviewKind.Shown, shown.Kind);
        Assert.Equal(1, platform.Requests);
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
    }

    [Fact]
    public async Task Ineligible_request_does_not_call_platform()
    {
        var (api, _, _, platform) = Create();
        var outcome = await api.RequestAsync();
        Assert.Equal(AppReviewKind.NotEligible, outcome.Kind);
        Assert.Equal(0, platform.Requests);
    }

    [Fact]
    public async Task Cooldown_blocks_until_window_elapses()
    {
        var (api, store, clock, platform) = Create();
        MakeEligible(store, clock);
        Assert.Equal(AppReviewKind.Shown, (await api.RequestAsync()).Kind);
        Assert.Equal(1, platform.Requests);
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
        clock.UtcNow += TimeSpan.FromDays(9);
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
        clock.UtcNow += TimeSpan.FromDays(2);
        Assert.Equal(AppReviewKind.Shown, (await api.GetEligibilityAsync()).Kind);
    }

    [Fact]
    public async Task Canceled_prompt_still_starts_cooldown()
    {
        var (api, store, clock, platform) = Create();
        platform.RequestKind = AppReviewKind.Canceled;
        MakeEligible(store, clock);
        Assert.Equal(AppReviewKind.Canceled, (await api.RequestAsync()).Kind);
        Assert.NotNull(store.LastPromptUtc);
        Assert.Equal(AppReviewKind.NotEligible, (await api.GetEligibilityAsync()).Kind);
    }

    [Fact]
    public async Task Unavailable_does_not_start_cooldown()
    {
        var (api, store, clock, platform) = Create();
        platform.RequestKind = AppReviewKind.Unavailable;
        MakeEligible(store, clock);
        Assert.Equal(AppReviewKind.Unavailable, (await api.RequestAsync()).Kind);
        Assert.Null(store.LastPromptUtc);
        Assert.Equal(AppReviewKind.Shown, (await api.GetEligibilityAsync()).Kind);
        Assert.Equal(1, platform.Requests);
    }

    [Fact]
    public async Task Missing_store_id_fails_open_listing()
    {
        var api = new AppReviewImplementation(new AppReviewOptions(), new MemoryAppReviewStore(), new FakeClock(), new FakePlatform());
        var outcome = await api.OpenStoreListingAsync();
        Assert.Equal(AppReviewKind.Unavailable, outcome.Kind);
    }

    [Fact]
    public async Task Listing_opens_when_ios_or_android_id_is_set()
    {
        var ios = new AppReviewImplementation(
            new AppReviewOptions { iOSAppStoreId = "1234567890" },
            new MemoryAppReviewStore(), new FakeClock(), new FakePlatform());
        Assert.Equal(AppReviewKind.Shown, (await ios.OpenStoreListingAsync()).Kind);

        var android = new AppReviewImplementation(
            new AppReviewOptions { AndroidPackageName = "com.example.app" },
            new MemoryAppReviewStore(), new FakeClock(), new FakePlatform());
        Assert.Equal(AppReviewKind.Shown, (await android.OpenStoreListingAsync()).Kind);
    }

    [Fact]
    public async Task Reset_clears_cooldown()
    {
        var (api, store, clock, _) = Create();
        api.ResetCounters();
        store.FirstLaunchUtc = clock.UtcNow;
        store.LaunchCount = 5;
        clock.UtcNow += TimeSpan.FromDays(30);
        Assert.Equal(AppReviewKind.Shown, (await api.GetEligibilityAsync()).Kind);
    }

    [Fact]
    public void RecordLaunch_increments_and_stamps_first_launch()
    {
        var store = new MemoryAppReviewStore();
        var clock = new FakeClock();
        var api = new AppReviewImplementation(new AppReviewOptions(), store, clock, new FakePlatform());
        Assert.Equal(1, store.LaunchCount);
        Assert.Equal(clock.UtcNow, store.FirstLaunchUtc);
        api.RecordLaunch();
        Assert.Equal(2, store.LaunchCount);
        Assert.Equal(clock.UtcNow, store.FirstLaunchUtc);
    }

    [Fact]
    public async Task Net10_request_is_not_supported()
    {
        var api = AppReview.Create(new AppReviewOptions { MinimumLaunchCount = 0, MinimumDaysSinceFirstLaunch = 0 });
        api.ResetCounters();
        api.RecordLaunch();
        var outcome = await api.RequestAsync();
        Assert.Equal(AppReviewKind.NotSupported, outcome.Kind);
        Assert.Same(api, AppReview.Current);
    }

    [Fact]
    public async Task Net10_listing_requires_store_id_then_is_not_supported()
    {
        var missing = AppReview.Create(new AppReviewOptions());
        Assert.Equal(AppReviewKind.Unavailable, (await missing.OpenStoreListingAsync()).Kind);

        var withId = AppReview.Create(new AppReviewOptions { iOSAppStoreId = "1", AndroidPackageName = "com.example" });
        Assert.Equal(AppReviewKind.NotSupported, (await withId.OpenStoreListingAsync()).Kind);
    }

    [Fact]
    public void Current_requires_initialization()
    {
        AppReview.SetDefault(new AppReviewImplementation(new AppReviewOptions(), new MemoryAppReviewStore(), new FakeClock(), new FakePlatform()));
        Assert.NotNull(AppReview.Current);
        Assert.Throws<ArgumentNullException>(() => AppReview.SetDefault(null!));
    }

    [Fact]
    public async Task Request_honors_cancellation()
    {
        var (api, store, clock, _) = Create();
        MakeEligible(store, clock);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => api.RequestAsync(cts.Token));
    }
}
