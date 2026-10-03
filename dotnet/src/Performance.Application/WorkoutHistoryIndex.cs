using Performance.Domain;
using System.Globalization;
using System.Text;

namespace Performance.Application;

/// <summary>Immutable-history lookups, rebuilt only when history or catalogue definitions change.</summary>
internal sealed class WorkoutHistoryIndex
{
    private readonly Dictionary<string, HashSet<string>> _aliases;
    private readonly Dictionary<(string ExerciseId, string Gym), IndexedEntry[]> _entries;
    private readonly Dictionary<(string ExerciseId, string? Gym), IReadOnlyList<ExerciseHistoryEntry>> _queries = [];
    private readonly Dictionary<(string ExerciseId, string Gym), decimal?> _records = [];

    public WorkoutHistoryIndex(IEnumerable<Exercise> definitions, IReadOnlyList<TrainingSessionLog> history)
    {
        var names = definitions.Select(exercise => (exercise.Id, Name: NormalizeName(exercise.Name))).ToArray();
        var groups = names.GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        _aliases = names.GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => groups[group.Last().Name], StringComparer.Ordinal);
        _entries = history.Reverse().OrderByDescending(log => log.Date)
            .SelectMany(log => log.Exercises.Select(performance => new ExerciseHistoryEntry(log.Date, log.SessionId, performance)))
            .Select((entry, order) => new IndexedEntry(order, entry))
            .GroupBy(item => (item.Entry.Performance.ExerciseId, item.Entry.Performance.Gym ?? "One Air"))
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    public IReadOnlyList<ExerciseHistoryEntry> GetHistory(string exerciseId, string? gym)
    {
        var key = (exerciseId, gym);
        if (_queries.TryGetValue(key, out var cached)) return cached;
        var aliases = _aliases.GetValueOrDefault(exerciseId) ?? new HashSet<string>(StringComparer.Ordinal) { exerciseId };
        var matches = _entries.Where(entry => aliases.Contains(entry.Key.ExerciseId) && (gym is null || entry.Key.Gym == gym))
            .SelectMany(entry => entry.Value).OrderBy(entry => entry.Order).Select(entry => entry.Entry).ToArray();
        _queries[key] = matches;
        return matches;
    }

    public static string NormalizeName(string name) =>
        string.Concat(name.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(char.IsLetterOrDigit));

    public decimal? GetRecord(string exerciseId, string gym)
    {
        var key = (exerciseId, gym);
        if (_records.TryGetValue(key, out var cached)) return cached;
        var record = Record(GetHistory(exerciseId, gym));
        _records[key] = record;
        return record;
    }

    private static decimal? Record(IReadOnlyList<ExerciseHistoryEntry> history)
    {
        var weights = history.SelectMany(entry => entry.Performance.Sets)
            .Select(set => decimal.TryParse(set.Weight.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var weight) && weight >= 0 ? (decimal?)weight : null);
        return weights.Max();
    }

    private sealed record IndexedEntry(int Order, ExerciseHistoryEntry Entry);
}
