using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutProgramSharingTests
{
    [Fact]
    public async Task ImportProgramsAsync_WithIdenticalDefinition_ShouldReuseExerciseButCopyProgramAndSession()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);
        await state.InitializeAsync();

        // Act
        var result = await state.ImportProgramsAsync(Package());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        repository.Stored.Exercises.ShouldBeEmpty();
        var imported = repository.Stored.Programs.Single();
        imported.Id.ShouldNotBe("program");
        imported.Sessions.Single().Id.ShouldNotBe("session");
        imported.Sessions.Single().Exercises.Single().ExerciseId.ShouldBe("press");
        imported.Gym.ShouldBe("One Air");
    }

    [Fact]
    public async Task ImportProgramsAsync_WithConflictingDefinition_ShouldPreserveLocalDefinitionAndRemapReferences()
    {
        // Arrange
        var localOverride = Press with { Name = "Mon ancienne presse" };
        var repository = new Repository { Stored = new PerformanceData { Exercises = [localOverride] } };
        var state = Create(repository);
        await state.InitializeAsync();

        // Act
        var result = await state.ImportProgramsAsync(Package());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        state.AllExercises.Single(exercise => exercise.Id == "press").ShouldBeSameAs(localOverride);
        var importedDefinition = repository.Stored.Exercises.Single(exercise => exercise.Id != "press");
        importedDefinition.IsCustom.ShouldBeTrue();
        importedDefinition.Name.ShouldBe(Press.Name);
        repository.Stored.Programs.Single().Sessions.Single().Exercises.Single().ExerciseId.ShouldBe(importedDefinition.Id);
    }

    [Fact]
    public async Task ImportProgramsAsync_WithOptionalExercises_ShouldCopyDefinitionsAndBothKindsOfPlans()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);
        var abs = Press with { Id = "abs", Name = "Abdos", Sets = 2, Rest = "1 min" };
        var package = Package() with
        {
            Exercises = [Press, abs],
            Programs = [new WorkoutProgram { Id = "program", Name = "Partagé", Sessions = [new WorkoutSession
            {
                Id = "session", Name = "Push", Exercises = [new WorkoutExercise { ExerciseId = "press" }],
                OptionalExercises = [new WorkoutExercise { ExerciseId = "abs", Sets = 2, RepTarget = "10–15", Rest = "1 min" }]
            }] }]
        };

        // Act
        var result = await state.ImportProgramsAsync(package);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var session = repository.Stored.Programs.Single().Sessions.Single();
        session.Exercises.Single().ExerciseId.ShouldBe("press");
        session.OptionalExercises.Single().ExerciseId.ShouldBe(repository.Stored.Exercises.Single().Id);
        session.OptionalExercises.Single().Sets.ShouldBe(2);
    }

    [Fact]
    public async Task ImportProgramsAsync_WithExistingPersonalData_ShouldOnlyAppendProgramsAndDefinitions()
    {
        // Arrange
        var week = new WorkoutProgram { Id = "react-week-plan", Name = "Ma semaine", Sessions = Package().Programs[0].Sessions };
        var active = new ActiveWorkout { Session = week.Sessions[0], StartedAt = DateTimeOffset.UtcNow };
        var history = new TrainingSessionLog { Id = "history", SessionId = "old", Date = DateTimeOffset.UtcNow };
        var original = new PerformanceData { Programs = [week], History = [history], ActiveWorkout = active,
            Settings = new Dictionary<string, string> { ["theme"] = "dark" }, HiddenExerciseIds = ["press"] };
        var repository = new Repository { Stored = original };
        var state = Create(repository);

        // Act
        await state.ImportProgramsAsync(Package());

        // Assert
        repository.Stored.Programs[0].ShouldBeSameAs(week);
        repository.Stored.History.ShouldBeSameAs(original.History);
        repository.Stored.ActiveWorkout.ShouldBeSameAs(active);
        repository.Stored.Settings.ShouldBeSameAs(original.Settings);
        repository.Stored.HiddenExerciseIds.ShouldBeSameAs(original.HiddenExerciseIds);
        state.WeekSessions.ShouldBeSameAs(week.Sessions);
    }

    [Fact]
    public async Task ImportProgramsAsync_WhenWriteFails_ShouldPublishNoChanges()
    {
        // Arrange
        var repository = new Repository { FailWrites = true };
        var state = Create(repository);
        await state.InitializeAsync();
        var original = state.Data;
        var notifications = 0;
        state.DataChanged += _ => notifications++;

        // Act
        var result = await state.ImportProgramsAsync(Package());

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        state.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
        notifications.ShouldBe(0);
    }

    [Fact]
    public async Task ImportProgramsAsync_WithInvalidPackage_ShouldNotWrite()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);

        // Act
        var result = await state.ImportProgramsAsync(Package() with { Exercises = [] });

        // Assert
        result.Error.ShouldBe("InvalidProgramPackage");
        repository.Writes.ShouldBe(0);
        state.Data.Programs.ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportProgramsAsync_ConcurrentImports_ShouldKeepBothCopiesAndUniqueIdentities()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);

        // Act
        var results = await Task.WhenAll(state.ImportProgramsAsync(Package()), state.ImportProgramsAsync(Package()));

        // Assert
        results.All(result => result.IsSuccess).ShouldBeTrue();
        repository.Stored.Programs.Count.ShouldBe(2);
        repository.Stored.Programs.Select(program => program.Id).Distinct().Count().ShouldBe(2);
        repository.Stored.Programs.SelectMany(program => program.Sessions).Select(session => session.Id).Distinct().Count().ShouldBe(2);
        repository.Writes.ShouldBe(2);
    }

    [Fact]
    public async Task ImportProgramsAsync_AfterReopening_ShouldRetainAddedCopies()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);
        await state.ImportProgramsAsync(Package());
        var reopened = Create(repository);

        // Act
        await reopened.InitializeAsync();

        // Assert
        reopened.Programs.Single().ShouldBeSameAs(repository.Stored.Programs.Single());
    }

    [Fact]
    public async Task ImportProgramsAsync_WithSameIdButDifferentMachine_ShouldCreateSeparateHistoryIdentity()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);
        var otherMachine = Press with { Variants = [new GymVariant { Gym = "One Air", MachineName = "Hammer Chest Press" }] };

        // Act
        await state.ImportProgramsAsync(Package() with { Exercises = [otherMachine] });

        // Assert
        var imported = repository.Stored.Exercises.Single();
        imported.Id.ShouldNotBe("press");
        imported.Variants.Single().MachineName.ShouldBe("Hammer Chest Press");
        repository.Stored.Programs.Single().Sessions.Single().Exercises.Single().ExerciseId.ShouldBe(imported.Id);
    }

    [Fact]
    public async Task ImportProgramsAsync_WithOnlyProvenanceDifference_ShouldReuseIdenticalDefinition()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);

        // Act
        await state.ImportProgramsAsync(Package() with { Exercises = [Press with { IsCustom = true }] });

        // Assert
        repository.Stored.Exercises.ShouldBeEmpty();
        repository.Stored.Programs.Single().Sessions.Single().Exercises.Single().ExerciseId.ShouldBe("press");
    }

    [Fact]
    public async Task ImportProgramsAsync_WhenCancelled_ShouldNotSaveOrPublish()
    {
        // Arrange
        var repository = new Repository();
        var state = Create(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(() => state.ImportProgramsAsync(Package(), cancellation.Token));
        repository.Writes.ShouldBe(0);
        state.Data.Programs.ShouldBeEmpty();
    }

    private static readonly Exercise Press = new() { Id = "press", Name = "Presse", MuscleGroup = "Pectoraux", RepTarget = "10", Rest = "2 min", Variants = [new GymVariant { Gym = "One Air", MachineName = "Chest Press" }] };
    private static ProgramPackage Package() => new() { Exercises = [Press], Programs = [new WorkoutProgram { Id = "program", Name = "Programme", Gym = "One Air", Sessions = [new WorkoutSession { Id = "session", Name = "Push", Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 4, RepTarget = "10", Rest = "2 min" }] }] }] };
    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);
    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [Press];
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
