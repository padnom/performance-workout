using System.Globalization;
using System.Text.Json;
using Performance.Application;
using Performance.Domain;

namespace Performance.Infrastructure;

internal static class BackupValidation
{
    public static void ValidateReact(JsonElement root)
    {
        ValidateLogs(root.GetProperty("logs"));
        ValidateOptionalArray(root, "customExercises", ValidateReactExercise);
        ValidateOptionalArray(root, "hiddenExerciseIds", ValidateString);
        ValidateOptionalArray(root, "weekPlan", ValidateString);
        ValidateOptionalObject(root, "settings", ValidateSettings);
        if (root.TryGetProperty("activeWorkout", out var active) && active.ValueKind != JsonValueKind.Null)
            ValidateReactActive(active);
    }

    public static void ValidateLogs(JsonElement logs)
    {
        Ensure(logs.ValueKind == JsonValueKind.Array);
        foreach (var log in logs.EnumerateArray()) ValidateReactLog(log);
    }

    public static void ValidateNative(PerformanceData data)
    {
        Ensure(data.Exercises is not null && data.Exercises.All(ValidExercise));
        Ensure(data.Programs is not null && data.Programs.All(ValidProgram));
        Ensure(data.History is not null && data.History.All(ValidLog));
        Ensure(data.HiddenExerciseIds is not null && data.HiddenExerciseIds.All(Nonempty));
        Ensure(data.Settings is not null && data.Settings.All(pair => Nonempty(pair.Key) && pair.Value is not null));
        Ensure(data.ReactLocalStorage is not null && data.ReactLocalStorage.All(pair => Nonempty(pair.Key) && pair.Value is not null));
        Ensure(data.ActiveWorkout is null || ValidActive(data.ActiveWorkout));
    }

    private static void ValidateReactLog(JsonElement log)
    {
        RequireText(log, "id");
        RequireText(log, "sessionId");
        RequireText(log, "date");
        Ensure(DateTimeOffset.TryParse(log.GetProperty("date").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _));
        OptionalInteger(log, "duration", 0, int.MaxValue);
        var entries = Require(log, "entries", JsonValueKind.Object);
        foreach (var entry in entries.EnumerateObject()) ValidateRows(entry.Value);
    }

    private static void ValidateReactExercise(JsonElement exercise)
    {
        RequireText(exercise, "id");
        RequireText(exercise, "name");
        OptionalInteger(exercise, "sets", 1, 20);
        ValidateOptionalArray(exercise, "gyms", ValidateString);
        ValidateOptionalArray(exercise, "tags", ValidateString);
        foreach (var name in new[] { "group", "target", "rest", "gym", "alt", "link" }) OptionalText(exercise, name);
    }

    private static void ValidateReactActive(JsonElement root)
    {
        var active = Require(root, "active", JsonValueKind.Object);
        RequireText(active, "id");
        RequireText(active, "name");
        var exercises = Require(active, "exercises", JsonValueKind.Array);
        foreach (var exercise in exercises.EnumerateArray()) ValidateReactExercise(exercise);
        Ensure(exercises.EnumerateArray().Select(exercise => exercise.GetProperty("id").GetString())
            .Distinct(StringComparer.Ordinal).Count() == exercises.GetArrayLength());
        var draft = Require(root, "draft", JsonValueKind.Object);
        foreach (var entry in draft.EnumerateObject()) ValidateRows(entry.Value);
        ValidateOptionalObject(root, "exerciseGyms", ValidateTextDictionary);
        OptionalText(root, "workoutGym");
        ValidateStartedAt(root);
        ValidateRest(root);
    }

    private static void ValidateRest(JsonElement root)
    {
        ValidateOptionalObject(root, "restDurations", dictionary =>
        {
            foreach (var entry in dictionary.EnumerateObject())
            {
                Ensure(Nonempty(entry.Name));
                Ensure(entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var seconds) && seconds is >= 15 and <= 600);
            }
        });
        if (root.TryGetProperty("restExerciseId", out var exercise) && exercise.ValueKind != JsonValueKind.Null)
            ValidateString(exercise);
        if (!root.TryGetProperty("restEnd", out var end) || end.ValueKind == JsonValueKind.Null) return;
        Ensure(end.ValueKind == JsonValueKind.Number && end.TryGetInt64(out var deadline)
            && deadline is >= -62135596800000 and <= 253402300799999);
        Ensure(root.TryGetProperty("restExerciseId", out var restExercise) && restExercise.ValueKind == JsonValueKind.String && Nonempty(restExercise.GetString()));
    }

    private static void ValidateStartedAt(JsonElement root)
    {
        if (!root.TryGetProperty("started", out var started)) return;
        if (started.ValueKind == JsonValueKind.Number)
        {
            Ensure(started.TryGetInt64(out var value) && value >= -62135596800000 && value <= 253402300799999);
            return;
        }
        Ensure(started.ValueKind == JsonValueKind.String);
        Ensure(DateTimeOffset.TryParse(started.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _));
    }

    private static void ValidateRows(JsonElement rows)
    {
        Ensure(rows.ValueKind == JsonValueKind.Array);
        foreach (var row in rows.EnumerateArray()) ValidateReactSet(row);
    }

    private static void ValidateReactSet(JsonElement row)
    {
        Ensure(row.ValueKind == JsonValueKind.Object);
        foreach (var name in new[] { "weight", "reps", "rir" }) OptionalText(row, name);
    }

    private static void ValidateSettings(JsonElement settings)
    {
        OptionalText(settings, "theme");
        OptionalText(settings, "workoutGym");
        if (settings.TryGetProperty("oneAir", out var oneAir)) Ensure(oneAir.ValueKind is JsonValueKind.True or JsonValueKind.False);
    }

    private static void ValidateTextDictionary(JsonElement dictionary)
    {
        foreach (var value in dictionary.EnumerateObject()) ValidateString(value.Value);
    }

    private static void ValidateOptionalArray(JsonElement root, string name, Action<JsonElement> validate)
    {
        if (!root.TryGetProperty(name, out var array)) return;
        Ensure(array.ValueKind == JsonValueKind.Array);
        foreach (var item in array.EnumerateArray()) validate(item);
    }

    private static void ValidateOptionalObject(JsonElement root, string name, Action<JsonElement> validate)
    {
        if (!root.TryGetProperty(name, out var value)) return;
        Ensure(value.ValueKind == JsonValueKind.Object);
        validate(value);
    }

    private static JsonElement Require(JsonElement root, string name, JsonValueKind kind)
    {
        Ensure(root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out _));
        var value = root.GetProperty(name);
        Ensure(value.ValueKind == kind);
        return value;
    }

    private static void RequireText(JsonElement root, string name) => ValidateString(Require(root, name, JsonValueKind.String));
    private static void ValidateString(JsonElement value) => Ensure(value.ValueKind == JsonValueKind.String && Nonempty(value.GetString()));

    private static void OptionalText(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value)) Ensure(value.ValueKind is JsonValueKind.String or JsonValueKind.Null);
    }

    private static void OptionalInteger(JsonElement root, string name, int minimum, int maximum)
    {
        if (!root.TryGetProperty(name, out var value)) return;
        Ensure(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= minimum && number <= maximum);
    }

    private static bool ValidExercise(Exercise? exercise) => exercise is not null && Nonempty(exercise.Id) && Nonempty(exercise.Name)
        && exercise.Rest is not null && exercise.RepTarget is not null && exercise.MuscleGroup is not null
        && exercise.Sets is >= 1 and <= 20 && exercise.Tags is not null && exercise.Tags.All(Nonempty)
        && exercise.Variants is not null && exercise.Variants.All(variant => variant is not null && Nonempty(variant.Gym));

    private static bool ValidProgram(WorkoutProgram? program) => program is not null && Nonempty(program.Id) && Nonempty(program.Name)
        && program.Sessions is not null && program.Sessions.All(ValidSession);

    private static bool ValidSession(WorkoutSession? session) => session is not null && Nonempty(session.Id) && Nonempty(session.Name)
        && ValidSessionExercises(session.Exercises) && ValidSessionExercises(session.OptionalExercises);

    private static bool ValidSessionExercises(IReadOnlyList<WorkoutExercise>? exercises) => exercises is not null
        && exercises.All(exercise => exercise is not null && Nonempty(exercise.ExerciseId)
            && (exercise.Sets is null || exercise.Sets is >= 1 and <= 20))
        && exercises.Select(exercise => exercise.ExerciseId).Distinct(StringComparer.Ordinal).Count() == exercises.Count;

    private static bool ValidLog(TrainingSessionLog? log) => log is not null && Nonempty(log.Id) && Nonempty(log.SessionId)
        && log.Exercises is not null && log.Exercises.All(ValidPerformance);

    private static bool ValidPerformance(ExercisePerformance? performance) => performance is not null && Nonempty(performance.ExerciseId)
        && performance.Sets is not null && performance.Sets.All(ValidSet);

    private static bool ValidSet(TrainingSet? set) => set is not null && set.Weight is not null && set.Reps is not null && set.Rir is not null;

    private static bool ValidActive(ActiveWorkout active) => ValidSession(active.Session) && active.Draft is not null
        && active.Draft.All(pair => Nonempty(pair.Key) && pair.Value is not null && pair.Value.All(ValidSet))
        && active.ExerciseGyms is not null && active.ExerciseGyms.All(pair => Nonempty(pair.Key) && Nonempty(pair.Value))
        && active.RestDurations is not null && active.RestDurations.All(pair => Nonempty(pair.Key) && pair.Value is >= 15 and <= 600)
        && (active.RestExerciseId is null || Nonempty(active.RestExerciseId))
        && (active.RestEnd is null || Nonempty(active.RestExerciseId));

    private static bool Nonempty(string? value) => !string.IsNullOrWhiteSpace(value);
    private static void Ensure(bool condition)
    {
        if (!condition) throw new JsonException("Invalid Performance backup content.");
    }
}
