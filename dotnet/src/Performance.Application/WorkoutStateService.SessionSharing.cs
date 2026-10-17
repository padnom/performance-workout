using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    public const string SessionsProgramId = "my-sessions";
    private const int MaximumSessionsInProgram = 100;

    public Task<WorkoutResult> ImportSessionAsync(SessionPackage package, CancellationToken cancellationToken = default) =>
        MutateAsync(() => ImportSession(package), cancellationToken);

    public Task<WorkoutResult> CreateSessionAsync(WorkoutSession draft, string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => CreateSession(draft, gym), cancellationToken);

    private Change ImportSession(SessionPackage package)
    {
        try { SessionPackageValidation.Validate(package); }
        catch (FormatException) { return Change.Failure("InvalidSessionPackage"); }
        if (SessionsProgramIsFull()) return Change.Failure("SessionsProgramFull");
        var imported = MapImportedExercises(package.Exercises);
        var copy = package.Session with
        {
            Id = $"imported-session-{Guid.NewGuid():N}",
            Exercises = Remap(package.Session.Exercises, imported.Map),
            OptionalExercises = Remap(package.Session.OptionalExercises, imported.Map)
        };
        return new Change(Data with { Programs = WithSessionAppended(copy), Exercises = [.. Data.Exercises, .. imported.Additions] });
    }

    private Change CreateSession(WorkoutSession draft, string gym)
    {
        if (!ValidSessionStructure(draft)) return Change.Failure("InvalidProgramSession");
        var normalizedGym = AuthoringGym(gym);
        if (normalizedGym is null) return Change.Failure("InvalidGym");
        var definitions = AllExercises.ToDictionary(exercise => exercise.Id, StringComparer.Ordinal);
        if (ValidateAuthoredSession(draft, definitions, normalizedGym) is { } error) return Change.Failure(error);
        if (SessionsProgramIsFull()) return Change.Failure("SessionsProgramFull");
        var saved = draft with
        {
            Id = $"custom-session-{Guid.NewGuid():N}", Name = draft.Name.Trim(), Focus = draft.Focus?.Trim(),
            Exercises = CloneAuthoredPlans(draft.Exercises, definitions, normalizedGym),
            OptionalExercises = CloneAuthoredPlans(draft.OptionalExercises, definitions, normalizedGym)
        };
        return new Change(Data with { Programs = WithSessionAppended(saved) });
    }

    private WorkoutProgram? SessionsProgram() => Data.Programs.FirstOrDefault(program => program.Id == SessionsProgramId);

    private bool SessionsProgramIsFull() => SessionsProgram()?.Sessions.Count >= MaximumSessionsInProgram;

    private WorkoutProgram[] WithSessionAppended(WorkoutSession session)
    {
        var existing = SessionsProgram();
        if (existing is null)
            return [.. Data.Programs, new WorkoutProgram { Id = SessionsProgramId, Name = "Mes séances", Gym = "Toutes", Sessions = [session] }];
        var updated = existing with { Sessions = [.. existing.Sessions, session] };
        return Data.Programs.Select(program => program.Id == SessionsProgramId ? updated : program).ToArray();
    }
}
