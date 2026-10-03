using System.Globalization;
using System.Text.RegularExpressions;
using Performance.Domain;

namespace Performance.Application;

public sealed record ProgressCount(string Name, int Count);

public sealed record WorkoutProgressSummary(int Sessions, int EnteredSets, int AverageMinutes, decimal Volume,
    IReadOnlyList<ProgressCount> Programs, IReadOnlyList<ProgressCount> Areas);

public static class WorkoutProgress
{
    private static readonly string[] AreaNames =
        ["Pectoraux", "Dos", "Épaules", "Biceps", "Triceps", "Quadriceps", "Ischios / fessiers"];

    public static WorkoutProgressSummary Summarize(IReadOnlyList<TrainingSessionLog> history,
        IReadOnlyList<Exercise> exercises, IReadOnlyList<WorkoutProgram> programs, DateTimeOffset now, string? gym = null)
    {
        var recent = history.Where(log => log.Date > now.AddDays(-28) && log.Date <= now)
            .Where(log => gym is null || log.Exercises.Any(performance => MatchesGym(performance, gym))).ToArray();
        var performances = recent.SelectMany(log => log.Exercises).Where(performance => MatchesGym(performance, gym)).ToArray();
        var sets = performances.SelectMany(performance => performance.Sets).ToArray();
        var averageMinutes = recent.Length == 0 ? 0
            : (int)decimal.Round(recent.Sum(log => (decimal)log.DurationMinutes) / recent.Length, MidpointRounding.AwayFromZero);
        return new(recent.Length, sets.Count(HasData), averageMinutes,
            sets.Sum(set => Number(set.Weight) * Number(set.Reps)), CountPrograms(recent, programs), CountAreas(performances, exercises));
    }

    private static IReadOnlyList<ProgressCount> CountPrograms(IReadOnlyList<TrainingSessionLog> history,
        IReadOnlyList<WorkoutProgram> programs)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var program in programs.Where(program => program.Id != WorkoutTransforms.WeekProgramId))
            foreach (var session in program.Sessions) names.TryAdd(session.Id, program.Name);
        return history.GroupBy(log => log.SessionId == "free" ? "Séance libre" : names.GetValueOrDefault(log.SessionId) ?? "Séance libre",
                StringComparer.Ordinal)
            .Select(group => new ProgressCount(group.Key, group.Count())).OrderByDescending(count => count.Count).ToArray();
    }

    private static IReadOnlyList<ProgressCount> CountAreas(IReadOnlyList<ExercisePerformance> performances,
        IReadOnlyList<Exercise> exercises)
    {
        var definitions = exercises.GroupBy(exercise => exercise.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var counts = AreaNames.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        foreach (var performance in performances)
        {
            if (!definitions.TryGetValue(performance.ExerciseId, out var exercise)) continue;
            var area = AreaFor(exercise);
            if (area is null) continue;
            counts[area] += performance.Sets.Count(HasData);
        }
        return AreaNames.Select(name => new ProgressCount(name, counts[name])).ToArray();
    }

    private static string? AreaFor(Exercise exercise)
    {
        if (Regex.IsMatch(exercise.Id, "rdl|legcurl|glute|hipthrust|backext|abduction", RegexOptions.CultureInvariant))
            return "Ischios / fessiers";
        if (Regex.IsMatch(exercise.Id, "squat|legpress|legext|split|goblet", RegexOptions.CultureInvariant)) return "Quadriceps";
        if (exercise.MuscleGroup is "Jambes" or "Mollets") return "Quadriceps";
        if (Regex.IsMatch(exercise.MuscleGroup, "Ischio|Fessier|Lombaire", RegexOptions.CultureInvariant)) return "Ischios / fessiers";
        return AreaNames.Contains(exercise.MuscleGroup, StringComparer.Ordinal) ? exercise.MuscleGroup : null;
    }

    private static bool MatchesGym(ExercisePerformance performance, string? gym) => gym is null || (performance.Gym ?? "One Air") == gym;
    private static bool HasData(TrainingSet set) => !string.IsNullOrWhiteSpace(set.Weight) || !string.IsNullOrWhiteSpace(set.Reps);
    private static decimal Number(string? value) => decimal.TryParse(value?.Trim().Replace(',', '.'),
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : 0;
}
