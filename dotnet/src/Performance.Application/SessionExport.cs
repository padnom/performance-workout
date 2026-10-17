using Performance.Domain;

namespace Performance.Application;

public static class SessionExport
{
    private const int MaximumSets = 20;
    private const int MaximumNameLength = 160;

    public static WorkoutSession FromLog(TrainingSessionLog log, WorkoutSession? planned, string name, IReadOnlyList<Exercise> catalog)
    {
        var definitions = Index(catalog);
        var plans = log.Exercises.DistinctBy(performance => performance.ExerciseId, StringComparer.Ordinal)
            .Where(performance => definitions.ContainsKey(performance.ExerciseId))
            .Select((performance, index) => LoggedPlan(performance, index, definitions[performance.ExerciseId], PlannedFor(planned, performance.ExerciseId)))
            .ToArray();
        return NewSession(new WorkoutSession { Id = "", Name = Bounded(name.Trim()) }, plans, []);
    }

    public static WorkoutSession FromPlan(WorkoutSession session, string gym, IReadOnlyList<Exercise> catalog)
    {
        var definitions = Index(catalog);
        var plans = PlannedPlans(session.Exercises, definitions, gym, []);
        var optionalPlans = PlannedPlans(session.OptionalExercises, definitions, gym,
            plans.Select(plan => plan.ExerciseId).ToHashSet(StringComparer.Ordinal));
        return NewSession(session, plans, optionalPlans);
    }

    private static WorkoutExercise[] PlannedPlans(IEnumerable<WorkoutExercise> source, Dictionary<string, Exercise> definitions,
        string gym, HashSet<string> excluded) => source
        .DistinctBy(plan => plan.ExerciseId, StringComparer.Ordinal)
        .Where(plan => definitions.ContainsKey(plan.ExerciseId) && !excluded.Contains(plan.ExerciseId))
        .Select((plan, index) => PlannedPlan(plan, index, definitions[plan.ExerciseId], gym))
        .ToArray();

    private static WorkoutSession NewSession(WorkoutSession template, WorkoutExercise[] plans, WorkoutExercise[] optionalPlans)
    {
        if (plans.Length == 0) throw new FormatException("Cette séance ne contient aucun exercice partageable.");
        return template with
        {
            Id = $"shared-session-{Guid.NewGuid():N}",
            Exercises = plans,
            OptionalExercises = optionalPlans
        };
    }

    private static WorkoutExercise LoggedPlan(ExercisePerformance performance, int index, Exercise definition, WorkoutExercise? planned) => new()
    {
        ExerciseId = definition.Id,
        Order = index,
        Sets = Math.Clamp(performance.Sets.Count, 1, MaximumSets),
        RepTarget = planned?.RepTarget ?? definition.RepTarget,
        Rest = planned?.Rest ?? definition.Rest,
        Gym = performance.Gym
    };

    private static WorkoutExercise PlannedPlan(WorkoutExercise plan, int index, Exercise definition, string gym) => plan with
    {
        Order = index,
        Sets = plan.Sets ?? definition.Sets,
        RepTarget = plan.RepTarget ?? definition.RepTarget,
        Rest = plan.Rest ?? definition.Rest,
        Gym = plan.Gym ?? gym
    };

    private static WorkoutExercise? PlannedFor(WorkoutSession? planned, string exerciseId) => planned?.Exercises
        .Concat(planned.OptionalExercises).FirstOrDefault(plan => plan.ExerciseId == exerciseId);

    private static string Bounded(string name) => name.Length <= MaximumNameLength ? name : name[..MaximumNameLength];

    internal static Dictionary<string, Exercise> Index(IReadOnlyList<Exercise> catalog) =>
        catalog.DistinctBy(exercise => exercise.Id, StringComparer.Ordinal).ToDictionary(exercise => exercise.Id, StringComparer.Ordinal);
}
