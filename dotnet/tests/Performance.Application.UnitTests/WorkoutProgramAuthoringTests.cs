using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutProgramAuthoringTests
{
    [Fact]
    public async Task CreateProgramAsync_ShouldCloneDraftAndIgnoreIncomingIdentities()
    {
        var repository = new Repository();
        var service = Create(repository);
        var exercises = new List<WorkoutExercise> { Plan("press") with { Order = 48 }, Plan("abs") with { Order = -3 } };
        var sessions = new List<WorkoutSession> { Session with { Id = "repeated", Exercises = exercises }, Session with { Id = "repeated" } };
        var draft = Draft with { Id = "builtin", Name = "  Personal  ", Sessions = sessions };

        (await service.CreateProgramAsync(draft)).IsSuccess.ShouldBeTrue();

        var saved = service.Data.Programs.Single();
        saved.Id.ShouldStartWith("custom-program-");
        saved.Name.ShouldBe("Personal");
        saved.Sessions.Select(session => session.Id).Distinct().Count().ShouldBe(2);
        saved.Sessions.ShouldAllBe(session => session.Id.StartsWith("custom-session-", StringComparison.Ordinal));
        saved.Sessions[0].Exercises.Select(plan => plan.Order).ShouldBe([0, 1]);
        saved.Sessions[0].Exercises[0].ShouldNotBeSameAs(exercises[0]);
        draft.Id.ShouldBe("builtin");
        exercises[0].Order.ShouldBe(48);
        sessions.Clear();
        exercises.Clear();
        saved.Sessions.Count.ShouldBe(2);
        saved.Sessions[0].Exercises.Count.ShouldBe(2);
        service.FavoriteSessionIds.ShouldBeEmpty();
        service.Data.Settings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Basic Fit", "Basic-Fit", "basic-press")]
    [InlineData("Basic-Fit", "Basic-Fit", "basic-press")]
    [InlineData("One Air", "One Air", "press")]
    [InlineData("Toutes", "Toutes", "press")]
    public async Task CreateProgramAsync_ShouldNormalizeSupportedGyms(string gym, string expected, string exerciseId)
    {
        var service = Create(new Repository());
        var draft = Draft with { Gym = gym, Sessions = [Session with { Exercises = [Plan(exerciseId)] }] };

        (await service.CreateProgramAsync(draft)).IsSuccess.ShouldBeTrue();

        service.Data.Programs.Single().Gym.ShouldBe(expected);
        service.Data.Programs.Single().Sessions.Single().Exercises.Single().Gym.ShouldBe(expected);
    }

    [Theory]
    [InlineData("name", "InvalidProgram")]
    [InlineData("name-long", "InvalidProgram")]
    [InlineData("description", "InvalidProgram")]
    [InlineData("sessions-empty", "InvalidProgram")]
    [InlineData("sessions-many", "InvalidProgram")]
    [InlineData("session-name", "InvalidProgramSession")]
    [InlineData("session-name-long", "InvalidProgramSession")]
    [InlineData("focus", "InvalidProgramSession")]
    [InlineData("color", "InvalidProgramSession")]
    [InlineData("optional-many", "InvalidProgramSession")]
    [InlineData("empty", "InvalidProgramSession")]
    [InlineData("many", "InvalidProgramSession")]
    [InlineData("sets-low", "InvalidExercise")]
    [InlineData("sets-high", "InvalidExercise")]
    [InlineData("reps", "InvalidExercise")]
    [InlineData("reps-long", "InvalidExercise")]
    [InlineData("rest-low", "InvalidExercise")]
    [InlineData("rest-high", "InvalidExercise")]
    [InlineData("rest-format", "InvalidExercise")]
    [InlineData("rest-seconds", "InvalidExercise")]
    [InlineData("rest-long", "InvalidExercise")]
    [InlineData("unknown", "UnknownExercise")]
    [InlineData("duplicate", "DuplicateExercise")]
    [InlineData("optional-duplicate", "DuplicateExercise")]
    [InlineData("optional-invalid", "InvalidExercise")]
    [InlineData("gym", "InvalidGym")]
    [InlineData("exercise-gym", "InvalidGym")]
    [InlineData("incompatible", "IncompatibleGym")]
    [InlineData("different-gym", "IncompatibleGym")]
    [InlineData("all-gyms-incompatible-plan", "IncompatibleGym")]
    [InlineData("null-draft", "InvalidProgram")]
    [InlineData("null-sessions", "InvalidProgram")]
    [InlineData("null-session", "InvalidProgramSession")]
    [InlineData("null-exercises", "InvalidProgramSession")]
    [InlineData("null-optional-exercises", "InvalidProgramSession")]
    [InlineData("null-main-plan", "UnknownExercise")]
    [InlineData("null-optional-plan", "UnknownExercise")]
    [InlineData("null-exercise-id", "UnknownExercise")]
    public async Task CreateProgramAsync_WithInvalidDraft_ShouldRejectWithoutWritingOrPublishing(string invalid, string expected)
    {
        var repository = new Repository();
        var service = Create(repository);
        await service.InitializeAsync();
        var original = service.Data;
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        (await service.CreateProgramAsync(InvalidDraft(invalid), true)).Error.ShouldBe(expected);

        repository.Writes.ShouldBe(0);
        service.Data.ShouldBeSameAs(original);
        notifications.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateProgramAsync_ShouldPreservePersonalStateAndOptionallyFavoriteInOneWrite(bool favorite)
    {
        var original = PersonalData();
        var repository = new Repository { Stored = original };
        var service = Create(repository);

        (await service.CreateProgramAsync(Draft, favorite)).IsSuccess.ShouldBeTrue();

        repository.Writes.ShouldBe(1);
        service.FavoriteSessionIds.OrderBy(id => id).ShouldBe(favorite ? service.Data.Programs.Last().Sessions.Select(session => session.Id).Order() : []);
        service.Data.Programs[0].ShouldBeSameAs(original.Programs[0]);
        service.Data.ActiveWorkout.ShouldBeSameAs(original.ActiveWorkout);
        service.Data.History.ShouldBeSameAs(original.History);
        service.Data.Exercises.ShouldBeSameAs(original.Exercises);
        service.Data.HiddenExerciseIds.ShouldBeSameAs(original.HiddenExerciseIds);
        service.Data.ReactLocalStorage.ShouldBeSameAs(original.ReactLocalStorage);
        service.Data.Settings["theme"].ShouldBe("dark");
        service.WeekSessions.ShouldBeSameAs(original.Programs[0].Sessions);
        if (!favorite) service.Data.Settings.ShouldBeSameAs(original.Settings);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("toggle")]
    public async Task ProgramMutation_WhenSaveFails_ShouldPublishNoProgramOrFavoriteChanges(string mutation)
    {
        var repository = new Repository { Stored = PersonalData(), FailWrites = true };
        var service = Create(repository);
        await service.InitializeAsync();
        var original = service.Data;
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        var result = mutation == "create" ? await service.CreateProgramAsync(Draft, true) : await service.ToggleSessionFavoriteAsync("draft-session");

        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
        service.FavoriteSessionIds.ShouldBeEmpty();
        notifications.ShouldBe(0);
    }

    [Fact]
    public async Task CreateProgramAsync_WhenSaveIsPending_ShouldPublishProgramAndFavoriteTogetherAfterSave()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { SaveStarted = started, ReleaseSave = release };
        var service = Create(repository);
        await service.InitializeAsync();
        var original = service.Data;
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        var pending = service.CreateProgramAsync(Draft, true);
        await started.Task;
        service.Data.ShouldBeSameAs(original);
        service.FavoriteSessionIds.ShouldBeEmpty();
        notifications.ShouldBe(0);
        release.SetResult();
        (await pending).IsSuccess.ShouldBeTrue();

        service.FavoriteSessionIds.Single().ShouldBe(service.Data.Programs.Single().Sessions.Single().Id);
        repository.Stored.ShouldBeSameAs(service.Data);
        notifications.ShouldBe(1);
    }

    [Theory]
    [InlineData("draft-session")]
    [InlineData("custom-existing")]
    [InlineData("imported-existing")]
    public async Task ToggleSessionFavoriteAsync_ShouldPersistAnySessionAcrossReopenAndAllowRemoval(string id)
    {
        var repository = new Repository { Stored = new PerformanceData { Programs =
        [Draft with { Id = "p1", Sessions = [Session with { Id = "custom-existing" }] }, Draft with { Id = "p2", Sessions = [Session with { Id = "imported-existing" }] }] } };
        var service = Create(repository);

        (await service.ToggleSessionFavoriteAsync(id)).IsSuccess.ShouldBeTrue();
        var reopened = Create(repository);
        await reopened.InitializeAsync();

        reopened.IsSessionFavorite(id).ShouldBeTrue();
        reopened.FavoriteSessionIds.Single().ShouldBe(id);
        (await reopened.ToggleSessionFavoriteAsync(id)).IsSuccess.ShouldBeTrue();
        reopened.FavoriteSessionIds.ShouldBeEmpty();
        repository.Writes.ShouldBe(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("broken")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[17]")]
    public async Task SessionFavorites_WithMissingOrCorruptSetting_ShouldStartEmptyAndRecoverOnToggle(string? json)
    {
        var settings = new Dictionary<string, string> { ["theme"] = "dark" };
        if (json is not null) settings[FavoriteSetting] = json;
        var repository = new Repository { Stored = new PerformanceData { Settings = settings } };
        var service = Create(repository);
        await service.InitializeAsync();

        service.FavoriteSessionIds.ShouldBeEmpty();
        service.IsSessionFavorite("draft-session").ShouldBeFalse();
        (await service.ToggleSessionFavoriteAsync("draft-session")).IsSuccess.ShouldBeTrue();

        service.IsSessionFavorite("draft-session").ShouldBeTrue();
        repository.Stored.Settings["theme"].ShouldBe("dark");
        repository.Stored.Settings[FavoriteSetting].ShouldBe("[\"draft-session\"]");
    }

    [Fact]
    public async Task SessionFavorites_ShouldFilterStaleDuplicateAndWeekIdsAndCleanThemOnNextSave()
    {
        var original = PersonalData() with { Settings = new Dictionary<string, string>
        { [FavoriteSetting] = "[\"stale\",\"draft-session\",\"draft-session\",\"week-session\",null]" } };
        var repository = new Repository { Stored = original };
        var service = Create(repository);
        await service.InitializeAsync();

        service.FavoriteSessionIds.Single().ShouldBe("draft-session");
        service.IsSessionFavorite("stale").ShouldBeFalse();
        service.IsSessionFavorite("week-session").ShouldBeFalse();
        repository.Writes.ShouldBe(0);
        (await service.ToggleSessionFavoriteAsync("draft-session")).IsSuccess.ShouldBeTrue();

        repository.Stored.Settings[FavoriteSetting].ShouldBe("[]");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("week-session")]
    public async Task ToggleSessionFavoriteAsync_WithUnknownOrWeekSession_ShouldNotWrite(string id)
    {
        var repository = new Repository { Stored = PersonalData() };
        var service = Create(repository);

        (await service.ToggleSessionFavoriteAsync(id)).Error.ShouldBe("UnknownSession");

        repository.Writes.ShouldBe(0);
        service.FavoriteSessionIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ConcurrentCreationsAndFavoriteToggle_ShouldRetainAllChangesAndDistinctIdentities()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new Repository { SaveStarted = started, ReleaseSave = release };
        var service = Create(repository);
        var first = service.CreateProgramAsync(Draft, true);
        await started.Task;
        var second = service.CreateProgramAsync(Draft, true);
        var toggle = service.ToggleSessionFavoriteAsync("draft-session");

        repository.Writes.ShouldBe(0);
        repository.Stored.Programs.ShouldBeEmpty();
        second.IsCompleted.ShouldBeFalse();
        toggle.IsCompleted.ShouldBeFalse();
        release.SetResult();
        var results = await Task.WhenAll(first, second, toggle);

        results.ShouldAllBe(result => result.IsSuccess);
        repository.Stored.Programs.Count.ShouldBe(2);
        repository.Stored.Programs.Select(program => program.Id).Distinct().Count().ShouldBe(2);
        repository.Stored.Programs.SelectMany(program => program.Sessions).Select(session => session.Id).Distinct().Count().ShouldBe(2);
        service.FavoriteSessionIds.Count.ShouldBe(3);
        repository.Writes.ShouldBe(3);
    }

    [Fact]
    public async Task CreateProgramAsync_WithMaximumCountsAndOptionalPlans_ShouldAcceptAndResolveDefaults()
    {
        var definitions = Enumerable.Range(0, 100).Select(index => Press with { Id = $"custom-{index}", IsCustom = true }).ToArray();
        var main = definitions.Take(80).Select(exercise => new WorkoutExercise { ExerciseId = exercise.Id }).ToArray();
        var optional = definitions.Skip(80).Select(exercise => Plan(exercise.Id) with { Order = 99 }).ToArray();
        var session = Session with { Name = new string('x', 160), Focus = new string('x', 300), Color = new string('x', 50),
            Exercises = main, OptionalExercises = optional };
        var repository = new Repository { Stored = new PerformanceData { Exercises = definitions } };
        var service = Create(repository);
        var draft = Draft with { Name = new string('x', 160), Description = new string('x', 2000),
            Sessions = Enumerable.Repeat(session, 100).ToArray() };

        (await service.CreateProgramAsync(draft)).IsSuccess.ShouldBeTrue();

        var saved = service.Data.Programs.Single();
        saved.Sessions.Count.ShouldBe(100);
        saved.Sessions.Select(item => item.Id).Distinct().Count().ShouldBe(100);
        saved.Sessions[0].Exercises.Count.ShouldBe(80);
        saved.Sessions[0].Exercises.ShouldAllBe(plan => plan.Sets == 4 && plan.RepTarget == "10" && plan.Rest == "2:00");
        saved.Sessions[0].OptionalExercises.Select(plan => plan.Order).ShouldBe(Enumerable.Range(0, 20));
    }

    [Theory]
    [InlineData(1, "0:15")]
    [InlineData(20, "10:00")]
    public async Task CreateProgramAsync_WithSetAndRestBoundaries_ShouldAccept(int sets, string rest)
    {
        var service = Create(new Repository());

        (await service.CreateProgramAsync(WithPlan(Plan("press") with { Sets = sets, Rest = rest, RepTarget = new string('x', 100) }))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ProgramMutations_WhenCancelled_ShouldNotSave()
    {
        var repository = new Repository();
        var service = Create(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => service.CreateProgramAsync(Draft, true, cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => service.ToggleSessionFavoriteAsync("draft-session", cancellation.Token));

        repository.Writes.ShouldBe(0);
    }

    private static WorkoutProgram InvalidDraft(string invalid) => invalid switch
    {
        "null-draft" => null!,
        "null-sessions" => Draft with { Sessions = null! },
        "null-session" => Draft with { Sessions = [null!] },
        "null-exercises" => WithSession(Session with { Exercises = null! }),
        "null-optional-exercises" => WithSession(Session with { OptionalExercises = null! }),
        "null-main-plan" => WithSession(Session with { Exercises = [null!] }),
        "null-optional-plan" => WithSession(Session with { OptionalExercises = [null!] }),
        "null-exercise-id" => WithPlan(Plan("press") with { ExerciseId = null! }),
        "name" => Draft with { Name = " " },
        "name-long" => Draft with { Name = new string('x', 161) },
        "description" => Draft with { Description = new string('x', 2001) },
        "sessions-empty" => Draft with { Sessions = [] },
        "sessions-many" => Draft with { Sessions = Enumerable.Repeat(Session, 101).ToArray() },
        "session-name" => WithSession(Session with { Name = " " }),
        "session-name-long" => WithSession(Session with { Name = new string('x', 161) }),
        "focus" => WithSession(Session with { Focus = new string('x', 301) }),
        "color" => WithSession(Session with { Color = new string('x', 51) }),
        "optional-many" => WithSession(Session with { OptionalExercises = Enumerable.Repeat(Plan("abs"), 21).ToArray() }),
        "empty" => WithSession(Session with { Exercises = [] }),
        "many" => WithSession(Session with { Exercises = Enumerable.Repeat(Plan("press"), 81).ToArray() }),
        "sets-low" => WithPlan(Plan("press") with { Sets = 0 }),
        "sets-high" => WithPlan(Plan("press") with { Sets = 21 }),
        "reps" => WithPlan(Plan("press") with { RepTarget = " " }),
        "reps-long" => WithPlan(Plan("press") with { RepTarget = new string('x', 101) }),
        "rest-low" => WithPlan(Plan("press") with { Rest = "0:14" }),
        "rest-high" => WithPlan(Plan("press") with { Rest = "10:01" }),
        "rest-format" => WithPlan(Plan("press") with { Rest = "2 min" }),
        "rest-seconds" => WithPlan(Plan("press") with { Rest = "1:60" }),
        "rest-long" => WithPlan(Plan("press") with { Rest = new string('0', 101) + ":15" }),
        "unknown" => WithPlan(Plan("unknown")),
        "duplicate" => WithSession(Session with { Exercises = [Plan("press"), Plan("press")] }),
        "optional-duplicate" => WithSession(Session with { OptionalExercises = [Plan("press")] }),
        "optional-invalid" => WithSession(Session with { OptionalExercises = [Plan("abs") with { Sets = 21 }] }),
        "gym" => Draft with { Gym = "Unknown" },
        "exercise-gym" => WithPlan(Plan("press") with { Gym = "Unknown" }),
        "incompatible" => Draft with { Gym = "Basic-Fit" },
        "different-gym" => WithPlan(Plan("press") with { Gym = "Basic-Fit" }),
        "all-gyms-incompatible-plan" => WithPlan(Plan("press") with { Gym = "Basic Fit" }) with { Gym = "Toutes" },
        _ => throw new ArgumentOutOfRangeException(nameof(invalid))
    };

    private static WorkoutProgram WithSession(WorkoutSession session) => Draft with { Sessions = [session] };
    private static WorkoutProgram WithPlan(WorkoutExercise plan) => WithSession(Session with { Exercises = [plan] });
    private static WorkoutExercise Plan(string id) => new() { ExerciseId = id, Sets = 4, RepTarget = "10", Rest = "1:30" };
    private static readonly WorkoutSession Session = new() { Id = "draft-session", Name = "Push", Exercises = [Plan("press")] };
    private static readonly WorkoutProgram Draft = new() { Id = "draft-program", Name = "Personal", Gym = "One Air", Sessions = [Session] };
    private static readonly Exercise Press = new() { Id = "press", Name = "Press", Sets = 4, RepTarget = "10", Rest = "2:00", Variants = [new GymVariant { Gym = "One Air" }] };
    private const string FavoriteSetting = "training-favorite-sessions";
    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);
    private static PerformanceData PersonalData() => new()
    {
        Programs = [Draft with { Id = "react-week-plan", Sessions = [Session with { Id = "week-session" }] }], ActiveWorkout = new ActiveWorkout { Session = Session },
        History = [new TrainingSessionLog { Id = "history", SessionId = Session.Id }],
        Settings = new Dictionary<string, string> { ["theme"] = "dark" }, HiddenExerciseIds = ["press"],
        ReactLocalStorage = new Dictionary<string, string> { ["original"] = "kept" }
    };
    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [Press, Press with { Id = "basic-press", Variants = [new GymVariant { Gym = "Basic Fit" }] },
            Press with { Id = "abs", Variants = [new GymVariant { Gym = "Toutes" }] }];
        public IReadOnlyList<WorkoutProgram> Programs => [Draft with { Id = "builtin" }];
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; set; } = new();
        public bool FailWrites { get; init; }
        public TaskCompletionSource? SaveStarted { get; init; }
        public TaskCompletionSource? ReleaseSave { get; init; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Write failed");
            SaveStarted?.TrySetResult();
            if (ReleaseSave is not null) await ReleaseSave.Task.WaitAsync(cancellationToken);
            Stored = data;
            Writes++;
        }
    }
}
