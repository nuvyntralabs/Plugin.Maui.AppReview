namespace Plugin.Maui.AppReview;

public enum AppReviewKind { Shown, NotEligible, Unavailable, Canceled, NotSupported }

public sealed class AppReviewOutcome
{
    public AppReviewKind Kind { get; init; }
    public string? Message { get; init; }
    public static AppReviewOutcome Of(AppReviewKind kind, string? message = null) => new() { Kind = kind, Message = message };
}

public sealed class AppReviewOptions
{
    public int MinimumLaunchCount { get; set; } = 5;
    public int MinimumDaysSinceFirstLaunch { get; set; } = 7;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromDays(90);
    public string? AndroidPackageName { get; set; }
    public string? iOSAppStoreId { get; set; }
    public string PreferencePrefix { get; set; } = "plugin.maui.appreview.";
}

public interface IAppReviewClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemReviewClock : IAppReviewClock
{
    public static SystemReviewClock Instance { get; } = new();
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface IAppReviewStore
{
    int LaunchCount { get; set; }
    DateTimeOffset? FirstLaunchUtc { get; set; }
    DateTimeOffset? LastPromptUtc { get; set; }
}

public sealed class MemoryAppReviewStore : IAppReviewStore
{
    public int LaunchCount { get; set; }
    public DateTimeOffset? FirstLaunchUtc { get; set; }
    public DateTimeOffset? LastPromptUtc { get; set; }
}

public interface IAppReviewPlatform
{
    Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken);
    Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken);
}

public interface IAppReview
{
    AppReviewOptions Options { get; }
    Task<AppReviewOutcome> GetEligibilityAsync(CancellationToken cancellationToken = default);
    Task<AppReviewOutcome> RequestAsync(CancellationToken cancellationToken = default);
    Task<AppReviewOutcome> OpenStoreListingAsync(CancellationToken cancellationToken = default);
    void RecordLaunch();
    void ResetCounters();
}

public static class AppReview
{
    static IAppReview? current;
    public static IAppReview Current =>
        current ?? throw new InvalidOperationException("AppReview is not initialized. Call builder.UseAppReview().");
    public static void SetDefault(IAppReview implementation) =>
        current = implementation ?? throw new ArgumentNullException(nameof(implementation));

    public static IAppReview Create(
        AppReviewOptions? options = null,
        IAppReviewStore? store = null,
        IAppReviewClock? clock = null,
        IAppReviewPlatform? platform = null)
    {
        var instance = new AppReviewImplementation(
            options ?? new AppReviewOptions(),
            store ?? new MemoryAppReviewStore(),
            clock ?? SystemReviewClock.Instance,
            platform ?? PlatformAppReview.Create());
        SetDefault(instance);
        return instance;
    }
}

public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder UseAppReview(this MauiAppBuilder builder, Action<AppReviewOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new AppReviewOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(AppReview.Create(options, new PreferencesAppReviewStore(options)));
        return builder;
    }
}

sealed class PreferencesAppReviewStore : IAppReviewStore
{
    readonly AppReviewOptions options;
    public PreferencesAppReviewStore(AppReviewOptions options) => this.options = options;
    string Key(string name) => options.PreferencePrefix + name;
    public int LaunchCount
    {
        get => Preferences.Default.Get(Key("launches"), 0);
        set => Preferences.Default.Set(Key("launches"), value);
    }
    public DateTimeOffset? FirstLaunchUtc
    {
        get => Read(Key("first"));
        set => Write(Key("first"), value);
    }
    public DateTimeOffset? LastPromptUtc
    {
        get => Read(Key("last"));
        set => Write(Key("last"), value);
    }
    static DateTimeOffset? Read(string key)
    {
        var raw = Preferences.Default.Get(key, "");
        return DateTimeOffset.TryParse(raw, out var value) ? value : null;
    }
    static void Write(string key, DateTimeOffset? value)
    {
        if (value is null) Preferences.Default.Remove(key);
        else Preferences.Default.Set(key, value.Value.ToString("O"));
    }
}

sealed class AppReviewImplementation : IAppReview
{
    readonly IAppReviewStore store;
    readonly IAppReviewClock clock;
    readonly IAppReviewPlatform platform;

    public AppReviewImplementation(AppReviewOptions options, IAppReviewStore store, IAppReviewClock clock, IAppReviewPlatform platform)
    {
        Options = options;
        this.store = store;
        this.clock = clock;
        this.platform = platform;
        RecordLaunch();
    }

    public AppReviewOptions Options { get; }

    public void RecordLaunch()
    {
        store.FirstLaunchUtc ??= clock.UtcNow;
        store.LaunchCount++;
    }

    public void ResetCounters()
    {
        store.LaunchCount = 0;
        store.FirstLaunchUtc = null;
        store.LastPromptUtc = null;
    }

    public Task<AppReviewOutcome> GetEligibilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Evaluate());

    public async Task<AppReviewOutcome> RequestAsync(CancellationToken cancellationToken = default)
    {
        var eligibility = Evaluate();
        if (eligibility.Kind != AppReviewKind.Shown)
            return eligibility;

        var outcome = await platform.RequestAsync(Options, cancellationToken).ConfigureAwait(false);
        if (outcome.Kind is AppReviewKind.Shown or AppReviewKind.Canceled)
            store.LastPromptUtc = clock.UtcNow;
        return outcome;
    }

    public Task<AppReviewOutcome> OpenStoreListingAsync(CancellationToken cancellationToken = default) =>
        platform.OpenStoreListingAsync(Options, cancellationToken);

    AppReviewOutcome Evaluate()
    {
        if (store.LaunchCount < Options.MinimumLaunchCount)
            return AppReviewOutcome.Of(AppReviewKind.NotEligible, $"launches {store.LaunchCount}/{Options.MinimumLaunchCount}");
        var first = store.FirstLaunchUtc ?? clock.UtcNow;
        if (clock.UtcNow - first < TimeSpan.FromDays(Options.MinimumDaysSinceFirstLaunch))
            return AppReviewOutcome.Of(AppReviewKind.NotEligible, "too soon after first launch");
        if (store.LastPromptUtc is { } last && clock.UtcNow - last < Options.Cooldown)
            return AppReviewOutcome.Of(AppReviewKind.NotEligible, "cooldown");
        return AppReviewOutcome.Of(AppReviewKind.Shown);
    }
}

#if !ANDROID && !IOS
sealed class PlatformAppReview : IAppReviewPlatform
{
    public static IAppReviewPlatform Create() => new PlatformAppReview();
    public Task<AppReviewOutcome> RequestAsync(AppReviewOptions options, CancellationToken cancellationToken) =>
        Task.FromResult(AppReviewOutcome.Of(AppReviewKind.NotSupported, "AppReview is not supported on this target."));
    public Task<AppReviewOutcome> OpenStoreListingAsync(AppReviewOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.iOSAppStoreId) && string.IsNullOrWhiteSpace(options.AndroidPackageName))
            return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.Unavailable, "Store id is missing."));
        return Task.FromResult(AppReviewOutcome.Of(AppReviewKind.NotSupported));
    }
}
#endif
