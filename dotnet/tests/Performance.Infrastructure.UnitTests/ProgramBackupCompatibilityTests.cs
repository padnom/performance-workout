using System.Text.Json;
using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(ReactLocalStorageMigrationAdapter))]
public sealed class ProgramBackupCompatibilityTests
{
    [Fact]
    public void ImportBackup_OldSessionWithoutOptionalExercises_PreservesEmptyDefault()
    {
        // Arrange
        var json = """{"schemaVersion":1,"exercises":[],"history":[],"programs":[{"id":"custom","name":"Custom","sessions":[{"id":"a","name":"A","exercises":[]}]}]}""";

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.Programs.Single().Sessions.Single().OptionalExercises.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[{\"exerciseId\":\"abs\",\"sets\":0}]")]
    [InlineData("[{\"exerciseId\":\"abs\"},{\"exerciseId\":\"abs\"}]")]
    public void ImportBackup_InvalidOptionalPlans_RejectsBeforePublication(string optional)
    {
        // Arrange
        var json = $$"""{"schemaVersion":1,"exercises":[],"history":[],"programs":[{"id":"custom","name":"Custom","sessions":[{"id":"a","name":"A","exercises":[],"optionalExercises":{{optional}}}]}]}""";

        // Act
        var import = () => new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        import.ShouldThrow<JsonException>();
    }

    [Fact]
    public void ExportBackup_ManuallyAddedPendingOptionalExercise_RoundTripsWithoutDuplicatingPlans()
    {
        // Arrange
        var plan = new WorkoutExercise { ExerciseId = "abs", Sets = 2 };
        var data = new PerformanceData { ActiveWorkout = new() { Session = new() { Id = "push", Name = "Push", Exercises = [plan], OptionalExercises = [plan] } } };
        var adapter = new ReactLocalStorageMigrationAdapter();

        // Act
        var restored = adapter.ImportBackup(adapter.ExportBackup(data));

        // Assert
        restored.ActiveWorkout!.Session!.Exercises.Single().ExerciseId.ShouldBe("abs");
        restored.ActiveWorkout.Session.OptionalExercises.Single().ExerciseId.ShouldBe("abs");
    }

    [Fact]
    public void ExportBackup_GymAndOptionalPlans_RoundTripsWithoutChangingRir()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();
        var adapter = new ReactLocalStorageMigrationAdapter(catalog);
        var session = new WorkoutSession { Id = "custom", Name = "Custom", OptionalExercises = [new() { ExerciseId = "abs", Sets = 2, RepTarget = "10–15", Rest = "60 s" }] };
        var data = new PerformanceData
        {
            Programs = [new() { Id = "custom", Name = "Custom", Gym = "One Air", Sessions = [session] }],
            ActiveWorkout = new() { Session = session, Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["old@@One Air"] = [new() { Rir = "2" }] } }
        };

        // Act
        var restored = adapter.ImportBackup(adapter.ExportBackup(data));

        // Assert
        restored.Programs.Single().Gym.ShouldBe("One Air");
        restored.ActiveWorkout.ShouldNotBeNull().Session.ShouldNotBeNull().OptionalExercises.Single().Sets.ShouldBe(2);
        restored.ActiveWorkout.Draft["old@@One Air"].Single().Rir.ShouldBe("2");
    }
}
