using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.Tests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutCatalogCacheTests
{
    [Fact]
    public async Task SaveExerciseAsync_AfterCatalogRead_ShouldRefreshCustomOverride()
    {
        // Arrange
        var service = CreateService();
        service.AllExercises.ShouldHaveSingleItem().Name.ShouldBe("Press");
        service.Exercises.ShouldHaveSingleItem().Name.ShouldBe("Press");

        // Act
        await service.SaveExerciseAsync(new Exercise { Id = "press", Name = "Personal press", Sets = 3 });

        // Assert
        service.AllExercises.ShouldHaveSingleItem().Name.ShouldBe("Personal press");
        service.Exercises.ShouldHaveSingleItem().Sets.ShouldBe(3);
    }

    [Fact]
    public async Task ReplaceDataAsync_AfterVisibleExercisesRead_ShouldRefreshHiddenIds()
    {
        // Arrange
        var service = CreateService();
        service.Exercises.ShouldHaveSingleItem();

        // Act
        await service.ReplaceDataAsync(new PerformanceData { HiddenExerciseIds = ["press"] });

        // Assert
        service.Exercises.ShouldBeEmpty();
        service.AllExercises.ShouldHaveSingleItem().Id.ShouldBe("press");
    }

    [Fact]
    public async Task ReplaceDataAsync_AfterProgramsRead_ShouldRefreshProgramOverride()
    {
        // Arrange
        var service = CreateService();
        service.Programs.ShouldHaveSingleItem().Name.ShouldBe("Upper");

        // Act
        await service.ReplaceDataAsync(new PerformanceData
        {
            Programs = [new WorkoutProgram { Id = "upper", Name = "Custom upper" }, new WorkoutProgram { Id = "extra", Name = "Extra" }]
        });

        // Assert
        service.Programs.Count.ShouldBe(2);
        service.Programs.Single(program => program.Id == "upper").Name.ShouldBe("Custom upper");
    }

    [Fact]
    public async Task ReplaceDataAsync_AfterExerciseRead_ShouldRefreshImportedDefinitions()
    {
        // Arrange
        var service = CreateService();
        service.AllExercises.ShouldHaveSingleItem();
        service.Exercises.ShouldHaveSingleItem();

        // Act
        await service.ReplaceDataAsync(new PerformanceData
        {
            Exercises = [new Exercise { Id = "press", Name = "Imported press" }, new Exercise { Id = "row", Name = "Row" }]
        });

        // Assert
        service.AllExercises.Count.ShouldBe(2);
        service.Exercises.Single(exercise => exercise.Id == "press").Name.ShouldBe("Imported press");
    }

    [Fact]
    public async Task SaveExerciseAsync_AfterAliasHistoryRead_ShouldRefreshNameAliases()
    {
        // Arrange
        var service = CreateService();
        var session = new WorkoutSession { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] };
        await service.ReplaceDataAsync(new PerformanceData
        {
            Exercises = [new Exercise { Id = "legacy-press", Name = "PRÉSS" }],
            History = [new TrainingSessionLog
            {
                Id = "old", SessionId = "upper", Date = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
                Exercises = [new ExercisePerformance { ExerciseId = "legacy-press", Gym = "One Air", Sets = [new TrainingSet { Weight = "50", Reps = "8" }] }]
            }]
        });
        await service.StartAsync(session, "One Air");
        service.GetSets("press")[0].Weight.ShouldBe("50");
        await service.CancelAsync();

        // Act
        await service.SaveExerciseAsync(new Exercise { Id = "press", Name = "Different movement" });
        await service.StartAsync(session, "One Air");

        // Assert
        service.GetSets("press")[0].Weight.ShouldBe("");
    }

    private static WorkoutStateService CreateService() => new(new FakeRepository(), new FakeCatalog(), new FakeTimeProvider());

    private sealed class FakeCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press" }];
        public IReadOnlyList<WorkoutProgram> Programs => [new WorkoutProgram { Id = "upper", Name = "Upper" }];
    }

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        private PerformanceData _stored = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            _stored = data;
            return ValueTask.CompletedTask;
        }
    }
}
