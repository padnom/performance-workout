using Performance.Domain;

namespace Performance.Application;

/// <summary>The built-in program families, whose base session (named like the program) is hidden in favor of its variants.</summary>
public static class ProgramFamilies
{
    public static IReadOnlySet<string> ClassicIds { get; } = new HashSet<string>(["push", "pull", "legs", "upper", "arms", "full"], StringComparer.Ordinal);
    public static IReadOnlySet<string> TargetedIds { get; } = new HashSet<string>(["chest-biceps", "back-triceps", "chest-back", "quads-calves", "hams-glutes"], StringComparer.Ordinal);

    /// <summary>The sessions a program offers: a built-in family keeps only its variants, any other program all its sessions.</summary>
    public static IReadOnlyList<WorkoutSession> VisibleSessions(WorkoutProgram program) => ClassicIds.Contains(program.Id) || TargetedIds.Contains(program.Id)
        ? program.Sessions.Where(session => session.Id != program.Id).ToArray()
        : program.Sessions;
}
