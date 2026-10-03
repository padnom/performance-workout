using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutHistoryIndexTests
{
    [Fact]
    public async Task GetExerciseHistory_WithLegacyAliases_ShouldReturnNewestMatchingGym()
    {
        // Arrange
        var service = Create(new PerformanceData
        {
            Exercises = [new Exercise { Id = "legacy", Name = "PRÉSS" }],
            History = [Log("legacy", "One Air", 1, "40"), Log("press", "Basic-Fit", 4, "80"), Log("press", null, 3, "50")]
        });
        await service.InitializeAsync();

        // Act
        var history = service.GetExerciseHistory("press", "One Air");

        // Assert
        history.Select(entry => entry.Performance.Sets[0].Weight).ShouldBe(["50", "40"]);
        service.GetPreviousSets("press", "One Air")[0].Weight.ShouldBe("50");
        service.GetExerciseRecord("press", "One Air").ShouldBe(50);
    }

    [Fact]
    public async Task GetExerciseHistory_WhenOnlyDraftChanges_ShouldReuseCachedHistory()
    {
        // Arrange
        var service = Create(new PerformanceData { History = [Log("press", "One Air", 1, "50")] });
        await service.StartAsync(new WorkoutSession { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] }, "One Air");
        var history = service.GetExerciseHistory("press", "One Air");

        // Act
        await service.UpdateSetValueAsync("press", 0, TrainingSetField.Reps, "12");

        // Assert
        service.GetExerciseHistory("press", "One Air").ShouldBeSameAs(history);
    }

    [Fact]
    public async Task GetExerciseHistory_AfterFinishing_ShouldRefreshCachedHistory()
    {
        // Arrange
        var service = Create(new PerformanceData());
        await service.StartAsync(new WorkoutSession { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] }, "One Air");
        var history = service.GetExerciseHistory("press", "One Air");
        history.ShouldBeEmpty();
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "30", Reps = "8" });

        // Act
        await service.FinishAsync();

        // Assert
        service.GetExerciseHistory("press", "One Air").ShouldHaveSingleItem().Performance.Sets[0].Weight.ShouldBe("30");
        service.GetExerciseHistory("press", "One Air").ShouldNotBeSameAs(history);
    }

    [Fact]
    public async Task GetExerciseHistory_AfterImport_ShouldRefreshCachedHistory()
    {
        // Arrange
        var service = Create(new PerformanceData { History = [Log("press", "One Air", 1, "50")] });
        await service.InitializeAsync();
        var history = service.GetExerciseHistory("press", "One Air");

        // Act
        await service.ReplaceDataAsync(new PerformanceData { History = [Log("press", "One Air", 2, "60")] });

        // Assert
        service.GetPreviousSets("press", "One Air")[0].Weight.ShouldBe("60");
        service.GetExerciseHistory("press", "One Air").ShouldNotBeSameAs(history);
    }

    [Fact]
    public async Task GetExerciseHistory_WhenGymNotSpecified_ShouldIncludeAllGyms()
    {
        // Arrange
        var service = Create(new PerformanceData { History = [Log("press", "One Air", 1, "50"), Log("press", "Basic-Fit", 2, "60")] });
        await service.InitializeAsync();

        // Act
        var history = service.GetExerciseHistory("press");

        // Assert
        history.Select(entry => entry.Performance.Gym).ShouldBe(["Basic-Fit", "One Air"]);
    }

    [Fact]
    public async Task GetExerciseRecord_WithCommaWeightAndInvalidImportedWeight_ShouldIgnoreInvalidWeight()
    {
        // Arrange
        var service = Create(new PerformanceData { History = [Log("press", "One Air", 1, "45,5"), Log("press", "One Air", 2, "bad")] });
        await service.InitializeAsync();

        // Act
        var record = service.GetExerciseRecord("press", "One Air");

        // Assert
        record.ShouldBe(45.5m);
    }

    [Fact]
    public async Task GetExerciseRecord_WithoutHistory_ShouldReturnNull()
    {
        // Arrange
        var service = Create(new PerformanceData());
        await service.InitializeAsync();

        // Act
        var record = service.GetExerciseRecord("press", "One Air");

        // Assert
        record.ShouldBeNull();
    }

    private static TrainingSessionLog Log(string exerciseId, string? gym, int day, string weight) => new()
    {
        Id = $"history-{day}", SessionId = "upper", Date = new DateTimeOffset(2026, 10, day, 12, 0, 0, TimeSpan.Zero),
        Exercises = [new ExercisePerformance { ExerciseId = exerciseId, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = "8" }] }]
    };
    private static WorkoutStateService Create(PerformanceData data) => new(new Repository(data), new Catalog(), new FakeTimeProvider());
    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press" }];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }
    private sealed class Repository(PerformanceData initial) : IPerformanceDataRepository
    {
        private PerformanceData _stored = initial;
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            _stored = data;
            return ValueTask.CompletedTask;
        }
    }
}
