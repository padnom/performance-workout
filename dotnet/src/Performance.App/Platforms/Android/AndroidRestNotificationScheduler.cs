using System.Text.Json;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Performance.Application;

namespace Performance.App.Services;

public sealed class AndroidRestNotificationScheduler : IRestNotificationScheduler
{
    internal const string OpenWorkoutAction = "fr.padnom.performance.OPEN_WORKOUT";
    internal const string TokenExtra = "rest-token";
    private const string RegistrationKey = "rest-notification-registration";
    private const string HandledKey = "rest-notification-handled";
    private const string ChannelId = "rest-completed-v1";
    private const int NotificationId = 2107;
    private static readonly object Gate = new();
    private static bool _foreground;
    private static event Action<DateTimeOffset>? RestElapsed;
    private static Context Context => global::Android.App.Application.Context;
    private static AlarmManager Alarms => (AlarmManager)Context.GetSystemService(Context.AlarmService)!;
    private static NotificationManager Notifications => (NotificationManager)Context.GetSystemService(Context.NotificationService)!;

    public event Action<DateTimeOffset>? ForegroundRestElapsed { add => RestElapsed += value; remove => RestElapsed -= value; }
    public bool IsForeground { get => Volatile.Read(ref _foreground); set => Volatile.Write(ref _foreground, value); }

    public Task<RestNotificationAccess> GetAccessAsync()
    {
        var allowed = !OperatingSystem.IsAndroidVersionAtLeast(33) ||
            Context.CheckSelfPermission("android.permission.POST_NOTIFICATIONS") == global::Android.Content.PM.Permission.Granted;
        if (OperatingSystem.IsAndroidVersionAtLeast(24)) allowed &= Notifications.AreNotificationsEnabled();
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            allowed &= Notifications.GetNotificationChannel(ChannelId)?.Importance != NotificationImportance.None;
        var precise = !OperatingSystem.IsAndroidVersionAtLeast(31) || Alarms.CanScheduleExactAlarms();
        return Task.FromResult(new RestNotificationAccess(allowed, precise));
    }

    public async Task<RestNotificationAccess> RequestAccessAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<RestNotificationPermission>());
        var access = await GetAccessAsync();
        if (OperatingSystem.IsAndroidVersionAtLeast(31) && access.NotificationsAllowed && !access.PreciseAlarmsAllowed)
        {
            var intent = new Intent(Settings.ActionRequestScheduleExactAlarm,
                global::Android.Net.Uri.Parse($"package:{Context.PackageName}"));
            intent.AddFlags(ActivityFlags.NewTask);
            await MainThread.InvokeOnMainThreadAsync(() => Context.StartActivity(intent));
        }
        return await GetAccessAsync();
    }

    public async Task ScheduleAsync(RestNotificationRegistration registration)
    {
        var access = await GetAccessAsync();
        if (!access.NotificationsAllowed) { await CancelAsync(); return; }
        lock (Gate)
        {
            EnsureChannel();
            Preferences.Default.Set(RegistrationKey, JsonSerializer.Serialize(registration));
            Notifications.Cancel(NotificationId);
            var pending = PendingIntent.GetBroadcast(Context, NotificationId,
                new Intent(Context, typeof(RestAlarmReceiver)).PutExtra(TokenExtra, registration.Token),
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
            var delay = Math.Max(0, (registration.Deadline - DateTimeOffset.UtcNow).TotalMilliseconds);
            var trigger = SystemClock.ElapsedRealtime() + (long)delay;
            try
            {
                if (access.PreciseAlarmsAllowed) Alarms.SetExactAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, trigger, pending);
                else Alarms.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, trigger, pending);
            }
            catch (Java.Lang.SecurityException)
            {
                Alarms.SetAndAllowWhileIdle(AlarmType.ElapsedRealtimeWakeup, trigger, pending);
            }
        }
    }

    public Task CancelAsync()
    {
        lock (Gate)
        {
            Preferences.Default.Remove(RegistrationKey);
            var pending = PendingIntent.GetBroadcast(Context, NotificationId, new Intent(Context, typeof(RestAlarmReceiver)),
                PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
            if (pending is not null) Alarms.Cancel(pending);
            Notifications.Cancel(NotificationId);
        }
        return Task.CompletedTask;
    }

    public bool TryClaimCompletion(string token)
    {
        lock (Gate) return Claim(token);
    }

    internal static void Receive(string? token)
    {
        lock (Gate)
        {
            var registration = ReadRegistration();
            if (registration is null || registration.Token != token || registration.Deadline > DateTimeOffset.UtcNow) return;
            if (Volatile.Read(ref _foreground) && RestElapsed is not null)
            {
                RestElapsed.Invoke(registration.Deadline);
                return;
            }
            if (!IsCurrentPersistedRest(registration.Token) || !Claim(registration.Token)) return;
            EnsureChannel();
            var open = new Intent(Context, typeof(MainActivity)).SetAction(OpenWorkoutAction)
                .AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var tap = PendingIntent.GetActivity(Context, NotificationId, open,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
                ? new Notification.Builder(Context, ChannelId) : new Notification.Builder(Context);
            builder.SetSmallIcon(Resource.Drawable.ic_rest_notification)
                .SetContentTitle("Repos terminé")
                .SetContentText("Tu peux reprendre ta prochaine série.")
                .SetContentIntent(tap).SetAutoCancel(true)
                .SetCategory(Notification.CategoryReminder).SetVisibility(NotificationVisibility.Private);
            if (!OperatingSystem.IsAndroidVersionAtLeast(26)) builder.SetVibrate([0, 200]);
            try { Notifications.Notify(NotificationId, builder.Build()); }
            catch (Java.Lang.SecurityException) { }
        }
    }

    private static bool Claim(string token)
    {
        if (Preferences.Default.Get(HandledKey, "") == token) return false;
        Preferences.Default.Set(HandledKey, token);
        return true;
    }

    private static bool IsCurrentPersistedRest(string token)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "performance-data.json");
            var data = JsonSerializer.Deserialize<PerformanceData>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return RestNotificationRegistration.IsCurrent(data, token);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    private static RestNotificationRegistration? ReadRegistration()
    {
        try { return JsonSerializer.Deserialize<RestNotificationRegistration>(Preferences.Default.Get(RegistrationKey, "null")); }
        catch (JsonException) { return null; }
    }

    private static void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var channel = new NotificationChannel(ChannelId, "Fin du repos", NotificationImportance.Default)
        { Description = "Alertes optionnelles à la fin du repos entre les séries." };
        channel.EnableVibration(true);
        channel.SetVibrationPattern([0, 200]);
        channel.SetSound(null, null);
        Notifications.CreateNotificationChannel(channel);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class RestAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent) =>
        AndroidRestNotificationScheduler.Receive(intent?.GetStringExtra(AndroidRestNotificationScheduler.TokenExtra));
}

public sealed class RestNotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [("android.permission.POST_NOTIFICATIONS", true)];
}
