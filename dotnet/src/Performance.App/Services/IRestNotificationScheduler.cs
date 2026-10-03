using System.Security.Cryptography;
using System.Text;
using Performance.Domain;
using Performance.Application;

namespace Performance.App.Services;

public sealed record RestNotificationAccess(bool NotificationsAllowed, bool PreciseAlarmsAllowed)
{
    public string Description => !NotificationsAllowed ? "Les notifications sont bloquées dans les réglages Android."
        : !PreciseAlarmsAllowed ? "Notifications autorisées. Sans l’autorisation Alarmes et rappels, Android peut retarder l’alerte."
        : "Notifications autorisées, y compris lorsque l’écran est éteint.";
}

public sealed record RestNotificationRegistration(string Token, DateTimeOffset Deadline)
{
    public static bool IsCurrent(PerformanceData? data, string? token) => data is not null && token is not null &&
        bool.TryParse(data.Settings.GetValueOrDefault("training-rest-notifications-enabled"), out var enabled) && enabled && From(data.ActiveWorkout)?.Token == token;

    public static RestNotificationRegistration? From(ActiveWorkout? active)
    {
        if (active?.RestEnd is not { } deadline || active.RestExerciseId is not { } exerciseId) return null;
        var value = $"{active.StartedAt.UtcTicks}:{exerciseId}:{deadline.UtcTicks}";
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))), deadline);
    }
}

public interface IRestNotificationScheduler
{
    event Action<DateTimeOffset>? ForegroundRestElapsed;
    bool IsForeground { get; set; }
    Task<RestNotificationAccess> GetAccessAsync();
    Task<RestNotificationAccess> RequestAccessAsync();
    Task ScheduleAsync(RestNotificationRegistration registration);
    Task CancelAsync();
    bool TryClaimCompletion(string token);
}
