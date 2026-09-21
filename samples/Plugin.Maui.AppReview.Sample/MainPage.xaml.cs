using Plugin.Maui.AppReview;

namespace Plugin.Maui.AppReview.Sample;

public partial class MainPage : ContentPage
{
    readonly Label log = new() { LineBreakMode = LineBreakMode.WordWrap };

    public MainPage()
    {
        InitializeComponent();
        Root.Children.Add(new Label
        {
            Text = "After a successful action — RequestAsync is not a Rate us button.",
            FontAttributes = FontAttributes.Bold
        });
        Root.Children.Add(new Button { Text = "Complete a task", Command = new Command(async () => await CompleteTask()) });
        Root.Children.Add(new Label { Text = "Settings", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 0) });
        Root.Children.Add(new Button { Text = "Eligibility", Command = new Command(async () => await Show(AppReview.Current.GetEligibilityAsync())) });
        Root.Children.Add(new Button { Text = "Request review", Command = new Command(async () => await RequestOrOpenListing()) });
        Root.Children.Add(new Button { Text = "Open store listing", Command = new Command(async () => await Show(AppReview.Current.OpenStoreListingAsync())) });
        Root.Children.Add(new Button { Text = "Reset counters", Command = new Command(() => { AppReview.Current.ResetCounters(); log.Text = "reset"; }) });
        Root.Children.Add(log);
    }

    async Task CompleteTask()
    {
        log.Text = "task completed";
        var eligibility = await AppReview.Current.GetEligibilityAsync();
        if (eligibility.Kind != AppReviewKind.Shown)
        {
            log.Text = $"task completed — {eligibility.Kind} {eligibility.Message}";
            return;
        }

        await RequestOrOpenListing();
    }

    async Task RequestOrOpenListing()
    {
        var outcome = await AppReview.Current.RequestAsync();
        if (outcome.Kind is AppReviewKind.Unavailable or AppReviewKind.NotSupported)
        {
            var listing = await AppReview.Current.OpenStoreListingAsync();
            log.Text = $"{outcome.Kind} {outcome.Message} → listing {listing.Kind} {listing.Message}";
            return;
        }

        log.Text = $"{outcome.Kind} {outcome.Message}";
    }

    async Task Show(Task<AppReviewOutcome> task)
    {
        var outcome = await task;
        log.Text = $"{outcome.Kind} {outcome.Message}";
    }
}
