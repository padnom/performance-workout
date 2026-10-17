using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutSessionSharingTests
{
    private const string ProgramId = "my-sessions";

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_FirstImport_ShouldCreateSessionsProgramWithCopiedSession()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);

        // Act
        var result = await service.ImportSessionAsync(Package());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var program = repository.Stored.Programs.Single();
        program.Id.ShouldBe(ProgramId);
        program.Name.ShouldBe("Mes séances");
        program.Gym.ShouldBe("Toutes");
        program.Sessions.Single().Id.ShouldStartWith("imported-session-");
        program.Sessions.Single().Id.ShouldNotBe("file-session");
        WorkoutStateService.SessionsProgramId.ShouldBe(ProgramId);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_SecondImport_ShouldAppendToSameProgram()
    {
        // Arrange
        var repository = new Repository { Stored = new PerformanceData { Programs = [Other] } };
        var service = Create(repository);
        await service.ImportSessionAsync(Package());

        // Act
        await service.ImportSessionAsync(Package());

        // Assert
        repository.Stored.Programs.Count.ShouldBe(2);
        var program = repository.Stored.Programs.Single(item => item.Id == ProgramId);
        program.Sessions.Count.ShouldBe(2);
        program.Sessions.Select(session => session.Id).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WithIdenticalDefinition_ShouldReuseLocalExercise()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);

        // Act
        await service.ImportSessionAsync(Package());

        // Assert
        repository.Stored.Exercises.ShouldBeEmpty();
        repository.Stored.Programs.Single().Sessions.Single().Exercises.Single().ExerciseId.ShouldBe("press");
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WithConflictingDefinition_ShouldAddCustomExerciseAndRemapBothPlanKinds()
    {
        // Arrange
        var localOverride = Press with { Name = "Mon ancienne presse" };
        var repository = new Repository { Stored = new PerformanceData { Exercises = [localOverride] } };
        var service = Create(repository);
        var package = Package(optional: true);

        // Act
        await service.ImportSessionAsync(package);

        // Assert
        var added = repository.Stored.Exercises.Single(exercise => exercise.Id != "press");
        added.Id.ShouldStartWith("imported-exercise-");
        added.IsCustom.ShouldBeTrue();
        var saved = repository.Stored.Programs.Single().Sessions.Single();
        saved.Exercises.Single().ExerciseId.ShouldBe(added.Id);
        saved.OptionalExercises.Single().ExerciseId.ShouldBe("abs");
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WithOptionalConflict_ShouldRemapOptionalPlan()
    {
        // Arrange
        var localAbs = Abs with { Name = "Abdos locaux" };
        var repository = new Repository { Stored = new PerformanceData { Exercises = [localAbs] } };
        var service = Create(repository);

        // Act
        await service.ImportSessionAsync(Package(optional: true));

        // Assert
        var optional = repository.Stored.Programs.Single().Sessions.Single().OptionalExercises.Single();
        optional.ExerciseId.ShouldStartWith("imported-exercise-");
        repository.Stored.Exercises.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_ShouldNotTouchHistoryWeekActiveWorkoutOrSettings()
    {
        // Arrange
        var original = PersonalData();
        var repository = new Repository { Stored = original };
        var service = Create(repository);

        // Act
        await service.ImportSessionAsync(Package());

        // Assert
        repository.Stored.History.ShouldBeSameAs(original.History);
        repository.Stored.ActiveWorkout.ShouldBeSameAs(original.ActiveWorkout);
        repository.Stored.Settings.ShouldBeSameAs(original.Settings);
        service.WeekSessions.ShouldBeSameAs(original.Programs[0].Sessions);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("undefined-exercise")]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WithInvalidPackage_ShouldRejectAndLeaveDataUnchanged(string invalid)
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.InitializeAsync();
        var original = service.Data;
        var package = invalid == "kind" ? Package() with { Kind = "other" } : Package() with { Exercises = [] };

        // Act
        var result = await service.ImportSessionAsync(package);

        // Assert
        result.Error.ShouldBe("InvalidSessionPackage");
        service.Data.ShouldBeSameAs(original);
        repository.Writes.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WhenSessionsProgramHasHundredSessions_ShouldRejectWithSessionsProgramFull()
    {
        // Arrange
        var full = SessionsProgram(100);
        var repository = new Repository { Stored = new PerformanceData { Programs = [full] } };
        var service = Create(repository);

        // Act
        var result = await service.ImportSessionAsync(Package());

        // Assert
        result.Error.ShouldBe("SessionsProgramFull");
        repository.Writes.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSessionAsync_WithExistingProgram_ShouldKeepItsOtherProperties()
    {
        // Arrange
        var existing = SessionsProgram(1) with { Name = "Renommée", Gym = "One Air" };
        var repository = new Repository { Stored = new PerformanceData { Programs = [existing] } };
        var service = Create(repository);

        // Act
        await service.ImportSessionAsync(Package());

        // Assert
        var program = repository.Stored.Programs.Single();
        program.Name.ShouldBe("Renommée");
        program.Gym.ShouldBe("One Air");
        program.Sessions.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSessionAsync_ShouldCloneTrimNormalizeGymAndReindexOrder()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        var draft = Draft(Plan("basic-press") with { Order = 40 }, Plan("abs") with { Order = -2 }) with { Id = "incoming", Name = "  Push  " };

        // Act
        var result = await service.CreateSessionAsync(draft, "Basic Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var program = repository.Stored.Programs.Single();
        program.Id.ShouldBe(ProgramId);
        var saved = program.Sessions.Single();
        saved.Id.ShouldStartWith("custom-session-");
        saved.Name.ShouldBe("Push");
        saved.Exercises.Select(plan => plan.Order).ShouldBe([0, 1]);
        saved.Exercises[0].Gym.ShouldBe("Basic-Fit");
        draft.Id.ShouldBe("incoming");
    }

    [Theory]
    [InlineData("unknown", "One Air", "UnknownExercise")]
    [InlineData("duplicate", "One Air", "DuplicateExercise")]
    [InlineData("press", "Unknown", "InvalidGym")]
    [InlineData("basic-press", "One Air", "IncompatibleGym")]
    [InlineData("empty", "One Air", "InvalidProgramSession")]
    [Trait("AC", "AC-45")]
    public async Task CreateSessionAsync_WithInvalidDraft_ShouldRejectWithoutWriting(string scenario, string gym, string expected)
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        var draft = scenario switch
        {
            "empty" => Draft(),
            "duplicate" => Draft(Plan("press"), Plan("press")),
            _ => Draft(Plan(scenario))
        };

        // Act
        var result = await service.CreateSessionAsync(draft, gym);

        // Assert
        result.Error.ShouldBe(expected);
        repository.Writes.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSessionAsync_WhenSessionsProgramHasHundredSessions_ShouldRejectWithSessionsProgramFull()
    {
        // Arrange
        var repository = new Repository { Stored = new PerformanceData { Programs = [SessionsProgram(100)] } };
        var service = Create(repository);

        // Act
        var result = await service.CreateSessionAsync(Draft(Plan("press")), "One Air");

        // Assert
        result.Error.ShouldBe("SessionsProgramFull");
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSessionAsync_ShouldNotTouchHistoryWeekActiveWorkoutOrSettings()
    {
        // Arrange
        var original = PersonalData();
        var repository = new Repository { Stored = original };
        var service = Create(repository);

        // Act
        await service.CreateSessionAsync(Draft(Plan("press")), "One Air");

        // Assert
        repository.Stored.History.ShouldBeSameAs(original.History);
        repository.Stored.ActiveWorkout.ShouldBeSameAs(original.ActiveWorkout);
        repository.Stored.Settings.ShouldBeSameAs(original.Settings);
        service.WeekSessions.ShouldBeSameAs(original.Programs[0].Sessions);
    }

    [Theory]
    [InlineData("import")]
    [InlineData("create")]
    [Trait("AC", "AC-45")]
    public async Task SessionMutation_WhenSaveFails_ShouldPublishNothing(string mutation)
    {
        // Arrange
        var repository = new Repository { Stored = PersonalData(), FailWrites = true };
        var service = Create(repository);
        await service.InitializeAsync();
        var original = service.Data;
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        // Act
        var result = mutation == "import"
            ? await service.ImportSessionAsync(Package())
            : await service.CreateSessionAsync(Draft(Plan("press")), "One Air");

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        notifications.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-46")]
    public async Task AddWeekSessionAsync_WithSessionFromSessionsProgram_ShouldAddItToWeek()
    {
        // Arrange
        var repository = new Repository { Stored = PersonalData() };
        var service = Create(repository);
        await service.CreateSessionAsync(Draft(Plan("press")), "One Air");
        var sessionId = service.Data.Programs.Single(program => program.Id == ProgramId).Sessions.Single().Id;

        // Act
        var result = await service.AddWeekSessionAsync(sessionId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.WeekSessions.ShouldContain(session => session.Id == sessionId);
    }

    [Fact]
    [Trait("AC", "AC-46")]
    public async Task StartAsync_WithSessionFromSessionsProgram_ShouldStartWorkout()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.ImportSessionAsync(Package());
        var session = service.Data.Programs.Single().Sessions.Single();

        // Act
        var result = await service.StartAsync(session, "One Air");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout.ShouldNotBeNull();
    }

    private static WorkoutExercise Plan(string id) => new() { ExerciseId = id, Sets = 4, RepTarget = "10", Rest = "1:30" };
    private static WorkoutSession Draft(params WorkoutExercise[] plans) => new() { Id = "draft", Name = "Push", Exercises = plans };
    private static readonly Exercise Press = new() { Id = "press", Name = "Press", Sets = 4, RepTarget = "10", Rest = "2:00", Variants = [new GymVariant { Gym = "One Air" }] };
    private static readonly Exercise Abs = new() { Id = "abs", Name = "Abdos", Sets = 3, RepTarget = "15", Rest = "1:00", Variants = [new GymVariant { Gym = "Toutes" }] };
    private static readonly WorkoutProgram Other = new() { Id = "other", Name = "Other", Gym = "One Air", Sessions = [Draft(Plan("press"))] };

    private static SessionPackage Package(bool optional = false) => new()
    {
        Session = optional
            ? new WorkoutSession { Id = "file-session", Name = "Push", Exercises = [Plan("press")], OptionalExercises = [Plan("abs")] }
            : new WorkoutSession { Id = "file-session", Name = "Push", Exercises = [Plan("press")] },
        Exercises = optional ? [Press, Abs] : [Press]
    };

    private static WorkoutProgram SessionsProgram(int count) => new()
    {
        Id = ProgramId, Name = "Mes séances", Gym = "Toutes",
        Sessions = Enumerable.Range(0, count).Select(index => Draft(Plan("press")) with { Id = $"existing-{index}" }).ToArray()
    };

    private static PerformanceData PersonalData() => new()
    {
        Programs = [Other with { Id = "react-week-plan" }], ActiveWorkout = new ActiveWorkout { Session = Draft(Plan("press")) },
        History = [new TrainingSessionLog { Id = "history", SessionId = "draft" }],
        Settings = new Dictionary<string, string> { ["training-favorite-programs"] = "[]" }
    };

    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);

    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [Press, Press with { Id = "basic-press", Variants = [new GymVariant { Gym = "Basic Fit" }] }, Abs];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; set; } = new();
        public bool FailWrites { get; init; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Write failed");
            Stored = data;
            Writes++;
            return ValueTask.CompletedTask;
        }
    }
}
