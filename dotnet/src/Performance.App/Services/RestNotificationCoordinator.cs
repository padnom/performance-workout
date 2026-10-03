using System.Diagnostics;
using Performance.Application;
using Performance.Domain;

namespace Performance.App.Services;

public sealed class RestNotificationCoordinator : IDisposable
{
    private readonly WorkoutStateService _state;
    private readonly IRestNotificationScheduler _scheduler;
    private readonly IWorkoutFeedback _feedback;
    private readonly TimeProvider _clock;
    private readonly IAppDiagnostics _diagnostics;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private bool _disposed;
    private string? _scheduledToken;
    private bool _scheduledPrecisely;
    public event Action? Changed;
    public RestNotificationAccess Access { get; private set; } = new(false, false);

    public RestNotificationCoordinator(WorkoutStateService state, IRestNotificationScheduler scheduler,
        IWorkoutFeedback feedback, TimeProvider clock, IAppDiagnostics diagnostics)
    {
        (_state, _scheduler, _feedback, _clock, _diagnostics) = (state, scheduler, feedback, clock, diagnostics);
        _state.DataChanged += DataChanged;
        _scheduler.ForegroundRestElapsed += ForegroundElapsed;
    }

    public async Task InitializeAsync()
    {
        try { await _state.InitializeAsync(); await SynchronizeAsync(); }
        catch (Exception exception) { _diagnostics.Record("state.initialize", TimeSpan.Zero, exception); }
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            Access = await _scheduler.RequestAccessAsync();
            if (!Access.NotificationsAllowed) { Changed?.Invoke(); return false; }
        }
        var result = await _state.SetRestNotificationsEnabledAsync(enabled);
        if (!result.IsSuccess) return false;
        await SynchronizeAsync();
        return true;
    }

    public async Task ResumeAsync()
    {
        _scheduler.IsForeground = true;
        await _state.InitializeAsync();
        await _sync.WaitAsync();
        try { _scheduledToken = null; }
        finally { _sync.Release(); }
        var registration = RestNotificationRegistration.From(_state.Data.ActiveWorkout);
        if (registration?.Deadline <= _clock.GetUtcNow()) await CompleteForegroundAsync(registration.Deadline);
        await SynchronizeAsync();
    }

    public void Pause() => _scheduler.IsForeground = false;

    public async Task SynchronizeAsync()
    {
        await _sync.WaitAsync();
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (_disposed) return;
            Access = await _scheduler.GetAccessAsync();
            var registration = RestNotificationRegistration.From(_state.Data.ActiveWorkout);
            if (_state.RestNotificationsEnabled && Access.NotificationsAllowed && registration?.Deadline > _clock.GetUtcNow())
            {
                if (_scheduledToken != registration.Token || _scheduledPrecisely != Access.PreciseAlarmsAllowed)
                {
                    await _scheduler.ScheduleAsync(registration);
                    _scheduledToken = registration.Token;
                    _scheduledPrecisely = Access.PreciseAlarmsAllowed;
                }
            }
            else if (_scheduler.IsForeground || !_state.RestNotificationsEnabled || !Access.NotificationsAllowed || registration is null ||
                (_scheduledToken is not null && _scheduledToken != registration.Token))
            {
                await _scheduler.CancelAsync(); _scheduledToken = null;
            }
            // A current expired rest in the background remains owned by the native reminder.
            Changed?.Invoke();
        }
        catch (Exception exception) { _diagnostics.Record("rest.synchronize", Stopwatch.GetElapsedTime(started), exception); }
        finally { _sync.Release(); }
    }

    public async Task CompleteForegroundAsync(DateTimeOffset deadline)
    {
        // Blazor timers can continue while the screen is off; native feedback owns that completion.
        if (!_scheduler.IsForeground) return;
        var registration = RestNotificationRegistration.From(_state.Data.ActiveWorkout);
        if (registration is null || registration.Deadline != deadline || deadline > _clock.GetUtcNow()) return;
        var result = await _state.CompleteRestAsync(deadline);
        if (!result.IsSuccess) return;
        if (_state.RestNotificationsEnabled && _scheduler.TryClaimCompletion(registration.Token)) _feedback.NotifyRestCompleted();
    }

    private void DataChanged(PerformanceData data) => _ = SynchronizeAsync();
    private void ForegroundElapsed(DateTimeOffset deadline) => _ = CompleteForegroundSafelyAsync(deadline);
    private async Task CompleteForegroundSafelyAsync(DateTimeOffset deadline)
    {
        try { await CompleteForegroundAsync(deadline); }
        catch (Exception exception) { _diagnostics.Record("rest.complete", TimeSpan.Zero, exception); }
    }
    public void Dispose()
    {
        _disposed = true;
        _state.DataChanged -= DataChanged;
        _scheduler.ForegroundRestElapsed -= ForegroundElapsed;
    }
}
