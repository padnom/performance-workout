using System.Text.Json;
using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    private const string FavoriteSessionsSetting = "training-favorite-sessions";
    private const string LegacyFavoriteProgramsSetting = "training-favorite-programs";

    /// <summary>Id under which a free workout from the history is saved in "Mes séances".</summary>
    public static string HistorySessionId(string logId) => $"history-session-{logId}";

    public IReadOnlySet<string> FavoriteSessionIds => GetFavoriteSessionIds();

    public bool IsSessionFavorite(string sessionId) => GetFavoriteSessionIds().Contains(sessionId);

    public Task<WorkoutResult> ToggleSessionFavoriteAsync(string sessionId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => ToggleSessionFavorite(sessionId), cancellationToken);

    public Task<WorkoutResult> FavoriteHistorySessionAsync(WorkoutSession session, CancellationToken cancellationToken = default) =>
        MutateAsync(() => FavoriteHistorySession(session), cancellationToken);

    private Change ToggleSessionFavorite(string sessionId)
    {
        if (!ExistingSessionIds().Contains(sessionId)) return Change.Failure("UnknownSession");
        var favorites = GetFavoriteSessionIds();
        if (!favorites.Remove(sessionId)) favorites.Add(sessionId);
        return new(Data with { Settings = WithFavoriteSessions(favorites) });
    }

    private Change FavoriteHistorySession(WorkoutSession session)
    {
        if (!ValidSessionStructure(session) || string.IsNullOrWhiteSpace(session.Id)) return Change.Failure("InvalidProgramSession");
        var favorites = GetFavoriteSessionIds();
        favorites.Add(session.Id);
        if (ExistingSessionIds().Contains(session.Id)) return new(Data with { Settings = WithFavoriteSessions(favorites) });
        if (SessionsProgramIsFull()) return Change.Failure("SessionsProgramFull");
        var saved = session with { Name = session.Name.Trim(), Focus = session.Focus?.Trim() };
        return new(Data with { Programs = WithSessionAppended(saved), Settings = WithFavoriteSessions(favorites) });
    }

    /// <summary>Whether a session can be a favorite: it exists and the library shows it (not the week plan, not a family's base session).</summary>
    public bool CanFavoriteSession(string sessionId) => ExistingSessionIds().Contains(sessionId);

    private HashSet<string> ExistingSessionIds() => Programs.Where(program => program.Id != WeekProgramId)
        .SelectMany(ProgramFamilies.VisibleSessions).Select(session => session.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Favorite sessions that still exist. Without the session setting, the former favorite programs are read as
    /// favorites of all their visible sessions; the former setting is dropped on the next write.
    /// </summary>
    private HashSet<string> GetFavoriteSessionIds()
    {
        var existing = ExistingSessionIds();
        if (Data.Settings.ContainsKey(FavoriteSessionsSetting))
            return ParseIds(Data.Settings[FavoriteSessionsSetting]).Where(existing.Contains).ToHashSet(StringComparer.Ordinal);
        if (!Data.Settings.TryGetValue(LegacyFavoriteProgramsSetting, out var legacy)) return new(StringComparer.Ordinal);
        var programs = ParseIds(legacy).ToHashSet(StringComparer.Ordinal);
        return Programs.Where(program => program.Id != WeekProgramId && programs.Contains(program.Id))
            .SelectMany(ProgramFamilies.VisibleSessions).Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
    }

    private static string[] ParseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return (JsonSerializer.Deserialize(json, ProgramFavoritesJsonContext.Default.StringArray) ?? []).Where(id => id is not null).ToArray(); }
        catch (JsonException) { return []; }
    }

    private Dictionary<string, string> WithFavoriteSessions(HashSet<string> favorites)
    {
        var settings = new Dictionary<string, string>(Data.Settings, StringComparer.Ordinal)
        {
            [FavoriteSessionsSetting] = JsonSerializer.Serialize(favorites.Order(StringComparer.Ordinal).ToArray(), ProgramFavoritesJsonContext.Default.StringArray)
        };
        settings.Remove(LegacyFavoriteProgramsSetting);
        return settings;
    }
}
