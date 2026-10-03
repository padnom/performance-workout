using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using Android.Views;
using Performance.App.Services;

namespace Performance.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private AndroidX.Activity.OnBackPressedCallback? _backCallback;

    internal void InstallBackHandler()
    {
        _backCallback?.Remove();
        _backCallback = new WorkoutBackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, _backCallback);
    }

    private async Task HandleWorkoutBackAsync()
    {
        try
        {
            var navigation = IPlatformApplication.Current?.Services.GetService<AppNavigationService>();
            if (navigation is not null && await navigation.RequestBackAsync()) return;
            MoveTaskToBack(true);
        }
        catch (Exception exception)
        {
            IPlatformApplication.Current?.Services.GetService<IAppDiagnostics>()?.Record("navigation.back", TimeSpan.Zero, exception);
        }
    }

    private sealed class WorkoutBackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed() => _ = activity.HandleWorkoutBackAsync();
    }

    public override bool DispatchKeyEvent(KeyEvent? keyEvent)
    {
        // The bundled BlazorWebView handles Back in OnKeyDown before the activity dispatcher.
        if (keyEvent?.KeyCode == Keycode.Back)
        {
            if (keyEvent.Action == KeyEventActions.Up && !keyEvent.IsCanceled) _ = HandleWorkoutBackAsync();
            return true;
        }
        return base.DispatchKeyEvent(keyEvent);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        OpenRequestedWorkout(Intent);
    }
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenRequestedWorkout(intent);
    }
    protected override void OnResume()
    {
        base.OnResume();
        var coordinator = IPlatformApplication.Current?.Services.GetService<RestNotificationCoordinator>();
        if (coordinator is not null) _ = coordinator.ResumeAsync();
    }
    protected override void OnPostResume()
    {
        base.OnPostResume();
        InstallBackHandler();
    }
    protected override void OnPause()
    {
        IPlatformApplication.Current?.Services.GetService<RestNotificationCoordinator>()?.Pause();
        if (IPlatformApplication.Current?.Services.GetService<IAppDiagnostics>() is MauiAppDiagnostics diagnostics) diagnostics.Flush();
        base.OnPause();
    }
    private static void OpenRequestedWorkout(Intent? intent)
    {
        if (intent?.Action != AndroidRestNotificationScheduler.OpenWorkoutAction) return;
        IPlatformApplication.Current?.Services.GetService<AppNavigationService>()?.OpenWorkout();
    }
}
