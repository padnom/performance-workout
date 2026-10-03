using System.Text.Json;
using Performance.Application;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;

namespace Performance.Infrastructure.Tests;

public sealed class RestMigrationTests
{
    [Fact]
    public void ImportBackup_WithReactRestSnapshot_ShouldRetainDeadlineExerciseAndDuration()
    {
        // Arrange
        var json = ReactBackup("\"restDurations\":{\"press\":150},\"restEnd\":1790935320000,\"restExerciseId\":\"press\"");

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.ActiveWorkout!.RestEnd.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1790935320000));
        data.ActiveWorkout.RestExerciseId.ShouldBe("press");
        data.ActiveWorkout.RestDurations["press"].ShouldBe(150);
    }

    [Fact]
    public void ImportBackup_WithReactSnapshotBeforeTimers_ShouldUseEmptyTimerDefaults()
    {
        // Arrange
        var json = ReactBackup("\"workoutGym\":\"One Air\"");

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.ActiveWorkout!.RestDurations.ShouldBeEmpty();
        data.ActiveWorkout.RestEnd.ShouldBeNull();
        data.ActiveWorkout.RestExerciseId.ShouldBeNull();
    }

    [Fact]
    public void ImportBackup_WithStoppedReactTimer_ShouldAcceptNullDeadlineAndRetainedExercise()
    {
        // Arrange
        var json = ReactBackup("\"restDurations\":{\"press\":90},\"restEnd\":null,\"restExerciseId\":\"press\"");

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.ActiveWorkout!.RestEnd.ShouldBeNull();
        data.ActiveWorkout.RestExerciseId.ShouldBe("press");
    }

    [Theory]
    [InlineData("\"restDurations\":null")]
    [InlineData("\"restDurations\":[]")]
    [InlineData("\"restDurations\":{\"press\":0}")]
    [InlineData("\"restDurations\":{\"press\":601}")]
    [InlineData("\"restDurations\":{\"press\":\"90\"}")]
    [InlineData("\"restDurations\":{\"press\":90.5}")]
    [InlineData("\"restDurations\":{\"\":90}")]
    [InlineData("\"restEnd\":\"1790935320000\"")]
    [InlineData("\"restEnd\":253402300800000")]
    [InlineData("\"restEnd\":1.5")]
    [InlineData("\"restExerciseId\":0")]
    [InlineData("\"restExerciseId\":\"\"")]
    public void ImportBackup_WithMalformedReactRestField_ShouldReject(string fields)
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();

        // Act
        var import = () => adapter.ImportBackup(ReactBackup(fields));

        // Assert
        import.ShouldThrow<JsonException>();
    }

    [Fact]
    public void ExportBackup_WithNativeRunningTimer_ShouldRoundTripExactSnapshot()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var deadline = DateTimeOffset.FromUnixTimeMilliseconds(1790935320000);
        var data = new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = new WorkoutSession { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] },
                RestDurations = new Dictionary<string, int> { ["press"] = 150 }, RestEnd = deadline, RestExerciseId = "press"
            }
        };

        // Act
        var restored = adapter.ImportBackup(adapter.ExportBackup(data));

        // Assert
        restored.ActiveWorkout!.RestEnd.ShouldBe(deadline);
        restored.ActiveWorkout.RestExerciseId.ShouldBe("press");
        restored.ActiveWorkout.RestDurations["press"].ShouldBe(150);
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

    [Theory]
    [InlineData("\"restDurations\":null")]
    [InlineData("\"restDurations\":{\"press\":601}")]
    [InlineData("\"restDurations\":{\"press\":0}")]
    [InlineData("\"restExerciseId\":\"\"")]
    public void ImportBackup_WithMalformedNativeRestField_ShouldReject(string fields)
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var json = $$$"""{"schemaVersion":1,"exercises":[],"history":[],"activeWorkout":{"session":{"id":"upper","name":"Upper","exercises":[]},{{{fields}}}}}""";

        // Act
        var import = () => adapter.ImportBackup(json);

        // Assert
        import.ShouldThrow<JsonException>();
    }

    private static string ReactBackup(string restFields) => $$$"""
    {"schemaVersion":2,"logs":[],"activeWorkout":{
      "active":{"id":"upper","name":"Upper","exercises":[{"id":"press","name":"Press","rest":"2 min"}]},
      "draft":{},"started":1790935200000,{{{restFields}}}
    }}
    """;
}
