using Performance.App.Services;

namespace Performance.App;

public partial class MainPage : ContentPage
{
    private readonly RestNotificationCoordinator _notifications;
    private readonly AppNavigationService _navigation;
    private readonly IAppDiagnostics _diagnostics;
    public MainPage(RestNotificationCoordinator notifications, AppNavigationService navigation, IAppDiagnostics diagnostics)
    {
        (_notifications, _navigation, _diagnostics) = (notifications, navigation, diagnostics);
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _notifications.InitializeAsync(); await _notifications.ResumeAsync(); }
        catch (Exception exception) { _diagnostics.Record("lifecycle.appear", TimeSpan.Zero, exception); }
    }

    private void OnBlazorWebViewInitialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs args)
    {
        args.WebView.ClearCache(true);
        args.WebView.Settings.CacheMode = Android.Webkit.CacheModes.NoCache;
        if (Platform.CurrentActivity is MainActivity activity) activity.InstallBackHandler();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = HandleBackAsync();
        return true;
    }

    private async Task HandleBackAsync()
    {
        try
        {
            if (await _navigation.RequestBackAsync()) return;
            Platform.CurrentActivity?.MoveTaskToBack(true);
        }
        catch (Exception exception) { _diagnostics.Record("navigation.back", TimeSpan.Zero, exception); }
    }
}
