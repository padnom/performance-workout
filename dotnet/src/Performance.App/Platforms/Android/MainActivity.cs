using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.OS;
using Android.Views;
using Performance.App.Services;

namespace Performance.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault],
    DataMimeTypes = ["application/json", "application/octet-stream"])]
[IntentFilter([Intent.ActionSend], Categories = [Intent.CategoryDefault],
    DataMimeTypes = ["application/json", "application/octet-stream"])]
public class MainActivity : MauiAppCompatActivity
{
    private AndroidX.Activity.OnBackPressedCallback? _backCallback;
    private IncomingShare? _pendingShare;

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
        if (savedInstanceState is null) ReadIncomingShare(Intent);
    }
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        OpenRequestedWorkout(intent);
        ReadIncomingShare(intent);
    }
    protected override void OnResume()
    {
        base.OnResume();
        var coordinator = IPlatformApplication.Current?.Services.GetService<RestNotificationCoordinator>();
        if (coordinator is not null) _ = coordinator.ResumeAsync();
        OfferPendingShare();
        var updates = IPlatformApplication.Current?.Services.GetService<AppUpdateService>();
        if (updates is not null) _ = updates.ResumeAsync();
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

    private void OfferPendingShare()
    {
        var share = _pendingShare;
        var service = IPlatformApplication.Current?.Services.GetService<IncomingShareService>();
        if (share is null || service is null) return;
        _pendingShare = null;
        service.Offer(share);
    }

    private void ReadIncomingShare(Intent? intent)
    {
        if (intent is null || intent.Flags.HasFlag(ActivityFlags.LaunchedFromHistory)) return;
        var uri = ResolveSharedUri(intent);
        if (uri?.Scheme != Android.Content.ContentResolver.SchemeContent) return;
        _ = ReadAndOfferAsync(uri);
    }

    private static Android.Net.Uri? ResolveSharedUri(Intent? intent) => intent?.Action switch
    {
        Intent.ActionView => intent.Data,
        Intent.ActionSend => OperatingSystem.IsAndroidVersionAtLeast(33)
            ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri
            : GetLegacyStream(intent),
        _ => null
    };

#pragma warning disable CA1422
    private static Android.Net.Uri? GetLegacyStream(Intent intent) => intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri;
#pragma warning restore CA1422

    private async Task ReadAndOfferAsync(Android.Net.Uri uri)
    {
        var share = await ReadShareAsync(uri);
        var service = IPlatformApplication.Current?.Services.GetService<IncomingShareService>();
        if (service is not null) service.Offer(share);
        else _pendingShare = share;
    }

    private async Task<IncomingShare> ReadShareAsync(Android.Net.Uri uri)
    {
        try
        {
            await using var stream = ContentResolver?.OpenInputStream(uri) ?? throw new IOException("No stream");
            var json = await Task.Run(() => BoundedTextReader.ReadAsync(stream, 1_100_000));
            return new IncomingShare(json, null);
        }
        catch (Exception exception)
        {
            IPlatformApplication.Current?.Services.GetService<IAppDiagnostics>()?.Record("share.incoming", TimeSpan.Zero, exception);
            return new IncomingShare(null, "Impossible de lire ce fichier.");
        }
    }
}
