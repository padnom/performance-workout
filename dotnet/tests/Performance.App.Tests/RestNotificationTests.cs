using Performance.Application;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.Tests;

public sealed class RestNotificationTests
{
    [Fact]
    public async Task BackgroundTimerCallback_DoesNotClearRestOrCancelNativeReminder()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var deadline = setup.State.Data.ActiveWorkout!.RestEnd!.Value;
        setup.Coordinator.Pause();
        setup.Clock.UtcNow = deadline.AddSeconds(1);

        await setup.Coordinator.CompleteForegroundAsync(deadline);
        await setup.Coordinator.SynchronizeAsync();

        setup.State.Data.ActiveWorkout.RestEnd.ShouldBe(deadline);
        setup.Scheduler.Scheduled.ShouldNotBeNull();
        setup.Feedback.Count.ShouldBe(0);
        await setup.Coordinator.SetEnabledAsync(false);
        setup.Scheduler.Scheduled.ShouldBeNull();
    }
    [Fact]
    public async Task PersistedRestCheck_RejectsDisableStopExtensionAndImportedOtherWorkout()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var data = setup.State.Data;
        var token = RestNotificationRegistration.From(data.ActiveWorkout)!.Token;
        RestNotificationRegistration.IsCurrent(data, token).ShouldBeTrue();
        RestNotificationRegistration.IsCurrent(data with { Settings = new Dictionary<string, string>() }, token).ShouldBeFalse();
        RestNotificationRegistration.IsCurrent(data with { ActiveWorkout = null }, token).ShouldBeFalse();
        RestNotificationRegistration.IsCurrent(data with { ActiveWorkout = data.ActiveWorkout! with { RestEnd = data.ActiveWorkout!.RestEnd!.Value.AddSeconds(30) } }, token).ShouldBeFalse();
        RestNotificationRegistration.IsCurrent(data with { ActiveWorkout = data.ActiveWorkout! with { StartedAt = data.ActiveWorkout!.StartedAt.AddDays(1) } }, token).ShouldBeFalse();
    }
    [Fact]
    public async Task DisabledForegroundCompletion_ClearsVisualTimerWithoutVibrating()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        var end = setup.State.Data.ActiveWorkout!.RestEnd!.Value;
        setup.Clock.UtcNow = end.AddSeconds(1);
        await setup.Coordinator.CompleteForegroundAsync(end);
        setup.State.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
        setup.Feedback.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ResumeAndPermissionChange_RearmAlarmEvenForSameDeadline()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var count = setup.Scheduler.Schedules;
        await setup.Coordinator.SynchronizeAsync();
        setup.Scheduler.Schedules.ShouldBe(count);
        await setup.Coordinator.ResumeAsync();
        setup.Scheduler.Schedules.ShouldBe(count + 1);
        setup.Scheduler.Access = new(true, false);
        await setup.Coordinator.SynchronizeAsync();
        setup.Scheduler.Schedules.ShouldBe(count + 2);
    }
    [Fact]
    public async Task DisabledByDefault_DoesNotRequestPermissionOrSchedule()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.InitializeAsync();
        setup.State.RestNotificationsEnabled.ShouldBeFalse();
        setup.Scheduler.Requests.ShouldBe(0);
        setup.Scheduler.Scheduled.ShouldBeNull();
    }

    [Fact]
    public async Task EnablingWithRefusedPermission_DoesNotPersistEnabled()
    {
        using var setup = new Setup();
        setup.Scheduler.Access = new(false, false);
        (await setup.Coordinator.SetEnabledAsync(true)).ShouldBeFalse();
        setup.State.RestNotificationsEnabled.ShouldBeFalse();
        setup.Scheduler.Scheduled.ShouldBeNull();
        setup.Scheduler.Requests.ShouldBe(1);
    }

    [Fact]
    public async Task ExtendAndStop_ReplaceThenCancelScheduledRest()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        (await setup.Coordinator.SetEnabledAsync(true)).ShouldBeTrue();
        var original = setup.Scheduler.Scheduled!;
        await setup.State.ExtendRestAsync();
        await setup.Coordinator.SynchronizeAsync();
        setup.Scheduler.Scheduled!.Token.ShouldNotBe(original.Token);
        setup.Scheduler.Scheduled.Deadline.ShouldBe(original.Deadline.AddSeconds(30));
        await setup.State.StopRestAsync();
        await setup.Coordinator.SynchronizeAsync();
        setup.Scheduler.Scheduled.ShouldBeNull();
    }

    [Fact]
    public async Task DisableDuringRest_CancelsAlarmAndPreservesDraft()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var active = setup.State.Data.ActiveWorkout;
        await setup.Coordinator.SetEnabledAsync(false);
        setup.Scheduler.Scheduled.ShouldBeNull();
        setup.State.Data.ActiveWorkout.ShouldBe(active);
    }

    [Fact]
    public async Task ForegroundCompletionAndResume_OnlyVibrateOnce()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var end = setup.State.Data.ActiveWorkout!.RestEnd!.Value;
        setup.Clock.UtcNow = end.AddSeconds(1);
        await Task.WhenAll(setup.Coordinator.CompleteForegroundAsync(end), setup.Coordinator.CompleteForegroundAsync(end));
        await setup.Coordinator.ResumeAsync();
        setup.Feedback.Count.ShouldBe(1);
        setup.State.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
    }

    [Fact]
    public async Task ReturningAfterBackgroundNotification_DoesNotVibrateAgain()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        var registration = RestNotificationRegistration.From(setup.State.Data.ActiveWorkout)!;
        setup.Scheduler.TryClaimCompletion(registration.Token).ShouldBeTrue();
        setup.Clock.UtcNow = registration.Deadline.AddSeconds(1);
        await setup.Coordinator.ResumeAsync();
        setup.Feedback.Count.ShouldBe(0);
        setup.State.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
    }

    [Fact]
    public async Task StaleCompletionAfterExtension_DoesNotClearOrVibrate()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        var old = setup.State.Data.ActiveWorkout!.RestEnd!.Value;
        await setup.State.ExtendRestAsync();
        setup.Clock.UtcNow = old.AddSeconds(1);
        await setup.Coordinator.CompleteForegroundAsync(old);
        setup.State.Data.ActiveWorkout!.RestEnd.ShouldBe(old.AddSeconds(30));
        setup.Feedback.Count.ShouldBe(0);
    }

    [Fact]
    public async Task FailedCompletionSave_PreservesRestAndAllowsRetry()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        var end = setup.State.Data.ActiveWorkout!.RestEnd!.Value;
        setup.Clock.UtcNow = end.AddSeconds(1);
        setup.Repository.Fail = true;
        await setup.Coordinator.CompleteForegroundAsync(end);
        setup.State.Data.ActiveWorkout!.RestEnd.ShouldBe(end);
        setup.Feedback.Count.ShouldBe(0);
        setup.Repository.Fail = false;
        await setup.Coordinator.CompleteForegroundAsync(end);
        setup.Feedback.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RevokedPermissionOnResume_CancelsRestAlertWithoutLosingWorkout()
    {
        using var setup = new Setup();
        await setup.StartRestAsync();
        await setup.Coordinator.SetEnabledAsync(true);
        setup.Scheduler.Access = new(false, false);
        await setup.Coordinator.ResumeAsync();
        setup.Scheduler.Scheduled.ShouldBeNull();
        setup.State.Data.ActiveWorkout.ShouldNotBeNull();
        setup.Coordinator.Access.NotificationsAllowed.ShouldBeFalse();
    }

    [Fact]
    public async Task FailedScheduler_RecordsErrorWithoutFailingWorkoutMutation()
    {
        using var setup = new Setup();
        setup.Scheduler.Fail = true;
        (await setup.State.StartAsync(setup.Catalog.Programs[0].Sessions[0], "One Air")).IsSuccess.ShouldBeTrue();
        await setup.Coordinator.SynchronizeAsync();
        setup.Diagnostics.Journal.Snapshot().ShouldContain(entry => entry.ErrorType == nameof(IOException));
    }

    [Fact]
    public void RestIdentity_ChangesAcrossSessionsAndExtensions()
    {
        var active = new ActiveWorkout { StartedAt = DateTimeOffset.UtcNow, RestEnd = DateTimeOffset.UtcNow.AddMinutes(2), RestExerciseId = "exercise" };
        var registration = RestNotificationRegistration.From(active)!;
        RestNotificationRegistration.From(active with { RestEnd = active.RestEnd!.Value.AddSeconds(30) })!.Token.ShouldNotBe(registration.Token);
        RestNotificationRegistration.From(active with { StartedAt = active.StartedAt.AddDays(1) })!.Token.ShouldNotBe(registration.Token);
    }

    private sealed class Setup : IDisposable
    {
        public TestClock Clock { get; } = new();
        public Repository Repository { get; } = new();
        public DefaultTrainingCatalog Catalog { get; } = new();
        public Scheduler Scheduler { get; } = new();
        public Feedback Feedback { get; } = new();
        public Diagnostics Diagnostics { get; }
        public WorkoutStateService State { get; }
        public RestNotificationCoordinator Coordinator { get; }
        public Setup()
        {
            Diagnostics = new(Clock);
            State = new(Repository, Catalog, Clock);
            Coordinator = new(State, Scheduler, Feedback, Clock, Diagnostics);
        }
        public async Task StartRestAsync()
        {
            var session = Catalog.Programs[0].Sessions.First(item => item.Exercises.Count > 0);
            await State.StartAsync(session, "One Air");
            await State.StartRestAsync(session.Exercises[0].ExerciseId);
        }
        public void Dispose() => Coordinator.Dispose();
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        public bool Fail { get; set; }
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("Simulated write failure.");
            _data = data; return ValueTask.CompletedTask;
        }
    }
    private sealed class Scheduler : IRestNotificationScheduler
    {
        public event Action<DateTimeOffset>? ForegroundRestElapsed { add { } remove { } }
        public bool IsForeground { get; set; } = true;
        public RestNotificationAccess Access { get; set; } = new(true, true);
        public RestNotificationRegistration? Scheduled { get; private set; }
        public int Requests { get; private set; }
        public int Schedules { get; private set; }
        public bool Fail { get; set; }
        private readonly HashSet<string> _claimed = [];
        public Task<RestNotificationAccess> GetAccessAsync() => Task.FromResult(Access);
        public Task<RestNotificationAccess> RequestAccessAsync() { Requests++; return Task.FromResult(Access); }
        public Task ScheduleAsync(RestNotificationRegistration registration) { if (Fail) throw new IOException(); Scheduled = registration; Schedules++; return Task.CompletedTask; }
        public Task CancelAsync() { if (Fail) throw new IOException(); Scheduled = null; return Task.CompletedTask; }
        public bool TryClaimCompletion(string token) => _claimed.Add(token);
    }
    private sealed class Feedback : IWorkoutFeedback
    {
        public int Count { get; private set; }
        public void NotifyRestCompleted() => Count++;
    }
    private sealed class Diagnostics(TimeProvider clock) : IAppDiagnostics
    {
        public DiagnosticJournal Journal { get; } = new(clock);
        public string Version => "1.1.0 (2)";
        public void Record(string operation, TimeSpan elapsed, Exception? exception = null) => Journal.Record(operation, elapsed, exception);
        public Task ShareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
