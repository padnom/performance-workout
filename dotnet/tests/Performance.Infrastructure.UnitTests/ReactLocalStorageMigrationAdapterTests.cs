using JetBrains.Annotations;
using System.Text.Json;
using Performance.Infrastructure;
using Performance.Domain;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(ReactLocalStorageMigrationAdapter))]
public sealed class ReactLocalStorageMigrationAdapterTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":3,\"logs\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"logs\":{}}")]
    [InlineData("{\"schemaVersion\":\"2\",\"logs\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"logs\":[null,{}]}")]
    [InlineData("{\"schemaVersion\":2,\"logs\":[],\"customExercises\":{}}")]
    [InlineData("{\"schemaVersion\":2,\"logs\":[],\"customExercises\":[{\"id\":\"x\"}]}")]
    [InlineData("{\"schemaVersion\":2,\"logs\":[],\"activeWorkout\":{\"active\":null}}")]
    [InlineData("{\"schemaVersion\":1,\"exercises\":[],\"history\":[],\"programs\":null}")]
    [InlineData("{\"schemaVersion\":1,\"exercises\":[],\"history\":[],\"settings\":null}")]
    [InlineData("{\"schemaVersion\":1,\"exercises\":[null],\"history\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"exercises\":[],\"history\":[{\"id\":\"log\",\"sessionId\":\"push\",\"exercises\":null}]}")]
    public void ImportBackup_UnrecognizedEnvelope_RejectsBeforeReplacingData(string json)
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();

        // Act
        var import = () => adapter.ImportBackup(json);

        // Assert
        import.ShouldThrow<JsonException>();
    }

    [Fact]
    public void ImportBackup_ActiveSnapshot_RetainsUnknownExerciseDefinitionAndSetOverrides()
    {
        // Arrange
        var json = """
        {"schemaVersion":2,"logs":[],"activeWorkout":{
          "active":{"id":"free","name":"Séance libre","exercises":[{
            "id":"old-custom","name":"Tirage personnel","group":"Dos",
            "sets":3,"target":"6–8","rest":"2 min","gym":"Basic-Fit"}]},
          "draft":{"old-custom":[{"weight":"42,5","reps":"8","rir":"2"}]},
          "started":1790935200000,"workoutGym":"Basic-Fit","exerciseGyms":{"old-custom":"Basic-Fit"}
        }}
        """;

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.Exercises.ShouldContain(exercise => exercise.Id == "old-custom" && exercise.Name == "Tirage personnel");
        data.ActiveWorkout.ShouldNotBeNull();
        data.ActiveWorkout.Session.ShouldNotBeNull();
        data.ActiveWorkout.Session.Exercises.Single().Sets.ShouldBe(3);
        data.ActiveWorkout.Draft["old-custom"].Single().Weight.ShouldBe("42,5");
    }

    [Fact]
    public void ImportBackup_WeekPlan_ResolvesOrderedCatalogSessions()
    {
        // Arrange
        var json = """{"schemaVersion":2,"logs":[],"weekPlan":["pa","pb","pa"]}""";

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.Programs.Single().Sessions.Select(session => session.Id).ShouldBe(new[] { "pa", "pb", "pa" });
        data.Programs.Single().Sessions.ShouldAllBe(session => session.Exercises.Count > 0);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"logs":[],"activeWorkout":{"active":{"id":"push","name":"Push","exercises":[{"id":"bench","name":"Développé couché"},{"id":"bench","name":"Développé couché"}]},"draft":{}}}""")]
    [InlineData("""{"schemaVersion":1,"exercises":[],"history":[],"activeWorkout":{"session":{"id":"push","name":"Push","exercises":[{"exerciseId":"bench"},{"exerciseId":"bench"}]},"draft":{}}}""")]
    public void ImportBackup_DuplicateActiveExerciseIds_RejectsUnusableSession(string json)
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();

        // Act
        var import = () => adapter.ImportBackup(json);

        // Assert
        import.ShouldThrow<JsonException>();
    }

    [Theory]
    [InlineData("rest")]
    [InlineData("repTarget")]
    [InlineData("muscleGroup")]
    public void ImportBackup_NullRequiredExerciseText_RejectsUnusableExercise(string field)
    {
        // Arrange
        var json = $$"""{"schemaVersion":1,"exercises":[{"id":"bench","name":"Bench","{{field}}":null}],"history":[]}""";
        var adapter = new ReactLocalStorageMigrationAdapter();

        // Act
        var import = () => adapter.ImportBackup(json);

        // Assert
        import.ShouldThrow<JsonException>();
    }

    [Fact]
    public void Import_WithGymQualifiedHistory_ShouldPreserveExerciseIdsAndGyms()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var storage = new Dictionary<string, string?>
        {
            [ReactLocalStorageMigrationAdapter.CustomExercisesKey] = """[{"id":"rope-1","name":"Extension triceps corde","group":"Triceps","target":"4 × 10–15","rest":"1 min 30","sets":4,"tags":["Poulie"],"gyms":["One Air","Basic-Fit"],"link":"https://example.test/photo.jpg"}]""",
            [ReactLocalStorageMigrationAdapter.LogsKey] = """[{"id":"log-1","date":"2026-01-02T10:00:00Z","sessionId":"push","duration":51,"entries":{"rope-1@@One Air":[{"weight":"25","reps":"12","rir":"2"}]}}]""",
            ["future-app-key"] = "keep-me",
            ["training-theme"] = "dark"
        };

        // Act
        var data = adapter.Import(storage);
        var exported = adapter.Export(data);

        // Assert
        var exercise = data.Exercises.ShouldHaveSingleItem();
        exercise.Id.ShouldBe("rope-1");
        exercise.Variants.Select(x => x.Gym).ShouldBe(new[] { "One Air", "Basic-Fit" });
        var log = data.History.ShouldHaveSingleItem();
        var performance = log.Exercises.ShouldHaveSingleItem();
        performance.ExerciseId.ShouldBe("rope-1");
        performance.Gym.ShouldBe("One Air");
        performance.Sets.ShouldHaveSingleItem().Weight.ShouldBe("25");
        data.ReactLocalStorage["future-app-key"].ShouldBe("keep-me");
        exported["future-app-key"].ShouldBe("keep-me");
        using var exportedLog = JsonDocument.Parse(exported[ReactLocalStorageMigrationAdapter.LogsKey]);
        exportedLog.RootElement[0].GetProperty("entries").TryGetProperty("rope-1@@One Air", out _).ShouldBeTrue();
    }

    [Fact]
    public void ExportBackup_WithUnknownStoragePayload_ShouldRetainPayloadOnImport()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var imported = adapter.Import(new Dictionary<string, string?> { ["future-key"] = "opaque-value" });

        // Act
        var backup = adapter.ExportBackup(imported);
        var restored = adapter.ImportBackup(backup);

        // Assert
        restored.SchemaVersion.ShouldBe(1);
        restored.ReactLocalStorage["future-key"].ShouldBe("opaque-value");
        adapter.Export(restored)["future-key"].ShouldBe("opaque-value");
    }

    [Fact]
    public void ImportBackup_WithCompleteReactV2Export_ShouldRestoreData()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 2,
          "exportedAt": "2026-09-26T08:00:00Z",
          "logs": [{
            "id": "log-2",
            "date": "2026-09-25T18:00:00Z",
            "sessionId": "pull",
            "duration": 48,
            "entries": { "pulldown@@Basic-Fit": [{ "weight": "55", "reps": "10", "rir": "1" }] }
          }],
          "customExercises": [{
            "id": "custom-1", "name": "Tirage test", "group": "Dos", "sets": 4,
            "target": "8-12", "rest": "2 min", "gyms": ["Basic-Fit"]
          }],
          "hiddenExerciseIds": ["pushup"],
          "weekPlan": ["pa", "pb"],
          "settings": { "theme": "dark", "workoutGym": "Basic-Fit", "oneAir": false },
          "activeWorkout": null
        }
        """;

        // Act
        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        // Assert
        data.Exercises.ShouldHaveSingleItem().Id.ShouldBe("custom-1");
        var performance = data.History.ShouldHaveSingleItem().Exercises.ShouldHaveSingleItem();
        performance.ExerciseId.ShouldBe("pulldown");
        performance.Gym.ShouldBe("Basic-Fit");
        data.Settings["training-workout-gym"].ShouldBe("Basic-Fit");
        data.Settings["training-one-air"].ShouldBe("false");
        data.HiddenExerciseIds.ShouldContain("pushup");
        using var weekPlan = JsonDocument.Parse(data.ReactLocalStorage[ReactLocalStorageMigrationAdapter.WeekPlanKey]);
        weekPlan.RootElement.EnumerateArray().Select(x => x.GetString()).ShouldBe(new[] { "pa", "pb" });
    }
}
