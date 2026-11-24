using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    public Task<WorkoutResult> CreateProgramAsync(WorkoutProgram draft, bool favorite = false,
        CancellationToken cancellationToken = default) =>
        MutateAsync(() => CreateProgram(draft, favorite), cancellationToken);

    private Change CreateProgram(WorkoutProgram draft, bool favorite)
    {
        if (!ValidAuthoredProgram(draft)) return Change.Failure("InvalidProgram");
        var gym = AuthoringGym(draft.Gym);
        if (gym is null) return Change.Failure("InvalidGym");
        var definitions = AllExercises.ToDictionary(exercise => exercise.Id, StringComparer.Ordinal);
        foreach (var session in draft.Sessions)
        {
            if (ValidateAuthoredSession(session, definitions, gym) is { } error) return Change.Failure(error);
        }
        var saved = CloneAuthoredProgram(draft, definitions, gym);
        var data = Data with { Programs = [.. Data.Programs, saved] };
        if (!favorite) return new(data);
        var favorites = GetFavoriteSessionIds();
        favorites.UnionWith(saved.Sessions.Select(session => session.Id));
        return new(data with { Settings = WithFavoriteSessions(favorites) });
    }

    private static string? ValidateAuthoredSession(WorkoutSession? session, IReadOnlyDictionary<string, Exercise> definitions, string gym)
    {
        if (!ValidSessionStructure(session)) return "InvalidProgramSession";
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in session.Exercises.Concat(session.OptionalExercises))
        {
            if (ValidateAuthoredPlan(plan, definitions, gym) is { } error) return error;
            if (!ids.Add(plan.ExerciseId)) return "DuplicateExercise";
        }
        return null;
    }

    private static bool ValidAuthoredProgram([NotNullWhen(true)] WorkoutProgram? draft) => draft is not null
        && ValidAuthoringText(draft.Name, 160) && ValidAuthoringOptionalText(draft.Description, 2000)
        && draft.Sessions is { Count: >= 1 and <= 100 };

    private static bool ValidSessionStructure([NotNullWhen(true)] WorkoutSession? session) => session is not null
        && ValidAuthoringText(session.Name, 160) && ValidAuthoringOptionalText(session.Focus, 300)
        && ValidAuthoringOptionalText(session.Color, 50) && session.Exercises is { Count: >= 1 and <= 80 }
        && session.OptionalExercises is { Count: <= 20 };

    private static string? ValidateAuthoredPlan(WorkoutExercise? plan, IReadOnlyDictionary<string, Exercise> definitions, string gym)
    {
        if (plan is null || !definitions.TryGetValue(plan.ExerciseId ?? "", out var definition)) return "UnknownExercise";
        if (!ValidPrescription(plan, definition)) return "InvalidExercise";
        return ValidatePlannedGym(plan.Gym, gym, definition);
    }

    private static bool ValidPrescription(WorkoutExercise plan, Exercise definition) =>
        ValidPrescriptionValues(plan.Sets ?? definition.Sets, plan.RepTarget ?? definition.RepTarget, plan.Rest ?? definition.Rest);

    private static bool ValidPrescriptionValues(int sets, string? repetitions, string? rest) => sets is >= 1 and <= 20
        && ValidAuthoringText(repetitions, 100) && rest is not null && ValidAuthoringText(rest, 100) && IsValidEditableRest(rest);

    private static string? ValidatePlannedGym(string? plannedGym, string gym, Exercise definition)
    {
        var exerciseGym = plannedGym is null ? gym : AuthoringGym(plannedGym);
        if (exerciseGym is null) return "InvalidGym";
        if (!PlannedGymMatchesProgram(gym, exerciseGym)) return "IncompatibleGym";
        var effectiveGym = gym == "Toutes" ? exerciseGym : gym;
        return IsEffectiveGymCompatible(definition, effectiveGym) ? null : "IncompatibleGym";
    }

    private static bool PlannedGymMatchesProgram(string gym, string exerciseGym) =>
        gym == "Toutes" || exerciseGym == "Toutes" || exerciseGym == gym;

    private static bool IsEffectiveGymCompatible(Exercise definition, string gym) =>
        gym == "Toutes" || IsAuthoringGymCompatible(definition, gym);

    private static WorkoutProgram CloneAuthoredProgram(WorkoutProgram draft, IReadOnlyDictionary<string, Exercise> definitions, string gym) => draft with
    {
        Id = $"custom-program-{Guid.NewGuid():N}", Name = draft.Name.Trim(), Description = draft.Description?.Trim(), Gym = gym,
        Sessions = draft.Sessions.Select(session => session with
        {
            Id = $"custom-session-{Guid.NewGuid():N}", Name = session.Name.Trim(), Focus = session.Focus?.Trim(),
            Exercises = CloneAuthoredPlans(session.Exercises, definitions, gym), OptionalExercises = CloneAuthoredPlans(session.OptionalExercises, definitions, gym)
        }).ToArray()
    };

    private static WorkoutExercise[] CloneAuthoredPlans(IReadOnlyList<WorkoutExercise> plans,
        IReadOnlyDictionary<string, Exercise> definitions, string gym) => plans.Select((plan, index) => plan with
        {
            Order = index, Sets = plan.Sets ?? definitions[plan.ExerciseId].Sets,
            RepTarget = (plan.RepTarget ?? definitions[plan.ExerciseId].RepTarget).Trim(),
            Rest = (plan.Rest ?? definitions[plan.ExerciseId].Rest).Trim(), Gym = plan.Gym is null ? gym : AuthoringGym(plan.Gym)
        }).ToArray();

    private static string? AuthoringGym(string? gym) => gym?.Trim() switch
    {
        "One Air" => "One Air", "Basic-Fit" or "Basic Fit" => "Basic-Fit", "Toutes" => "Toutes", _ => null
    };

    private static bool IsAuthoringGymCompatible(Exercise exercise, string gym) => exercise.Variants.Count == 0
        || exercise.Variants.Any(variant => AuthoringGym(variant.Gym) == "Toutes")
        || exercise.Variants.Any(variant => AuthoringGym(variant.Gym) == gym);

    private static bool ValidAuthoringText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private static bool ValidAuthoringOptionalText(string? value, int maximum) => value is null || value.Length <= maximum;
}

[JsonSerializable(typeof(string[]))]
internal sealed partial class ProgramFavoritesJsonContext : JsonSerializerContext;
