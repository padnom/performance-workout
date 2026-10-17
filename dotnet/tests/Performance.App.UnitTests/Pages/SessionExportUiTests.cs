using Bunit;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Components;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(SessionHistory))]
public sealed class SessionExportUiTests : IDisposable
{
    private const string LoggedWeight = "73,5";
    private readonly BunitContext _context = new();
    private readonly Files _files = new();
    private readonly JsonSessionPackageCodec _codec = new();
    private readonly WorkoutStateService _state;

    public SessionExportUiTests()
    {
        _state = new(new Repository(), new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton(new AppNavigationService());
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
        _context.Services.AddSingleton<IProgramFiles>(_files);
        _context.Services.AddSingleton<ISessionPackageCodec>(_codec);
        _context.Services.AddTransient<ExportWorkoutSessionUseCase>();
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public async Task History_ExportFreeWorkout_SharesStructureWithoutLoggedValues()
    {
        // Arrange
        var exercise = _state.AllExercises.First();
        await SeedHistoryAsync(exercise.Id);
        var component = _context.Render<SessionHistory>();

        // Act
        component.Find(".export-session").Click();

        // Assert
        var package = _codec.Decode(_files.SharedSessionJson!);
        package.Session.Exercises.Single().ExerciseId.ShouldBe(exercise.Id);
        package.Session.Exercises.Single().Sets.ShouldBe(2);
        _files.SharedSessionJson!.ShouldNotContain(LoggedWeight);
        _files.SharedSessionJson!.ToLowerInvariant().ShouldNotContain("weight");
        component.Find("p.notice[role=status]").TextContent.ShouldBe("Séance prête à partager.");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public async Task History_ExportFreeWorkout_NamesSessionWithDate()
    {
        // Arrange
        await SeedHistoryAsync(_state.AllExercises.First().Id);
        var component = _context.Render<SessionHistory>();

        // Act
        component.Find(".export-session").Click();

        // Assert
        _files.SharedSessionName.ShouldStartWith("Séance libre · ");
        _files.SharedSessionName.ShouldBe(_codec.Decode(_files.SharedSessionJson!).Session.Name);
    }

    [Fact]
    [Trait("AC", "AC-41")]
    public async Task History_ShareText_SharesFormattedSession()
    {
        // Arrange
        var exercise = _state.AllExercises.First();
        await SeedHistoryAsync(exercise.Id);
        var component = _context.Render<SessionHistory>();

        // Act
        component.Find(".share-session-text").Click();

        // Assert
        _files.SharedText!.ShouldContain(exercise.Name);
        _files.SharedText.ShouldNotContain(LoggedWeight);
        _files.SharedTextTitle!.ShouldStartWith("Séance libre · ");
        _files.SharedSessionJson.ShouldBeNull();
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public async Task History_ExportWhenNothingShareable_ShowsFormatMessage()
    {
        // Arrange
        await _state.ReplaceDataAsync(new PerformanceData { History = [new TrainingSessionLog { Id = "empty", SessionId = "free", Date = DateTimeOffset.UtcNow,
            Exercises = [new ExercisePerformance { ExerciseId = "unknown-exercise", Sets = [new TrainingSet { Reps = "5" }] }] }] });
        var component = _context.Render<SessionHistory>();

        // Act
        component.Find(".export-session").Click();

        // Assert
        component.Find("p.notice").TextContent.ShouldBe("Cette séance ne contient aucun exercice partageable.");
        _files.SharedSessionJson.ShouldBeNull();
    }

    [Fact]
    [Trait("AC", "AC-42")]
    public void SessionStart_PreviewExport_SharesResolvedSession()
    {
        // Arrange
        var session = _state.GetRecommendedProgram("One Air")!.Sessions[0];
        var starter = _context.Render<SessionStart>();

        // Act
        starter.InvokeAsync(() => starter.Instance.Select(session));
        starter.Find(".export-pending-session").Click();

        // Assert
        var package = _codec.Decode(_files.SharedSessionJson!);
        package.Session.Exercises.Select(plan => plan.ExerciseId)
            .ShouldBe(_state.ResolveSessionForGym(session, "One Air").Exercises.Select(plan => plan.ExerciseId));
        starter.Find(".confirm-dialog p.notice").TextContent.ShouldBe("Séance prête à partager.");
    }

    [Fact]
    [Trait("AC", "AC-42")]
    public void SessionStart_PreviewTextShare_SharesFormattedSession()
    {
        // Arrange
        var session = _state.GetRecommendedProgram("One Air")!.Sessions[0];
        var starter = _context.Render<SessionStart>();

        // Act
        starter.InvokeAsync(() => starter.Instance.Select(session));
        starter.Find(".share-pending-session-text").Click();

        // Assert
        _files.SharedText!.ShouldContain("Performance · ");
        _files.SharedSessionJson.ShouldBeNull();
    }

    [Fact]
    [Trait("AC", "AC-42")]
    public void SessionStart_ConfirmOnly_ShowsExportButtons()
    {
        // Arrange
        var session = _state.GetRecommendedProgram("One Air")!.Sessions[0];
        var starter = _context.Render<SessionStart>();

        // Act
        starter.InvokeAsync(() => starter.Instance.Select(session, confirmOnly: true));

        // Assert
        starter.Find(".confirm-dialog").ClassList.ShouldNotContain("start-preview");
        starter.FindAll(".export-pending-session").Count.ShouldBe(1);
        starter.FindAll(".share-pending-session-text").Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-42")]
    public void SessionStart_FreeWorkout_ShowsNoExportButton()
    {
        // Arrange
        var starter = _context.Render<SessionStart>();

        // Act
        starter.InvokeAsync(() => starter.Instance.Select(new WorkoutSession { Id = "free", Name = "Séance libre" }, confirmOnly: true));

        // Assert
        starter.FindAll("[role=dialog]").Count.ShouldBe(1);
        starter.FindAll(".export-pending-session").ShouldBeEmpty();
        starter.FindAll(".share-pending-session-text").ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-46")]
    public async Task SessionStart_SessionWithOnlyBasicFitPlans_DefaultsToBasicFit()
    {
        // Arrange
        await _state.InitializeAsync();
        (await _state.CreateSessionAsync(DraftSession("Basic-Fit"), "Basic-Fit")).Error.ShouldBeNull();
        var saved = _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Single();
        var starter = _context.Render<SessionStart>();

        // Act
        await starter.InvokeAsync(() => starter.Instance.Select(saved, confirmOnly: true));
        starter.Find(".primary-button").Click();

        // Assert
        starter.WaitForAssertion(() => _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit"));
    }

    [Fact]
    [Trait("AC", "AC-46")]
    public async Task SessionStart_SessionWithOnlyOneAirPlans_DefaultsToOneAirEvenWhenCurrentGymDiffers()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.ReplaceDataAsync(new PerformanceData { Settings = new Dictionary<string, string> { ["workoutGym"] = "Basic-Fit" } });
        (await _state.CreateSessionAsync(DraftSession("One Air"), "One Air")).Error.ShouldBeNull();
        var saved = _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Single();
        var starter = _context.Render<SessionStart>();

        // Act
        await starter.InvokeAsync(() => starter.Instance.Select(saved, confirmOnly: true));
        starter.Find(".primary-button").Click();

        // Assert
        starter.WaitForAssertion(() => _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("One Air"));
    }

    [Fact]
    [Trait("AC", "AC-46")]
    public async Task SessionStart_SessionOutsideMySessionsWithOnlyOneAirPlans_KeepsCurrentGym()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.ReplaceDataAsync(new PerformanceData { Settings = new Dictionary<string, string> { ["workoutGym"] = "Basic-Fit" } });
        var session = DraftSession("One Air") with { Id = "outside", Exercises = DraftSession("One Air").Exercises.Select(plan => plan with { Gym = "One Air" }).ToArray() };
        var starter = _context.Render<SessionStart>();

        // Act
        await starter.InvokeAsync(() => starter.Instance.Select(session, confirmOnly: true));
        starter.Find(".primary-button").Click();

        // Assert
        starter.WaitForAssertion(() => _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit"));
    }

    private WorkoutSession DraftSession(string gym) => new()
    {
        Id = "draft", Name = "Ma séance",
        Exercises = _state.AllExercises.Where(exercise => ProgramDraft.Compatible(exercise, gym))
            .Take(2).Select((exercise, index) => new WorkoutExercise { ExerciseId = exercise.Id, Order = index, Sets = 3, RepTarget = "10", Rest = "1:30" }).ToArray()
    };

    private Task SeedHistoryAsync(string exerciseId) => _state.ReplaceDataAsync(new PerformanceData
    {
        History = [new TrainingSessionLog
        {
            Id = "log", SessionId = "free", DurationMinutes = 30, Date = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
            Exercises = [new ExercisePerformance { ExerciseId = exerciseId, Gym = "One Air", Sets =
                [new TrainingSet { Weight = LoggedWeight, Reps = "8" }, new TrainingSet { Weight = LoggedWeight, Reps = "7" }] }]
        }]
    });

    public void Dispose() => _context.Dispose();

    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }

    private sealed class Files : IProgramFiles
    {
        public string? SharedSessionJson { get; private set; }
        public string? SharedSessionName { get; private set; }
        public string? SharedText { get; private set; }
        public string? SharedTextTitle { get; private set; }
        public Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> PickSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareSessionAsync(string json, string name, CancellationToken cancellationToken = default) { SharedSessionJson = json; SharedSessionName = name; return Task.CompletedTask; }
        public Task ShareTextAsync(string text, string title, CancellationToken cancellationToken = default) { SharedText = text; SharedTextTitle = title; return Task.CompletedTask; }
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}
