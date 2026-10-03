using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutRestTests
{
    [Theory]
    [InlineData("1:45", 105)]
    [InlineData("3 min", 180)]
    [InlineData("2–3 min", 180)]
    [InlineData("2–3", 150)]
    [InlineData("1 min 30–2 min", 150)]
    [InlineData("2 min", 120)]
    [InlineData("1 min 30", 90)]
    [InlineData("45 s", 60)]
    [InlineData("", 60)]
    public async Task GetRestSeconds_WithOriginalRestLabels_ShouldMatchWebParser(string rest, int expected)
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(rest), "One Air");

        // Act
        var seconds = service.GetRestSeconds("press");

        // Assert
        seconds.ShouldBe(expected);
    }

    [Fact]
    public async Task GetRestSeconds_WithPlanOverride_ShouldUsePlanInsteadOfCatalog()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session("3 min"), "One Air");

        // Act
        var seconds = service.GetRestSeconds("press");

        // Assert
        seconds.ShouldBe(180);
    }

    [Fact]
    public async Task AddExerciseAsync_WithNoPlanRest_ShouldUseCatalogRest()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session() with { Exercises = [] }, "One Air");

        // Act
        await service.AddExerciseAsync("press");

        // Assert
        service.GetRestSeconds("press").ShouldBe(90);
    }

    [Fact]
    public async Task UpdateSetAsync_WithFirstPositiveRepetitions_ShouldPersistRestDeadlineWithSet()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var repository = new FakeRepository();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session("2 min"), "One Air");

        // Act
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "40", Reps = "8" });

        // Assert
        repository.Stored.ActiveWorkout!.RestEnd.ShouldBe(clock.GetUtcNow().AddSeconds(120));
        repository.Stored.ActiveWorkout.RestExerciseId.ShouldBe("press");
        repository.Stored.ActiveWorkout.Draft["press@@One Air"][0].Reps.ShouldBe("8");
        repository.SuccessfulWrites.ShouldBe(2);
    }

    [Theory]
    [InlineData("40", "")]
    [InlineData("40", "0")]
    public async Task UpdateSetAsync_WithoutPositiveRepetitions_ShouldNotStartRest(string weight, string reps)
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(), "One Air");

        // Act
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = weight, Reps = reps });

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateSetAsync_WithAlreadyEnteredRepetitions_ShouldNotRestartRest()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = CreateService(new FakeRepository(), clock);
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Reps = "8" });
        var deadline = service.Data.ActiveWorkout!.RestEnd;
        clock.Advance(TimeSpan.FromSeconds(20));

        // Act
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "45", Reps = "10" });

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBe(deadline);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(105)]
    [InlineData(120)]
    [InlineData(150)]
    [InlineData(180)]
    public async Task StartRestAsync_WithConfiguredDuration_ShouldUseSelection(int seconds)
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = CreateService(new FakeRepository(), clock);
        await service.StartAsync(Session(), "One Air");
        await service.SetRestDurationAsync("press", seconds);

        // Act
        await service.StartRestAsync("press");

        // Assert
        service.GetRestSeconds("press").ShouldBe(seconds);
        service.Data.ActiveWorkout!.RestEnd.ShouldBe(clock.GetUtcNow().AddSeconds(seconds));
    }

    [Fact]
    public async Task ExtendRestAsync_WithRunningTimer_ShouldAddThirtySecondsToDeadline()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = CreateService(new FakeRepository(), clock);
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");
        var deadline = service.Data.ActiveWorkout!.RestEnd;
        clock.Advance(TimeSpan.FromSeconds(20));

        // Act
        await service.ExtendRestAsync();

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBe(deadline!.Value.AddSeconds(30));
    }

    [Fact]
    public async Task ExtendRestAsync_WithExpiredTimer_ShouldNotReviveTimer()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = CreateService(new FakeRepository(), clock);
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");
        clock.Advance(TimeSpan.FromMinutes(5));
        var original = service.Data;

        // Act
        var result = await service.ExtendRestAsync();

        // Assert
        result.Error.ShouldBe("NoActiveRest");
        service.Data.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task ExtendRestAsync_WithMaximumImportedDeadline_ShouldRejectWithoutOverflow()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.ReplaceDataAsync(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout { Session = Session(), RestEnd = DateTimeOffset.MaxValue, RestExerciseId = "press" }
        });
        var previous = service.Data;

        // Act
        var result = await service.ExtendRestAsync();

        // Assert
        result.Error.ShouldBe("InvalidRestDuration");
        service.Data.ShouldBeSameAs(previous);
    }

    [Fact]
    public async Task StopRestAsync_WithRunningTimer_ShouldClearDeadlineAndExercise()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");

        // Act
        await service.StopRestAsync();

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
        service.Data.ActiveWorkout.RestExerciseId.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteRestAsync_WithExpiredTimer_ShouldClearOnce()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var repository = new FakeRepository();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        await Task.WhenAll(service.CompleteRestAsync(), service.CompleteRestAsync());

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
        service.Data.ActiveWorkout.RestExerciseId.ShouldBeNull();
        repository.SuccessfulWrites.ShouldBe(3);
    }

    [Fact]
    public async Task CompleteRestAsync_WithRunningTimer_ShouldKeepDeadline()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");
        var previous = service.Data;

        // Act
        await service.CompleteRestAsync();

        // Assert
        service.Data.ShouldBeSameAs(previous);
        repository.SuccessfulWrites.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateSetAsync_WhenPersistenceFails_ShouldRetainPreviousRestAndSet()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        var original = service.Data;
        repository.FailWrites = true;

        // Act
        var result = await service.UpdateSetAsync("press", 0, new TrainingSet { Reps = "8" });

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
    }

    [Fact]
    public async Task StartRestAsync_WhenPersistenceFails_ShouldRetainPreviousTimer()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        var original = service.Data;
        repository.FailWrites = true;

        // Act
        var result = await service.StartRestAsync("press");

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task CompleteRestAsync_WhenPersistenceFails_ShouldRetainExpiredDeadlineForRetry()
    {
        // Arrange
        var repository = new FakeRepository();
        var clock = new FakeTimeProvider();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        await service.StartRestAsync("press");
        clock.Advance(TimeSpan.FromMinutes(5));
        var original = service.Data;
        repository.FailWrites = true;

        // Act
        var result = await service.CompleteRestAsync();

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task InitializeAsync_WithStoredRunningRest_ShouldRestoreExactDeadlineAndSelection()
    {
        // Arrange
        var repository = new FakeRepository();
        var clock = new FakeTimeProvider();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        await service.SetRestDurationAsync("press", 150);
        await service.StartRestAsync("press");
        var deadline = service.Data.ActiveWorkout!.RestEnd;
        clock.Advance(TimeSpan.FromSeconds(45));
        var resumed = CreateService(repository, clock);

        // Act
        await resumed.InitializeAsync();

        // Assert
        resumed.Data.ActiveWorkout!.RestEnd.ShouldBe(deadline);
        resumed.Data.ActiveWorkout.RestExerciseId.ShouldBe("press");
        resumed.GetRestSeconds("press").ShouldBe(150);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    [InlineData(601)]
    public async Task SetRestDurationAsync_WithInvalidDuration_ShouldRejectWithoutSave(int seconds)
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");

        // Act
        var result = await service.SetRestDurationAsync("press", seconds);

        // Assert
        result.Error.ShouldBe("InvalidRestDuration");
        repository.SuccessfulWrites.ShouldBe(1);
    }

    [Fact]
    public async Task StartRestAsync_WithUnknownExercise_ShouldRejectWithoutSave()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");

        // Act
        var result = await service.StartRestAsync("unknown");

        // Assert
        result.Error.ShouldBe("UnknownExercise");
        repository.SuccessfulWrites.ShouldBe(1);
    }

    private static WorkoutSession Session(string? rest = null) => new()
    {
        Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 2, Rest = rest }]
    };

    private static WorkoutStateService CreateService(FakeRepository repository, FakeTimeProvider? clock = null) =>
        new(repository, new FakeCatalog(), clock ?? new FakeTimeProvider());

    private sealed class FakeCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press", Rest = "1 min 30", Sets = 2 }];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = new();
        public bool FailWrites { get; set; }
        public int SuccessfulWrites { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test write failure");
            Stored = data;
            SuccessfulWrites++;
            return ValueTask.CompletedTask;
        }
    }
}
