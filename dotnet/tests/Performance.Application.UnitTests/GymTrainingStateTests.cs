using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class GymTrainingStateTests
{
    [Theory]
    [InlineData("One Air", "gym-one-air")]
    [InlineData("Basic-Fit", "gym-basic-fit")]
    [InlineData("Basic Fit", "gym-basic-fit")]
    public void GetRecommendedProgram_ShouldResolveKnownGymWithoutLoadingPersonalData(string gym, string expected)
    {
        // Arrange
        var service = Create(new FakeRepository());

        // Act
        var program = service.GetRecommendedProgram(gym);

        // Assert
        program.ShouldNotBeNull().Id.ShouldBe(expected);
    }

    [Theory]
    [InlineData("push")]
    [InlineData("pull")]
    [InlineData("legs")]
    [InlineData("upper")]
    public async Task StartAsync_ShouldMapRecommendedRoleToSelectedGymAndKeepChargesSeparate(string role)
    {
        // Arrange
        var service = Create(new FakeRepository(new PerformanceData { History = [Log("one-press", "One Air", "90")] }));
        var oneAir = service.GetRecommendedProgram("One Air")!.Sessions.Single(session => session.Id == $"one-air-{role}");

        // Act
        var result = await service.StartAsync(oneAir, "Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout.ShouldNotBeNull().Session.ShouldNotBeNull().Id.ShouldBe($"basic-fit-{role}");
        service.GetExerciseGym("basic-press").ShouldBe("Basic-Fit");
        service.GetSets("basic-press").ShouldAllBe(set => set.Weight == "" && set.Reps == "");
    }

    [Fact]
    public async Task StartAsync_GymAlias_PersistsOneCanonicalGymForRecommendedSession()
    {
        // Arrange
        var service = Create(new FakeRepository());
        var session = service.GetRecommendedProgram("One Air")!.Sessions[0];

        // Act
        var result = await service.StartAsync(session, "Basic Fit");
        var optionalResult = await service.AddOptionalExercisesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        optionalResult.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout.ShouldNotBeNull().DefaultGym.ShouldBe("Basic-Fit");
        service.Data.ActiveWorkout.ExerciseGyms.Count.ShouldBe(3);
        service.Data.ActiveWorkout.ExerciseGyms.Values.ShouldAllBe(gym => gym == "Basic-Fit");
    }

    [Fact]
    public async Task StartAsync_ShouldKeepLegacySessionIdentity()
    {
        // Arrange
        var service = Create(new FakeRepository());
        var session = new WorkoutSession { Id = "push", Name = "Ancien programme", Exercises = [] };

        // Act
        var result = await service.StartAsync(session, "Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout.ShouldNotBeNull().Session.ShouldBeSameAs(session);
    }

    [Fact]
    public async Task SetGymAsync_ShouldPreserveEntireActiveSessionAndManualWeek()
    {
        // Arrange
        var service = Create(new FakeRepository());
        await service.UseGymProgramWeekAsync("One Air");
        await service.StartAsync(service.WeekSessions[0], "One Air");
        await service.UpdateSetValueAsync("one-press", 0, TrainingSetField.Reps, "10");
        var active = service.Data.ActiveWorkout;
        var week = service.WeekSessions;

        // Act
        var result = await service.SetGymAsync("Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout.ShouldBeSameAs(active);
        service.WeekSessions.ShouldBeSameAs(week);
        service.CurrentGym.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task SetActiveWorkoutGymAsync_ShouldChangeDefaultWithoutMovingExerciseChargesOrSettings()
    {
        // Arrange
        var service = Create(new FakeRepository());
        await service.SetGymAsync("One Air");
        await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        await service.UpdateSetAsync("one-press", 0, new TrainingSet { Weight = "40", Reps = "10" });
        var original = service.Data.ActiveWorkout!;

        // Act
        var result = await service.SetActiveWorkoutGymAsync("Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit");
        service.Data.ActiveWorkout.Session.ShouldBeSameAs(original.Session);
        service.GetExerciseGym("one-press").ShouldBe("One Air");
        service.GetSets("one-press").ShouldBe(original.Draft["one-press@@One Air"]);
        service.Data.ActiveWorkout.RestEnd.ShouldBe(original.RestEnd);
        service.CurrentGym.ShouldBe("One Air");
        (await service.AddOptionalExercisesAsync()).Error.ShouldBe("IncompatibleGym");
    }

    [Fact]
    public async Task UseGymProgramWeekAsync_ShouldPersistExactlyFourOrderedSessions()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = Create(repository);

        // Act
        var result = await service.UseGymProgramWeekAsync("Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.WeekSessions.Select(session => session.Id).ShouldBe(["basic-fit-push", "basic-fit-pull", "basic-fit-legs", "basic-fit-upper"]);
        repository.Stored.Programs.ShouldHaveSingleItem().Sessions.ShouldBeSameAs(service.WeekSessions);
    }

    [Fact]
    public async Task AddOptionalExercisesAsync_ShouldAddTwoSetsEachAndPreserveRestAndExistingDraft()
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData { History = [Log("one-abs", "One Air", "25"), Log("one-abs", "Basic-Fit", "70")] });
        var service = Create(repository);
        await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        await service.UpdateSetAsync("one-press", 0, new TrainingSet { Weight = "50", Reps = "10" });
        var original = service.Data.ActiveWorkout!;
        original.Session!.Exercises.Sum(exercise => exercise.Sets).ShouldBe(4);

        // Act
        var result = await service.AddOptionalExercisesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var active = service.Data.ActiveWorkout.ShouldNotBeNull();
        active.Session.ShouldNotBeNull().Exercises.Select(exercise => exercise.ExerciseId).ShouldBe(["one-press", "one-abs", "reverse-crunch"]);
        active.Session.OptionalExercises.ShouldBeEmpty();
        active.Session.Exercises.Sum(exercise => exercise.Sets).ShouldBe(8);
        active.RestEnd.ShouldBe(original.RestEnd);
        active.RestExerciseId.ShouldBe(original.RestExerciseId);
        service.GetSets("one-press").ShouldBe(original.Draft["one-press@@One Air"]);
        service.GetSets("one-abs").Count.ShouldBe(2);
        service.GetSets("one-abs")[0].ShouldBe(new TrainingSet { Weight = "25" });
        service.GetSets("reverse-crunch").ShouldAllBe(set => set.Weight == "" && set.Reps == "");
        service.GetRestSeconds("one-abs").ShouldBe(60);
    }

    [Fact]
    public async Task AddOptionalExercisesAsync_AfterReopeningAndRemoving_ShouldNeverReaddBlock()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = Create(repository);
        await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        await service.AddOptionalExercisesAsync();
        await service.RemoveExerciseAsync("one-abs");
        var resumed = Create(repository);
        await resumed.InitializeAsync();
        var before = resumed.Data;
        var writes = repository.Writes;

        // Act
        var result = await resumed.AddOptionalExercisesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        resumed.Data.ShouldBeSameAs(before);
        repository.Writes.ShouldBe(writes);
        resumed.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddOptionalExercisesAsync_WithManuallyAddedExercise_ShouldKeepItWithoutDuplicate()
    {
        // Arrange
        var service = Create(new FakeRepository());
        await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        await service.AddExerciseAsync("one-abs");
        await service.UpdateSetValueAsync("one-abs", 0, TrainingSetField.Reps, "12");

        // Act
        var result = await service.AddOptionalExercisesAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(3);
        service.GetSets("one-abs")[0].Reps.ShouldBe("12");
        service.Data.ActiveWorkout.Session.OptionalExercises.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrainingMutation_WithWriteFailure_ShouldPreserveDataAndPublishNothing(bool optionalBlock)
    {
        // Arrange
        var repository = new FakeRepository();
        var service = Create(repository);
        await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        var before = service.Data;
        repository.FailWrites = true;
        var published = 0;
        service.DataChanged += _ => published++;

        // Act
        var result = optionalBlock ? await service.AddOptionalExercisesAsync() : await service.UseGymProgramWeekAsync("Basic-Fit");

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(before);
        repository.Stored.ShouldBeSameAs(before);
        published.ShouldBe(0);
    }

    [Fact]
    public async Task StartAsync_WithUnsupportedGym_ShouldRejectInsteadOfSilentlyUsingOneAir()
    {
        // Arrange
        var service = Create(new FakeRepository());

        // Act
        var result = await service.StartAsync(service.GetRecommendedProgram("One Air")!.Sessions[0], "Unmapped gym");

        // Assert
        result.Error.ShouldBe("IncompatibleGym");
        service.Data.ActiveWorkout.ShouldBeNull();
    }

    [Fact]
    public async Task ReplayFromHistory_RecommendedSession_KeepsLoggedExercisesAndName()
    {
        // Arrange
        var log = new TrainingSessionLog
        {
            Id = "done", SessionId = "one-air-push", Name = "push", Date = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
            Exercises =
            [
                new ExercisePerformance { ExerciseId = "cable-crunch", Gym = "One Air", Sets = [new TrainingSet { Weight = "20", Reps = "12" }, new TrainingSet { Weight = "20", Reps = "12" }, new TrainingSet { Weight = "20", Reps = "10" }] },
                new ExercisePerformance { ExerciseId = "one-press", Gym = "One Air", Sets = [new TrainingSet { Weight = "90", Reps = "8" }] }
            ]
        };
        var repository = new FakeRepository(new PerformanceData { History = [log] });
        var service = Create(repository);
        await service.InitializeAsync();
        var planned = service.Programs.SelectMany(program => program.Sessions).Single(session => session.Id == log.SessionId);
        var replay = SessionExport.FromLog(log, planned, "push", service.AllExercises);

        // Act
        var started = await service.StartAsync(replay, "One Air");
        await service.UpdateSetAsync("one-press", 0, new TrainingSet { Weight = "92", Reps = "8" });
        var finished = await service.FinishAsync();

        // Assert
        started.IsSuccess.ShouldBeTrue();
        finished.IsSuccess.ShouldBeTrue();
        replay.Exercises.Select(plan => (plan.ExerciseId, plan.Sets)).ShouldBe([("cable-crunch", (int?)3), ("one-press", (int?)1)]);
        var replayed = repository.Stored.History.Last();
        replayed.Name.ShouldBe("push");
        replayed.Exercises.Select(performance => performance.ExerciseId).ShouldContain("one-press");
    }

    private static WorkoutStateService Create(FakeRepository repository) => new(repository, new FakeCatalog(),
        new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero)));

    private static TrainingSessionLog Log(string exerciseId, string gym, string weight) => new()
    {
        Id = $"{exerciseId}-{gym}", SessionId = "old", Date = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero),
        Exercises = [new ExercisePerformance { ExerciseId = exerciseId, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = "12", Rir = "2" }] }]
    };

    private sealed class FakeCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises { get; } =
        [
            Definition("one-press", "One Air"), Definition("basic-press", "Basic-Fit"),
            Definition("one-abs", "One Air"), Definition("cable-crunch", "Toutes"), Definition("reverse-crunch", "Toutes")
        ];
        public IReadOnlyList<WorkoutProgram> Programs { get; } = [Program("One Air", "one-air", "one-press", "one-abs"), Program("Basic-Fit", "basic-fit", "basic-press", "cable-crunch")];

        private static Exercise Definition(string id, string gym) => new() { Id = id, Name = id, Sets = 4, Rest = "2:30", Variants = [new GymVariant { Gym = gym }] };
        private static WorkoutProgram Program(string gym, string prefix, string press, string abs) => new()
        {
            Id = $"gym-{prefix}", Name = gym, Gym = gym,
            Sessions = new[] { "push", "pull", "legs", "upper" }.Select(role => new WorkoutSession
            {
                Id = $"{prefix}-{role}", Name = role,
                Exercises = [new WorkoutExercise { ExerciseId = press, Sets = 4, Gym = gym }],
                OptionalExercises = [new WorkoutExercise { ExerciseId = abs, Sets = 2, RepTarget = "2 × 10–15", Rest = "1:00", Gym = gym },
                    new WorkoutExercise { ExerciseId = "reverse-crunch", Sets = 2, RepTarget = "2 × 10–15", Rest = "1:00", Gym = gym }]
            }).ToArray()
        };
    }

    private sealed class FakeRepository(PerformanceData? data = null) : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = data ?? new();
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test persistence failure");
            Stored = data;
            Writes++;
            return ValueTask.CompletedTask;
        }
    }
}
