using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.Infrastructure.IntegrationTests;

[TestSubject(typeof(JsonPerformanceDataRepository))]
public sealed class JsonPerformanceDataRepositoryTest
{
    [Fact]
    public async Task SaveAsync_ReopenRepository_RetainsWorkoutAndGymHistory()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data.json");
        var data = new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = new WorkoutSession { Id = "pull", Name = "Pull" },
                DefaultGym = "Basic-Fit",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>>
                {
                    ["row@@Basic-Fit"] = [new TrainingSet { Weight = "42,5", Reps = "10", Rir = "2" }]
                }
            },
            History = [new TrainingSessionLog
            {
                Id = "old-log", SessionId = "pull", Date = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero),
                Exercises = [new ExercisePerformance { ExerciseId = "row", Gym = "One Air", Sets = [new TrainingSet { Weight = "55", Reps = "8" }] }]
            }],
            ReactLocalStorage = new Dictionary<string, string> { ["future-key"] = "opaque-data" }
        };

        try
        {
            // Act
            await new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)).SaveAsync(data);
            var restored = await new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)).LoadAsync();

            // Assert
            restored.ActiveWorkout.ShouldNotBeNull();
            restored.ActiveWorkout.Draft["row@@Basic-Fit"].Single().Weight.ShouldBe("42,5");
            restored.History.Single().Exercises.Single().Gym.ShouldBe("One Air");
            restored.ReactLocalStorage["future-key"].ShouldBe("opaque-data");
            File.Exists(path + ".tmp").ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_WithRunningTimer_ShouldRestoreAfterRepositoryRecreation()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-rest-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data.json");
        var deadline = DateTimeOffset.FromUnixTimeMilliseconds(1790935320000);
        var data = new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = new WorkoutSession { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] },
                RestDurations = new Dictionary<string, int> { ["press"] = 150 }, RestEnd = deadline, RestExerciseId = "press"
            }
        };

        try
        {
            // Act
            await new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)).SaveAsync(data);
            var restored = await new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)).LoadAsync();

            // Assert
            restored.ActiveWorkout!.RestEnd.ShouldBe(deadline);
            restored.ActiveWorkout.RestExerciseId.ShouldBe("press");
            restored.ActiveWorkout.RestDurations["press"].ShouldBe(150);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WeekMutation_WithJsonRepositoryAndNativeBackup_ShouldRestoreReorderedRepeatedWeek()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-week-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data.json");
        var adapter = new ReactLocalStorageMigrationAdapter();
        var initial = adapter.Import(new Dictionary<string, string?> { [ReactLocalStorageMigrationAdapter.WeekPlanKey] = "[\"pa\",\"pa\",\"pua\"]" });
        try
        {
            var repository = new JsonPerformanceDataRepository(new JsonFileDocumentStore(path));
            await repository.SaveAsync(initial);
            var service = Create(repository);

            // Act
            await service.MoveWeekSessionAsync(2, -1);
            var reopened = Create(new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)));
            await reopened.InitializeAsync();
            var restored = adapter.ImportBackup(adapter.ExportBackup(reopened.Data));

            // Assert
            reopened.WeekSessions.Select(session => session.Id).ShouldBe(["pa", "pua", "pa"]);
            restored.Programs.Single().Sessions.Select(session => session.Id).ShouldBe(["pa", "pua", "pa"]);
            restored.Programs[0].Sessions[0].Exercises.ShouldNotBeEmpty();
            File.Exists(path + ".tmp").ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImportPrograms_ReopenFileStore_PreservesWeekAndOptionalPlansWithoutPersonalData()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-program-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data.json");
        var catalog = new DefaultTrainingCatalog();
        var program = catalog.Programs.Single(item => item.Id == "gym-one-air");
        var codec = new JsonProgramPackageCodec();
        var package = new ImportWorkoutProgramsUseCase(codec).Execute(new ExportWorkoutProgramsUseCase(codec).Execute([program], catalog.Exercises));
        try
        {
            var repository = new JsonPerformanceDataRepository(new JsonFileDocumentStore(path));
            var service = Create(repository);
            await service.InitializeAsync();
            await service.AddWeekSessionAsync("pa");
            await service.StartAsync(program.Sessions[0], "One Air");
            var previousSession = service.Data.ActiveWorkout!.Session!.Id;

            // Act
            (await service.ImportProgramsAsync(package)).IsSuccess.ShouldBeTrue();
            var reopened = Create(new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)));
            await reopened.InitializeAsync();
            (await reopened.AddOptionalExercisesAsync()).IsSuccess.ShouldBeTrue();
            var restored = Create(new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)));
            await restored.InitializeAsync();

            // Assert
            restored.WeekSessions.Single().Id.ShouldBe("pa");
            restored.Data.History.ShouldBeEmpty();
            restored.Data.ActiveWorkout!.Session!.Id.ShouldBe(previousSession);
            restored.Data.ActiveWorkout.Session.OptionalExercises.ShouldBeEmpty();
            restored.Data.ActiveWorkout.Session.Exercises.Count.ShouldBe(program.Sessions[0].Exercises.Count + 2);
            restored.Data.Programs.Single(item => item.Id.StartsWith("imported-program-", StringComparison.Ordinal))
                .Sessions.ShouldAllBe(session => session.OptionalExercises.Count == 2);
            File.Exists(path + ".tmp").ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static WorkoutStateService Create(IPerformanceDataRepository repository) =>
        new(repository, new DefaultTrainingCatalog(), TimeProvider.System);
}
