using System.Text.Json;
using Performance.Infrastructure;

namespace Performance.Infrastructure.Tests;

public sealed class ReactLocalStorageMigrationAdapterTests
{
    [Fact]
    public void Import_preserves_history_exercise_id_and_gym_qualified_records()
    {
        var adapter = new ReactLocalStorageMigrationAdapter();
        var storage = new Dictionary<string, string?>
        {
            [ReactLocalStorageMigrationAdapter.CustomExercisesKey] = """[{"id":"rope-1","name":"Extension triceps corde","group":"Triceps","target":"4 × 10–15","rest":"1 min 30","sets":4,"tags":["Poulie"],"gyms":["One Air","Basic-Fit"],"link":"https://example.test/photo.jpg"}]""",
            [ReactLocalStorageMigrationAdapter.LogsKey] = """[{"id":"log-1","date":"2026-01-02T10:00:00Z","sessionId":"push","duration":51,"entries":{"rope-1@@One Air":[{"weight":"25","reps":"12","rir":"2"}]}}]""",
            ["future-app-key"] = "keep-me",
            ["training-theme"] = "dark"
        };

        var data = adapter.Import(storage);
        var exercise = Assert.Single(data.Exercises);
        Assert.Equal("rope-1", exercise.Id);
        Assert.Equal(new[] { "One Air", "Basic-Fit" }, exercise.Variants.Select(x => x.Gym));
        var log = Assert.Single(data.History);
        var performance = Assert.Single(log.Exercises);
        Assert.Equal("rope-1", performance.ExerciseId);
        Assert.Equal("One Air", performance.Gym);
        Assert.Equal("25", Assert.Single(performance.Sets).Weight);
        Assert.Equal("keep-me", data.ReactLocalStorage["future-app-key"]);

        var exported = adapter.Export(data);
        Assert.Equal("keep-me", exported["future-app-key"]);
        using var exportedLog = JsonDocument.Parse(exported[ReactLocalStorageMigrationAdapter.LogsKey]);
        Assert.True(exportedLog.RootElement[0]["entries"].TryGetProperty("rope-1@@One Air", out _));
    }

    [Fact]
    public void Backup_round_trip_retains_unknown_local_storage_payloads()
    {
        var adapter = new ReactLocalStorageMigrationAdapter();
        var imported = adapter.Import(new Dictionary<string, string?> { ["future-key"] = "opaque-value" });
        var backup = adapter.ExportBackup(imported);
        var restored = adapter.ImportBackup(backup);
        Assert.Equal(1, restored.SchemaVersion);
        Assert.Equal("opaque-value", restored.ReactLocalStorage["future-key"]);
        Assert.Equal("opaque-value", adapter.Export(restored)["future-key"]);
    }

    [Fact]
    public void ImportBackup_accepts_complete_react_v2_export()
    {
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

        var data = new ReactLocalStorageMigrationAdapter().ImportBackup(json);

        Assert.Equal("custom-1", Assert.Single(data.Exercises).Id);
        var performance = Assert.Single(Assert.Single(data.History).Exercises);
        Assert.Equal("pulldown", performance.ExerciseId);
        Assert.Equal("Basic-Fit", performance.Gym);
        Assert.Equal("Basic-Fit", data.Settings["training-workout-gym"]);
        Assert.Equal("false", data.Settings["training-one-air"]);
        Assert.Contains("pushup", data.HiddenExerciseIds);
        using var weekPlan = JsonDocument.Parse(data.ReactLocalStorage[ReactLocalStorageMigrationAdapter.WeekPlanKey]);
        Assert.Equal(new[] { "pa", "pb" }, weekPlan.RootElement.EnumerateArray().Select(x => x.GetString()));
    }
}
