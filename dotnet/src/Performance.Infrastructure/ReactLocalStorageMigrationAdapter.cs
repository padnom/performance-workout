using System.Globalization;
using System.Text.Json;
using Performance.Application;
using Performance.Domain;

namespace Performance.Infrastructure;

/// <summary>Compatibility bridge for the React PWA's known localStorage keys plus opaque preservation of unknown keys.</summary>
public sealed class ReactLocalStorageMigrationAdapter : IReactLocalStorageAdapter
{
    public const string LogsKey = "jo-strength-logs";
    public const string ActiveKey = "jo-strength-active";
    public const string WeekPlanKey = "training-week-plan";
    public const string CustomExercisesKey = "training-custom-exercises";
    public const string HiddenExercisesKey = "training-hidden-exercises";
    private static readonly HashSet<string> SettingKeys = ["training-theme", "training-one-air", "training-workout-gym"];
    private readonly JsonSerializerOptions _json = PersistenceJson.Options();

    public PerformanceData Import(IReadOnlyDictionary<string, string?> localStorage)
    {
        ArgumentNullException.ThrowIfNull(localStorage);
        var raw = localStorage.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value!, StringComparer.Ordinal);
        return new PerformanceData
        {
            ReactLocalStorage = raw,
            Settings = raw.Where(x => SettingKeys.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            Exercises = ParseExercises(raw.GetValueOrDefault(CustomExercisesKey)),
            History = ParseHistory(raw.GetValueOrDefault(LogsKey)),
            ActiveWorkout = ParseActive(raw.GetValueOrDefault(ActiveKey)),
            Programs = ParseWeekPlan(raw.GetValueOrDefault(WeekPlanKey)),
            HiddenExerciseIds = ParseStringArray(raw.GetValueOrDefault(HiddenExercisesKey))
        };
    }

    public IReadOnlyDictionary<string, string> Export(PerformanceData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var output = data.ReactLocalStorage.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var setting in data.Settings) output[setting.Key] = setting.Value;
        if (data.Exercises.Any(x => x.IsCustom) || output.ContainsKey(CustomExercisesKey))
            output[CustomExercisesKey] = JsonSerializer.Serialize(data.Exercises.Where(x => x.IsCustom).Select(ToReactExercise), _json);
        if (data.History.Count > 0 || output.ContainsKey(LogsKey))
            output[LogsKey] = JsonSerializer.Serialize(data.History.Select(ToReactLog), _json);
        if (data.HiddenExerciseIds.Count > 0 || output.ContainsKey(HiddenExercisesKey))
            output[HiddenExercisesKey] = JsonSerializer.Serialize(data.HiddenExerciseIds, _json);
        return output;
    }

    public string ExportBackup(PerformanceData data) => JsonSerializer.Serialize(data with { SchemaVersion = 1, ExportedAt = DateTimeOffset.UtcNow }, _json);

    public PerformanceData ImportBackup(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Historical exports from the React app contained the logs array only.
        if (root.ValueKind == JsonValueKind.Array)
            return Import(new Dictionary<string, string?> { [LogsKey] = root.GetRawText() });

        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Sauvegarde vide ou invalide.");

        // Version 2 is the complete backup produced by the React PWA.
        if (Property(root, "schemaVersion", out var schema) && schema.TryGetInt32(out var version) && version >= 2)
            return ImportReactBackup(root);

        var data = JsonSerializer.Deserialize<PerformanceData>(json, _json)
            ?? throw new JsonException("Sauvegarde vide ou invalide.");
        return data with { SchemaVersion = 1 };
    }

    private PerformanceData ImportReactBackup(JsonElement root)
    {
        var storage = new Dictionary<string, string?>(StringComparer.Ordinal);
        CopyJson(root, "logs", LogsKey, storage);
        CopyJson(root, "customExercises", CustomExercisesKey, storage);
        CopyJson(root, "hiddenExerciseIds", HiddenExercisesKey, storage);
        CopyJson(root, "weekPlan", WeekPlanKey, storage);
        CopyJson(root, "activeWorkout", ActiveKey, storage, ignoreNull: true);

        if (Property(root, "settings", out var settings) && settings.ValueKind == JsonValueKind.Object)
        {
            if (Property(settings, "theme", out var theme) && theme.ValueKind == JsonValueKind.String)
                storage["training-theme"] = theme.GetString();
            if (Property(settings, "workoutGym", out var gym) && gym.ValueKind == JsonValueKind.String)
                storage["training-workout-gym"] = gym.GetString();
            if (Property(settings, "oneAir", out var oneAir) && oneAir.ValueKind is JsonValueKind.True or JsonValueKind.False)
                storage["training-one-air"] = oneAir.GetBoolean().ToString().ToLowerInvariant();
        }

        return Import(storage);
    }

    private static void CopyJson(
        JsonElement source,
        string propertyName,
        string storageKey,
        IDictionary<string, string?> destination,
        bool ignoreNull = false)
    {
        if (!Property(source, propertyName, out var value)) return;
        if (ignoreNull && value.ValueKind == JsonValueKind.Null) return;
        destination[storageKey] = value.GetRawText();
    }

    private static IReadOnlyList<Exercise> ParseExercises(string? json)
    {
        if (!Array(json, out var list)) return [];
        var output = new List<Exercise>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = Text(item, "id"); var name = Text(item, "name");
            if (id.Length == 0 || name.Length == 0) continue;
            var gyms = TextArray(item, "gyms");
            if (gyms.Count == 0 && Text(item, "gym") is { Length: > 0 } gym) gyms.Add(gym);
            var link = NullableText(item, "link");
            output.Add(new Exercise
            {
                Id = id, Name = name, MuscleGroup = Text(item, "group"), RepTarget = Text(item, "target"),
                Rest = Text(item, "rest"), Sets = Number(item, "sets", 4), Link = link, Tags = TextArray(item, "tags"), IsCustom = true,
                Variants = gyms.Distinct(StringComparer.OrdinalIgnoreCase).Select(g => new GymVariant { Gym = g, PhotoUrl = link }).ToArray()
            });
        }
        return output;
    }

    private static IReadOnlyList<TrainingSessionLog> ParseHistory(string? json)
    {
        if (!Array(json, out var list)) return [];
        var output = new List<TrainingSessionLog>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Text(item, "id") is not { Length: > 0 } id) continue;
            var rows = new List<ExercisePerformance>();
            if (Property(item, "entries", out var entries) && entries.ValueKind == JsonValueKind.Object)
                foreach (var entry in entries.EnumerateObject())
                {
                    var (exerciseId, gym) = SplitId(entry.Name);
                    var sets = new List<TrainingSet>();
                    if (entry.Value.ValueKind == JsonValueKind.Array)
                        foreach (var row in entry.Value.EnumerateArray())
                            if (row.ValueKind == JsonValueKind.Object) sets.Add(new TrainingSet { Weight = Text(row, "weight"), Reps = Text(row, "reps"), Rir = Text(row, "rir") });
                    rows.Add(new ExercisePerformance { ExerciseId = exerciseId, Gym = gym, Sets = sets });
                }
            DateTimeOffset.TryParse(Text(item, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date);
            output.Add(new TrainingSessionLog { Id = id, Date = date, SessionId = Text(item, "sessionId"), DurationMinutes = Number(item, "duration", 0), Exercises = rows });
        }
        return output;
    }

    private static ActiveWorkout? ParseActive(string? json)
    {
        if (!Object(json, out var root) || !Property(root, "active", out var active) || active.ValueKind != JsonValueKind.Object) return null;
        var exercises = new List<WorkoutExercise>();
        if (Property(active, "exercises", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            var order = 0;
            foreach (var x in list.EnumerateArray()) if (Text(x, "id") is { Length: > 0 } id) exercises.Add(new WorkoutExercise { ExerciseId = id, Order = order++ });
        }
        var draft = new Dictionary<string, IReadOnlyList<TrainingSet>>(StringComparer.Ordinal);
        if (Property(root, "draft", out var draftObject) && draftObject.ValueKind == JsonValueKind.Object)
            foreach (var x in draftObject.EnumerateObject()) draft[x.Name] = ParseSets(x.Value);
        var gyms = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Property(root, "exerciseGyms", out var gymObject) && gymObject.ValueKind == JsonValueKind.Object)
            foreach (var x in gymObject.EnumerateObject()) if (x.Value.ValueKind == JsonValueKind.String) gyms[x.Name] = x.Value.GetString()!;
        var startedText = Text(root, "started");
        var started = long.TryParse(startedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMilliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
            : DateTimeOffset.TryParse(startedText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedStarted)
                ? parsedStarted : DateTimeOffset.MinValue;
        return new ActiveWorkout
        {
            Session = new WorkoutSession { Id = Text(active, "id"), Name = Text(active, "name"), Focus = NullableText(active, "kicker"), Exercises = exercises },
            StartedAt = started, Draft = draft, ExerciseGyms = gyms, DefaultGym = NullableText(root, "workoutGym")
        };
    }

    private static IReadOnlyList<WorkoutProgram> ParseWeekPlan(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.ValueKind == JsonValueKind.Null ? [] : [new WorkoutProgram { Id = "react-week-plan", Name = "Programme hebdomadaire" }]; }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyList<string> ParseStringArray(string? json) => Array(json, out var value) ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
    private static IReadOnlyList<TrainingSet> ParseSets(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new TrainingSet { Weight = Text(x, "weight"), Reps = Text(x, "reps"), Rir = Text(x, "rir") }).ToArray() : [];
    private static object ToReactExercise(Exercise x) => new { x.Id, x.Name, target = x.RepTarget, x.Rest, x.Sets, group = x.MuscleGroup, x.Link, x.Tags, gyms = x.Variants.Select(v => v.Gym).Distinct().ToArray(), gym = x.Variants.FirstOrDefault()?.Gym };
    private static object ToReactLog(TrainingSessionLog x) => new { x.Id, date = x.Date.ToString("O", CultureInfo.InvariantCulture), sessionId = x.SessionId, duration = x.DurationMinutes, entries = x.Exercises.ToDictionary(e => e.Gym is null ? e.ExerciseId : $"{e.ExerciseId}@@{e.Gym}", e => e.Sets.Select(s => new { s.Weight, s.Reps, s.Rir }).ToArray(), StringComparer.Ordinal) };
    private static (string Id, string? Gym) SplitId(string value) { var at = value.LastIndexOf("@@", StringComparison.Ordinal); return at < 0 ? (value, null) : (value[..at], value[(at + 2)..]); }
    private static bool Array(string? json, out JsonElement value) { value = default; try { using var doc = JsonDocument.Parse(json ?? ""); if (doc.RootElement.ValueKind != JsonValueKind.Array) return false; value = doc.RootElement.Clone(); return true; } catch (JsonException) { return false; } }
    private static bool Object(string? json, out JsonElement value) { value = default; try { using var doc = JsonDocument.Parse(json ?? ""); if (doc.RootElement.ValueKind != JsonValueKind.Object) return false; value = doc.RootElement.Clone(); return true; } catch (JsonException) { return false; } }
    private static bool Property(JsonElement obj, string name, out JsonElement value) { value = default; return obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out value); }
    private static string Text(JsonElement obj, string name) => Property(obj, name, out var x) ? x.ValueKind switch { JsonValueKind.String => x.GetString() ?? "", JsonValueKind.Number => x.GetRawText(), JsonValueKind.True => "true", JsonValueKind.False => "false", _ => "" } : "";
    private static string? NullableText(JsonElement obj, string name) => Property(obj, name, out var x) && x.ValueKind == JsonValueKind.String ? x.GetString() : null;
    private static int Number(JsonElement obj, string name, int fallback) => Property(obj, name, out var x) && x.TryGetInt32(out var n) ? n : fallback;
    private static List<string> TextArray(JsonElement obj, string name) => Property(obj, name, out var x) && x.ValueKind == JsonValueKind.Array ? x.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToList() : [];
}
