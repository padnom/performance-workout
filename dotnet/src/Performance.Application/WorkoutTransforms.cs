using Performance.Domain;

namespace Performance.Application;

/// <summary>Pure transformations; persistence and publication remain in WorkoutStateService.</summary>
internal static class WorkoutTransforms
{
    internal const string WeekProgramId = "react-week-plan";

    public static PerformanceData UpdateWeek(PerformanceData data, IReadOnlyList<WorkoutSession> sessions)
    {
        var programs = data.Programs.ToArray();
        var index = Array.FindIndex(programs, program => program.Id == WeekProgramId);
        if (index < 0) return data with { Programs = [.. programs, new WorkoutProgram { Id = WeekProgramId, Name = "Programme hebdomadaire", Sessions = sessions }] };
        programs[index] = programs[index] with { Sessions = sessions };
        return data with { Programs = programs };
    }

    public static WorkoutSession Reorder(WorkoutSession session, IReadOnlyList<WorkoutExercise> exercises) =>
        session with { Exercises = exercises.Select((exercise, order) => exercise with { Order = order }).ToArray() };

    public static ActiveWorkout RemoveExercise(ActiveWorkout active, string exerciseId)
    {
        var draft = active.Draft.Where(entry => entry.Key != exerciseId && !entry.Key.StartsWith($"{exerciseId}@@", StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return active with
        {
            Session = Reorder(active.Session!, active.Session!.Exercises.Where(item => item.ExerciseId != exerciseId).ToArray()),
            Draft = draft,
            ExerciseGyms = active.ExerciseGyms.Where(entry => entry.Key != exerciseId).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            RestDurations = active.RestDurations.Where(entry => entry.Key != exerciseId).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            RestEnd = active.RestExerciseId == exerciseId ? null : active.RestEnd,
            RestExerciseId = active.RestExerciseId == exerciseId ? null : active.RestExerciseId
        };
    }

    public static IReadOnlyList<TrainingSet> Resize(IReadOnlyList<TrainingSet> existing, int count) =>
        Enumerable.Range(0, count).Select(index => index < existing.Count ? existing[index] : new TrainingSet()).ToArray();
}
