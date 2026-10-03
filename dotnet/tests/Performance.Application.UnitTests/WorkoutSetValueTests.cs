using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutSetValueTests
{
    [Fact]
    public async Task UpdateSetValueAsync_WithConcurrentFieldsAndDelayedSave_ShouldRetainBothValues()
    {
        // Arrange
        var repository = new DelayedRepository();
        var clock = new FakeTimeProvider();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        repository.PauseNextWrite = true;

        // Act
        var weightEdit = service.UpdateSetValueAsync("press", 0, TrainingSetField.Weight, "40");
        await repository.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var repetitionEdit = service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "8");
        repository.ReleaseWrite.SetResult();
        var results = await Task.WhenAll(weightEdit, repetitionEdit);

        // Assert
        results.ShouldAllBe(result => result.IsSuccess);
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = "40", Reps = "8" });
        repository.Stored.ActiveWorkout!.Draft["press@@One Air"][0].ShouldBe(new TrainingSet { Weight = "40", Reps = "8" });
        repository.Stored.ActiveWorkout.RestEnd.ShouldBe(clock.GetUtcNow().AddSeconds(90));
        repository.Stored.ActiveWorkout.RestExerciseId.ShouldBe("press");
    }

    [Fact]
    public async Task UpdateSetValueAsync_WithConcurrentRepetitionEdits_ShouldStartTimerOnlyOnce()
    {
        // Arrange
        var repository = new DelayedRepository();
        var clock = new FakeTimeProvider();
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        var expectedDeadline = clock.GetUtcNow().AddSeconds(90);
        repository.PauseNextWrite = true;

        // Act
        var first = service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "8");
        await repository.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(20));
        var correction = service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "10");
        repository.ReleaseWrite.SetResult();
        await Task.WhenAll(first, correction);

        // Assert
        service.GetSets("press")[0].Reps.ShouldBe("10");
        service.Data.ActiveWorkout!.RestEnd.ShouldBe(expectedDeadline);
    }

    [Fact]
    public async Task UpdateSetValueAsync_WithQueuedInvalidEdit_ShouldRetainLatestSuccessfulField()
    {
        // Arrange
        var repository = new DelayedRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        repository.PauseNextWrite = true;

        // Act
        var weightEdit = service.UpdateSetValueAsync("press", 0, TrainingSetField.Weight, "40");
        await repository.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var invalidRepetitions = service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "1.5");
        repository.ReleaseWrite.SetResult();
        await weightEdit;
        var result = await invalidRepetitions;

        // Assert
        result.Error.ShouldBe("InvalidSet");
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = "40" });
        service.Data.ShouldBeSameAs(repository.Stored);
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
        repository.SuccessfulWrites.ShouldBe(2);
    }

    [Fact]
    public async Task UpdateSetValueAsync_WithQueuedFailedSave_ShouldRetainLatestSuccessfulField()
    {
        // Arrange
        var repository = new DelayedRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        repository.PauseNextWrite = true;
        repository.FailWriteNumber = 3;

        // Act
        var weightEdit = service.UpdateSetValueAsync("press", 0, TrainingSetField.Weight, "40");
        await repository.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var repetitionEdit = service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "8");
        repository.ReleaseWrite.SetResult();
        await weightEdit;
        var result = await repetitionEdit;

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = "40" });
        service.Data.ShouldBeSameAs(repository.Stored);
        service.Data.ActiveWorkout!.RestEnd.ShouldBeNull();
        repository.SuccessfulWrites.ShouldBe(2);
    }

    [Theory]
    [InlineData(TrainingSetField.Weight, "45", "45", "8", "2")]
    [InlineData(TrainingSetField.Reps, "12", "40", "12", "2")]
    [InlineData(TrainingSetField.Rir, "1", "40", "8", "1")]
    public async Task UpdateSetValueAsync_WithSingleField_ShouldPreserveOtherFields(TrainingSetField field, string value,
        string expectedWeight, string expectedRepetitions, string expectedReserve)
    {
        // Arrange
        var service = CreateService(new DelayedRepository());
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "40", Reps = "8", Rir = "2" });

        // Act
        var result = await service.UpdateSetValueAsync("press", 0, field, value);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = expectedWeight, Reps = expectedRepetitions, Rir = expectedReserve });
    }

    [Theory]
    [InlineData(TrainingSetField.Weight, "-1")]
    [InlineData(TrainingSetField.Reps, "-1")]
    [InlineData(TrainingSetField.Reps, "1.5")]
    [InlineData(TrainingSetField.Rir, "11")]
    [InlineData(TrainingSetField.Weight, null)]
    public async Task UpdateSetValueAsync_WithInvalidValue_ShouldRejectWithoutSave(TrainingSetField field, string? value)
    {
        // Arrange
        var repository = new DelayedRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        var original = service.Data;

        // Act
        var result = await service.UpdateSetValueAsync("press", 0, field, value!);

        // Assert
        result.Error.ShouldBe("InvalidSet");
        service.Data.ShouldBeSameAs(original);
        repository.SuccessfulWrites.ShouldBe(1);
    }

    [Theory]
    [InlineData("press", -1, TrainingSetField.Weight, "One Air", "InvalidSetIndex")]
    [InlineData("press", 2, TrainingSetField.Weight, "One Air", "InvalidSetIndex")]
    [InlineData("unknown", 0, TrainingSetField.Weight, "One Air", "UnknownExercise")]
    [InlineData("press", 0, (TrainingSetField)99, "One Air", "InvalidSetField")]
    [InlineData("press", 0, TrainingSetField.Weight, " ", "InvalidGym")]
    public async Task UpdateSetValueAsync_WithInvalidTarget_ShouldRejectWithoutSave(string id, int index,
        TrainingSetField field, string gym, string expectedError)
    {
        // Arrange
        var repository = new DelayedRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");

        // Act
        var result = await service.UpdateSetValueAsync(id, index, field, "40", gym);

        // Assert
        result.Error.ShouldBe(expectedError);
        repository.SuccessfulWrites.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateSetValueAsync_WithoutActiveWorkout_ShouldRejectWithoutSave()
    {
        // Arrange
        var repository = new DelayedRepository();
        var service = CreateService(repository);

        // Act
        var result = await service.UpdateSetValueAsync("press", 0, TrainingSetField.Weight, "40");

        // Assert
        result.Error.ShouldBe("NoActiveWorkout");
        repository.SuccessfulWrites.ShouldBe(0);
    }

    private static WorkoutStateService CreateService(DelayedRepository repository, FakeTimeProvider? clock = null) =>
        new(repository, new FakeCatalog(), clock ?? new FakeTimeProvider());

    private static WorkoutSession Session() => new()
    {
        Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 2 }]
    };

    private sealed class FakeCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press", Rest = "1 min 30" }];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }

    private sealed class DelayedRepository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = new();
        public bool PauseNextWrite { get; set; }
        public int? FailWriteNumber { get; set; }
        public int SuccessfulWrites { get; private set; }
        private int _attemptedWrites;
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            _attemptedWrites++;
            if (PauseNextWrite)
            {
                PauseNextWrite = false;
                WriteStarted.SetResult();
                await ReleaseWrite.Task.WaitAsync(cancellationToken);
            }
            if (FailWriteNumber == _attemptedWrites) throw new IOException("Test write failure");
            Stored = data;
            SuccessfulWrites++;
        }
    }
}
