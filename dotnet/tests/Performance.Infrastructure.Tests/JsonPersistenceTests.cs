using Performance.Application;
using Performance.Domain;
using Shouldly;

namespace Performance.Infrastructure.Tests;

public sealed class JsonPersistenceTests
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
}
