using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Shouldly;
using Xunit;

namespace Performance.Infrastructure.IntegrationTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class ProgramAuthoringPersistenceTests
{
    [Fact]
    public async Task CreateProgramAsync_ReopenAndBackup_PreservesFavoritesWeekAndPersonalDraft()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-authoring-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "data.json");
        try
        {
            var service = Create(path);
            await service.InitializeAsync();
            await service.AddWeekSessionAsync("pa");
            await service.StartAsync(service.WeekSessions.Single(), "One Air");
            var exerciseId = service.Data.ActiveWorkout!.Session!.Exercises[0].ExerciseId;
            await service.UpdateSetAsync(exerciseId, 0, new() { Weight = "42,5", Reps = "10", Rir = "2" });
            var previous = service.Data;

            // Act
            (await service.CreateProgramAsync(Draft(), favorite: true)).IsSuccess.ShouldBeTrue();
            var created = service.Data.Programs.Single(program => program.Id.StartsWith("custom-program-", StringComparison.Ordinal));
            var reopened = Create(path);
            await reopened.InitializeAsync();
            var backup = new ReactLocalStorageMigrationAdapter().ExportBackup(reopened.Data);
            var restoredData = new ReactLocalStorageMigrationAdapter().ImportBackup(backup);
            var backupPath = Path.Combine(directory, "restored.json");
            await new JsonPerformanceDataRepository(new JsonFileDocumentStore(backupPath)).SaveAsync(restoredData);
            var restored = Create(backupPath);
            await restored.InitializeAsync();

            // Assert
            restored.FavoriteSessionIds.OrderBy(id => id).ShouldBe(created.Sessions.Select(session => session.Id).Order());
            restored.Programs.Single(program => program.Id == created.Id).Sessions.Select(session => session.Name).ShouldBe(["Push perso", "Upper perso"]);
            restored.WeekSessions.Single().Id.ShouldBe(previous.Programs.Single().Sessions.Single().Id);
            restored.Data.ActiveWorkout!.Session!.Id.ShouldBe(previous.ActiveWorkout!.Session!.Id);
            restored.GetSets(exerciseId)[0].ShouldBe(new TrainingSet { Weight = "42,5", Reps = "10", Rir = "2" });
            restored.Data.ActiveWorkout.StartedAt.ShouldBe(previous.ActiveWorkout.StartedAt);
            File.Exists(path + ".tmp").ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateProgramAsync_ExportAndImportCopy_ExcludesFavoritesAndStartsWithPlannedSets()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "performance-authoring-share-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = Create(Path.Combine(directory, "source.json"));
            await service.CreateProgramAsync(Draft(), favorite: true);
            var program = service.Data.Programs.Single();
            var codec = new JsonProgramPackageCodec();
            var json = new ExportWorkoutProgramsUseCase(codec).Execute([program], service.AllExercises);
            var destination = Create(Path.Combine(directory, "destination.json"));

            // Act
            (await destination.ImportProgramsAsync(new ImportWorkoutProgramsUseCase(codec).Execute(json))).IsSuccess.ShouldBeTrue();
            var copy = destination.Data.Programs.Single();
            (await destination.AddWeekSessionAsync(copy.Sessions[0].Id)).IsSuccess.ShouldBeTrue();
            (await destination.StartAsync(copy.Sessions[0], copy.Gym!)).IsSuccess.ShouldBeTrue();
            var reopened = Create(Path.Combine(directory, "destination.json"));
            await reopened.InitializeAsync();

            // Assert
            json.ShouldNotContain("training-favorite-sessions");
            json.ShouldNotContain("settings");
            destination.FavoriteSessionIds.ShouldBeEmpty();
            copy.Id.ShouldNotBe(program.Id);
            copy.Gym.ShouldBe("One Air");
            reopened.WeekSessions.Single().Id.ShouldBe(copy.Sessions[0].Id);
            reopened.GetSets("rope").Count.ShouldBe(4);
            reopened.GetSets("rope").ShouldAllBe(set => set.Weight == "" && set.Reps == "" && set.Rir == "");
            reopened.GetRestSeconds("rope").ShouldBe(90);
            reopened.Data.History.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static WorkoutStateService Create(string path) => new(
        new JsonPerformanceDataRepository(new JsonFileDocumentStore(path)), new DefaultTrainingCatalog(), TimeProvider.System);

    private static WorkoutProgram Draft() => new()
    {
        Id = "ignored", Name = "Mon programme", Gym = "One Air",
        Sessions = [Session("Push perso"), Session("Upper perso")]
    };

    private static WorkoutSession Session(string name) => new()
    {
        Id = "duplicate-draft-id", Name = name,
        Exercises = [new() { ExerciseId = "rope", Sets = 4, RepTarget = "10", Rest = "1:30" }]
    };
}
