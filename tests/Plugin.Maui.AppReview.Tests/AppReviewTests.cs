using Plugin.Maui.AppReview;

namespace Plugin.Maui.AppReview.Tests;

sealed class FakeClock : IAppReviewClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

sealed class FakePlatform : IAppReviewPlatform
{
    public AppReviewKind RequestKind { get; set; } = AppReviewKind.Shown;
    public int Requests { get; private set; }
    public Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(AppReviewOutcome.Of(RequestKind));
    }
    public Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.iOSAppStoreId) && string.IsNullOrWhiteSpace(options.AndroidPackageName))
            return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.Unavailable, "missing id"));
        return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.Shown));
    }
}

public sealed class AppReviewTests
{
    static (AppReviewImplementation Api, MemoryAppReviewStore Store, FakeClock Clock, FakePlatform Platform) Create()
    {
        var options = new AppReviewOptions { MinimumLaunchCount = 3, MinimumDaysSinceFirstLaunch = 2, Cooldown = TimeSpan.FromDays(10) };
        var store = new MemoryAppReviewStore();
        var clock = new FakeClock();
        var platform = new FakePlatform();
        var api = new AppReviewImplementation(options, store, clock, platform);
        return (api, store, clock, platform);
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
    public async Task Missing_store_id_fails_open_listing()
    {
        var api = new AppReviewImplementation(new AppReviewOptions(), new MemoryAppReviewStore(), new FakeClock(), new FakePlatform());
        var outcome = await api.OpenStoreListingAsync();
        Assert.Equal(AppReviewKind.Unavailable, outcome.Kind);
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
    public async Task Net10_request_is_not_supported()
    {
        var api = AppReview.Create(new AppReviewOptions { MinimumLaunchCount = 0, MinimumDaysSinceFirstLaunch = 0 });
        api.ResetCounters();
        api.RecordLaunch();
        var outcome = await api.RequestAsync();
        Assert.Equal(AppReviewKind.NotSupported, outcome.Kind);
    }
}
