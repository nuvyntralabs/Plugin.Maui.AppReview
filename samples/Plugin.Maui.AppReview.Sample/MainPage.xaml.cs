using Plugin.Maui.AppReview;

namespace Plugin.Maui.AppReview.Sample;

public partial class MainPage : ContentPage
{
    readonly Label log = new();

    public MainPage()
    {
        InitializeComponent();
        Root.Children.Add(new Button { Text = "Eligibility", Command = new Command(async () => await Show(AppReview.Current.GetEligibilityAsync())) });
        Root.Children.Add(new Button { Text = "Request review", Command = new Command(async () => await Show(AppReview.Current.RequestAsync())) });
        Root.Children.Add(new Button { Text = "Open store listing", Command = new Command(async () => await Show(AppReview.Current.OpenStoreListingAsync())) });
        Root.Children.Add(new Button { Text = "Reset counters", Command = new Command(() => { AppReview.Current.ResetCounters(); log.Text = "reset"; }) });
        Root.Children.Add(log);
    }

    async Task Show(Task<AppReviewOutcome> task)
    {
        var outcome = await task;
        log.Text = $"{outcome.Kind} {outcome.Message}";
    }
}
