using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    private static readonly string[] RecommendedRoles = ["push", "pull", "legs", "upper"];

    public WorkoutProgram? GetRecommendedProgram(string gym) => catalog.Programs
        .FirstOrDefault(program => program.Id == RecommendedProgramId(gym));

    /// <summary>Only the built-in gym sessions have equivalents. Custom and legacy sessions keep their identity.</summary>
    public WorkoutSession ResolveSessionForGym(WorkoutSession session, string gym)
    {
        var role = RecommendedRoles.FirstOrDefault(role => session.Id == $"one-air-{role}" || session.Id == $"basic-fit-{role}");
        var mapped = role is null ? null : GetRecommendedProgram(gym)?.Sessions
            .FirstOrDefault(candidate => candidate.Id.EndsWith($"-{role}", StringComparison.Ordinal));
        return mapped is null || mapped.Id == session.Id ? session : mapped;
    }

    public Task<WorkoutResult> UseGymProgramWeekAsync(string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => GetRecommendedProgram(gym) is { } program
            ? UpdateWeek(program.Sessions) : Change.Failure("UnknownGymProgram"), cancellationToken);

    public Task<WorkoutResult> AddOptionalExercisesAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(AddOptionalExercises, cancellationToken);

    public Task<WorkoutResult> SetActiveWorkoutGymAsync(string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => SetActiveWorkoutGym(gym), cancellationToken);

    private Change SetActiveWorkoutGym(string gym)
    {
        if (Data.ActiveWorkout?.Session is not { } session) return Change.Failure("NoActiveWorkout");
        if (string.IsNullOrWhiteSpace(gym)) return Change.Failure("InvalidGym");
        var active = Data.ActiveWorkout;
        var gyms = session.Exercises.ToDictionary(item => item.ExerciseId,
            item => ResolveGym(active, item.ExerciseId), StringComparer.Ordinal);
        return new(Data with { ActiveWorkout = active with { DefaultGym = gym, ExerciseGyms = gyms, Draft = CopyDraft(active) } });
    }

    private Change AddOptionalExercises()
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is not { } session) return Change.Failure("NoActiveWorkout");
        var pending = session.OptionalExercises ?? [];
        if (pending.Count == 0) return new(Data);
        if (ValidateOptionalExercises(pending) is { } error) return Change.Failure(error);
        var added = pending.Where(item => !session.Exercises.Any(existing => existing.ExerciseId == item.ExerciseId)).ToArray();
        var gym = active.DefaultGym ?? CurrentGym;
        if (added.Any(item => !IsAvailableInGym(item.ExerciseId, gym))) return Change.Failure("IncompatibleGym");
        return new(Data with { ActiveWorkout = WithOptionalExercises(active, added, gym) });
    }

    private string? ValidateOptionalExercises(IReadOnlyList<WorkoutExercise> pending)
    {
        if (pending.Select(item => item.ExerciseId).Distinct(StringComparer.Ordinal).Count() != pending.Count)
            return "DuplicateExercise";
        if (pending.Any(item => !AllExercises.Any(exercise => exercise.Id == item.ExerciseId)))
            return "UnknownExercise";
        return null;
    }

    private ActiveWorkout WithOptionalExercises(ActiveWorkout active, IReadOnlyList<WorkoutExercise> added, string gym)
    {
        var draft = CopyDraft(active);
        var gyms = new Dictionary<string, string>(active.ExerciseGyms, StringComparer.Ordinal);
        var durations = new Dictionary<string, int>(active.RestDurations, StringComparer.Ordinal);
        foreach (var item in added)
        {
            var definition = AllExercises.First(exercise => exercise.Id == item.ExerciseId);
            gyms[item.ExerciseId] = gym;
            draft[DraftKey(item.ExerciseId, gym)] = Prefill(item.ExerciseId, gym, Math.Clamp(item.Sets ?? definition.Sets, 1, 20));
            durations[item.ExerciseId] = RestDuration.Parse(item.Rest ?? definition.Rest);
        }
        var session = active.Session!;
        return active with { Session = WorkoutTransforms.Reorder(session with { OptionalExercises = [] }, [.. session.Exercises, .. added]),
            Draft = draft, ExerciseGyms = gyms, RestDurations = durations };
    }

    private bool IsAvailableInGym(string exerciseId, string gym)
    {
        var exercise = AllExercises.FirstOrDefault(exercise => exercise.Id == exerciseId);
        return exercise is not null && (exercise.Variants.Count == 0 || exercise.Variants
            .Any(variant => variant.Gym == "Toutes" || NormalizeGym(variant.Gym) == NormalizeGym(gym)));
    }

    private static bool IsRecommendedSession(string id) => RecommendedRoles.Any(role =>
        id == $"one-air-{role}" || id == $"basic-fit-{role}");

    private static string? RecommendedProgramId(string gym) => NormalizeGym(gym) switch
    {
        "One Air" => "gym-one-air",
        "Basic-Fit" => "gym-basic-fit",
        _ => null
    };

    private static string NormalizeGym(string gym) => gym.Trim() == "Basic Fit" ? "Basic-Fit" : gym.Trim();
}
