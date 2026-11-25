using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    private const int SessionNameMaxLength = 160;

    /// <summary>Whether a session belongs to the user (stored programs, not the week plan) and can be renamed.</summary>
    public bool CanRenameSession(string sessionId) => Data.Programs
        .Any(program => program.Id != WeekProgramId && program.Sessions.Any(session => session.Id == sessionId));

    public Task<WorkoutResult> RenameSessionAsync(string sessionId, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(() => RenameSession(sessionId, name), cancellationToken);

    private Change RenameSession(string sessionId, string name)
    {
        if (!CanRenameSession(sessionId)) return Change.Failure("UnknownSession");
        var trimmed = (name ?? "").Trim();
        if (!ValidAuthoringText(trimmed, SessionNameMaxLength)) return Change.Failure("InvalidSessionName");
        var programs = Data.Programs.Select(program => program.Id == WeekProgramId || program.Sessions.All(session => session.Id != sessionId)
            ? program
            : program with { Sessions = [.. program.Sessions.Select(session => session.Id == sessionId ? session with { Name = trimmed } : session)] }).ToArray();
        return new(Data with { Programs = programs });
    }
}
