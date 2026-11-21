using Performance.Domain;

namespace Performance.Application;

/// <summary>Plain-text summary of a workout to share, e.g. on WhatsApp, in the display language.</summary>
public static class SessionText
{
    public static string Format(WorkoutSession session, IReadOnlyList<Exercise> catalog)
    {
        var definitions = SessionExport.Index(catalog);
        var lines = new List<string> { $"Performance · {session.Name}" };
        lines.AddRange(session.Exercises.Select((plan, index) => Line(plan, index + 1, definitions)));
        if (session.OptionalExercises.Count == 0) return string.Join("\n", lines);
        lines.Add(DisplayLanguage.Pick("Optionnels :", "Optional:"));
        lines.AddRange(session.OptionalExercises.Select((plan, index) => Line(plan, session.Exercises.Count + index + 1, definitions)));
        return string.Join("\n", lines);
    }

    private static string Line(WorkoutExercise plan, int number, Dictionary<string, Exercise> definitions)
    {
        definitions.TryGetValue(plan.ExerciseId, out var definition);
        var name = definition?.Name ?? plan.ExerciseId;
        var sets = (plan.Sets ?? definition?.Sets)?.ToString() ?? "—";
        var repTarget = plan.RepTarget ?? definition?.RepTarget ?? "—";
        var rest = plan.Rest ?? definition?.Rest ?? "—";
        var gym = string.IsNullOrEmpty(plan.Gym) ? "" : $" · {(plan.Gym == "Toutes" ? DisplayLanguage.Pick("Toutes", "All gyms") : plan.Gym)}";
        return $"{number}. {name} — {sets} × {repTarget} · {DisplayLanguage.Pick("repos", "rest")} {rest}{gym}";
    }
}
