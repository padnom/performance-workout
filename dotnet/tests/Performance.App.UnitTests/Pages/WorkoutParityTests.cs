using Performance.App.UnitTests.Setup;
using JetBrains.Annotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(Workout))]
public sealed class WorkoutParityTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;
    private readonly FakeRepository _repository = new();
    private readonly Exercise _exercise = new DefaultTrainingCatalog().Exercises.First();
    private readonly FakeClock _clock = new();
    private readonly FakeWorkoutFeedback _feedback = new();

    public WorkoutParityTests()
    {
        _state = new WorkoutStateService(_repository, new DefaultTrainingCatalog(), _clock);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton<TimeProvider>(_clock);
        _context.Services.AddSingleton<IWorkoutFeedback>(_feedback);
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/seance");
    }

    [Fact]
    public async Task Workout_ToolbarPrecedesCardsAndRetainsRestActions()
    {
        await StartAsync();
        await _state.SetRestDurationAsync(_exercise.Id, 90);
        await _state.StartRestAsync(_exercise.Id);
        var page = _context.Render<Workout>();
        page.Find(".workout-page > :first-child").ClassList.ShouldContain("workout-toolbar");
        page.Find(".workout-toolbar .workout-elapsed").TextContent.ShouldBe("0:00");
        page.Find(".workout-toolbar .rest-banner strong").TextContent.ShouldContain("1:30");
        page.Find(".workout-toolbar [aria-label='Ajouter 30 secondes de repos']").Click();
        page.WaitForAssertion(() => page.Find(".rest-banner strong").TextContent.ShouldContain("2:00"));
        page.Find(".workout-toolbar [aria-label='Arrêter le repos']").Click();
        page.WaitForAssertion(() => page.FindAll(".rest-banner").ShouldBeEmpty());
        page.Find(".workout-toolbar .finish-workout").Click();
        page.Find("[role=dialog]").TextContent.ShouldContain("Terminer la séance ?");
    }

    [Fact]
    public void Home_ConfirmedStart_ShowsTotalTimeImmediately()
    {
        // Arrange
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/");
        var page = _context.Render<RouteHarness>();
        page.Find(".free-workout-card").Click();
        page.FindAll(".workout-clock").ShouldBeEmpty();

        // Act
        page.Find("[role=dialog] .primary-button").Click();

        // Assert
        page.Find(".workout-clock").TextContent.ShouldContain("Temps total");
        page.Find(".workout-elapsed").TextContent.ShouldBe("0:00");
    }

    [Fact]
    public async Task Home_ReopenedWorkout_ShowsElapsedTimeFromOriginalStart()
    {
        // Arrange
        await StartAsync();
        var startedAt = _state.Data.ActiveWorkout!.StartedAt;
        _clock.Advance(1025);

        // Act
        var page = _context.Render<Workout>();

        // Assert
        page.Find(".workout-elapsed").TextContent.ShouldBe("17:05");
        _state.Data.ActiveWorkout.StartedAt.ShouldBe(startedAt);
    }

    [Fact]
    public async Task Home_SetEntry_HidesRirAndPreservesPreviouslyStoredValue()
    {
        // Arrange
        await StartAsync();
        await _state.UpdateSetAsync(_exercise.Id, 0, new TrainingSet { Weight = "40", Reps = "8", Rir = "2" });
        var page = _context.Render<Workout>();

        // Assert
        page.FindAll("input[aria-label^='RIR']").ShouldBeEmpty();
        page.Find(".set-labels").TextContent.ShouldNotContain("RIR");

        // Act
        page.Find("input[aria-label^='Charge série 1']").Change("45");
        page.Find("input[aria-label^='Répétitions série 1']").Input("10");

        // Assert
        _state.GetSets(_exercise.Id)[0].Weight.ShouldBe("45");
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("10");
        _state.GetSets(_exercise.Id)[0].Rir.ShouldBe("2");
    }

    [Fact]
    public async Task Home_EnteringRepetitions_StartsVisibleRestWithoutManualButton()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<Workout>();

        // Act
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");

        // Assert
        page.WaitForAssertion(() => page.FindAll(".rest-banner").Count.ShouldBe(1));
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("8");
    }

    [Fact]
    public async Task Home_RecentHistory_ClosesWithoutFinishingOrChangingDraft()
    {
        // Arrange
        await _state.ReplaceDataAsync(new PerformanceData { History = [Log("One Air", "42,5")] });
        await StartAsync();
        await _state.UpdateSetAsync(_exercise.Id, 0, new TrainingSet { Weight = "50", Reps = "8" });
        var page = _context.Render<Workout>();

        // Act
        page.Find("button[aria-label='Consulter l’historique sans quitter la séance']").Click();

        // Assert
        page.Find(".workout-history-dialog").TextContent.ShouldContain("42,5");
        page.Find(".workout-history-dialog").TextContent.ShouldContain("One Air");
        page.Find("button[aria-label='Fermer l’historique']").Click();
        page.FindAll(".workout-history-dialog").ShouldBeEmpty();
        _state.GetSets(_exercise.Id)[0].Weight.ShouldBe("50");
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("8");
        _state.Data.History.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Exercise_Record_ChangesWithGymWithoutMixingPerformances()
    {
        // Arrange
        await _state.ReplaceDataAsync(new PerformanceData { History = [Log("One Air", "42,5"), Log("Basic-Fit", "70")] });
        await StartAsync();
        var page = _context.Render<Workout>();

        // Assert
        page.Find(".gym-record strong").TextContent.ShouldBe("42,5 kg");

        // Act
        page.Find(".gym-history select").Change("Basic-Fit");

        // Assert
        page.Find(".gym-record strong").TextContent.ShouldBe("70 kg");
        page.Find(".gym-history details").TextContent.ShouldContain("70 kg");
        page.Find(".gym-history details").TextContent.ShouldNotContain("42,5 kg");
    }

    private Task<WorkoutResult> StartAsync() => _state.StartAsync(new WorkoutSession
    {
        Id = "free", Name = "Séance libre",
        Exercises = [new WorkoutExercise { ExerciseId = _exercise.Id, Sets = 4 }]
    }, "One Air");

    [Fact]
    public async Task Home_ConfiguredRest_CanExtendStopAndResumeAfterPageRecreation()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find("select[aria-label^='Durée de repos']").Change("90");
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");
        page.Find(".rest-banner strong").TextContent.ShouldContain("1:30");

        // Act
        page.Find("button[aria-label='Ajouter 30 secondes de repos']").Click();
        _clock.Advance(10);
        page.Dispose();
        page = _context.Render<Workout>();

        // Assert
        page.Find(".rest-banner strong").TextContent.ShouldContain("1:50");
        page.Find("select[aria-label^='Durée de repos']").GetAttribute("value").ShouldBe("90");
        page.Find("button[aria-label='Arrêter le repos']").Click();
        page.FindAll(".rest-banner, .mini-rest-timer").ShouldBeEmpty();
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("8");
    }

    [Fact]
    public async Task Home_ExpiredRest_ClearsTimerAndSignalsCompletionOnce()
    {
        // Arrange
        await StartAsync();
        await _state.SetRestNotificationsEnabledAsync(true);
        await _state.SetRestDurationAsync(_exercise.Id, 60);
        var page = _context.Render<Workout>();
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");

        // Act
        _clock.Advance(61);

        // Assert
        page.WaitForAssertion(() =>
        {
            page.FindAll(".rest-banner, .mini-rest-timer").ShouldBeEmpty();
            page.Markup.ShouldContain("Repos terminé");
            _feedback.Notifications.ShouldBe(1);
        }, TimeSpan.FromSeconds(3));
        page.Dispose();
        _context.Render<Workout>();
        _feedback.Notifications.ShouldBe(1);
    }

    [Fact]
    public async Task Home_FailedSetSave_DoesNotStartRestOrLosePreviousSet()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<Workout>();
        _repository.FailSaves = true;

        // Act
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");

        // Assert
        page.FindAll(".rest-banner, .mini-rest-timer").ShouldBeEmpty();
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("");
        page.Markup.ShouldContain("L’enregistrement a échoué");
    }

    [Fact]
    public async Task Workout_ExpiredRest_WithDefaultDisabledSetting_DoesNotVibrate()
    {
        await StartAsync();
        await _state.SetRestDurationAsync(_exercise.Id, 60);
        var page = _context.Render<Workout>();
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");

        _clock.Advance(61);

        page.WaitForAssertion(() =>
        {
            page.FindAll(".rest-banner, .mini-rest-timer").ShouldBeEmpty();
            page.Markup.ShouldContain("Repos terminé");
        }, TimeSpan.FromSeconds(3));
        _feedback.Notifications.ShouldBe(0);
    }

    [Fact]
    public async Task Home_ClockTick_DoesNotRerenderWholeWorkout()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<ObservedHome>();
        var renders = page.Instance.OwnRenders;

        // Act
        _clock.Advance(2);

        // Assert
        page.WaitForAssertion(() => page.Find(".workout-elapsed").TextContent.ShouldBe("0:02"), TimeSpan.FromSeconds(3));
        page.Instance.OwnRenders.ShouldBe(renders);
    }

    [Fact]
    public async Task Home_DelayedWrite_KeepsInputEditableAndPersistsLatestDigits()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<Workout>();
        _repository.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var input = page.Find("input[aria-label^='Répétitions série 1']");

        // Act
        var first = input.InputAsync("1");
        Task second = Task.CompletedTask;
        try
        {
            // Assert
            page.Find("input[aria-label^='Répétitions série 1']").HasAttribute("disabled").ShouldBeFalse();
            second = input.InputAsync("12");
        }
        finally
        {
            _repository.WriteGate.SetResult();
            await Task.WhenAll(first, second);
        }
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("12");
    }

    [Fact]
    public async Task Home_ConcurrentFieldEdits_PreserveWeightAndRepetitions()
    {
        // Arrange
        await StartAsync();
        var page = _context.Render<Workout>();
        _repository.WriteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var weight = page.Find("input[aria-label^='Charge série 1']").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "50" });
        var reps = page.Find("input[aria-label^='Répétitions série 1']").InputAsync("12");
        _repository.WriteGate.SetResult();
        await Task.WhenAll(weight, reps);

        // Assert
        _state.GetSets(_exercise.Id)[0].Weight.ShouldBe("50");
        _state.GetSets(_exercise.Id)[0].Reps.ShouldBe("12");
    }

    private TrainingSessionLog Log(string gym, string weight) => new()
    {
        Id = gym, SessionId = "free", Date = DateTimeOffset.Parse("2026-10-01T12:00:00Z"), DurationMinutes = 30,
        Exercises = [new ExercisePerformance { ExerciseId = _exercise.Id, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = "8" }] }]
    };

    public void Dispose() => _context.Dispose();

    public sealed class ObservedHome : Workout
    {
        public int OwnRenders { get; private set; }
        protected override void OnAfterRender(bool firstRender) => OwnRenders++;
    }

    private sealed class FakeWorkoutFeedback : IWorkoutFeedback
    {
        public int Notifications { get; private set; }
        public void NotifyRestCompleted() => Notifications++;
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public bool FailSaves { get; set; }
        public TaskCompletionSource? WriteGate { get; set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (WriteGate is { } gate) await gate.Task.WaitAsync(cancellationToken);
            if (FailSaves) throw new IOException("Test write failure");
            _data = data;
        }
    }
}
