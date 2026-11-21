using System.Globalization;
using Performance.Application;
using Performance.Domain;
using static Performance.App.Resources.Text;

namespace Performance.App.Components;

public static class WorkoutUi
{
    public static readonly string[] Gyms = ["One Air", "Basic-Fit", "Toutes"];
    /// <summary>Display name of a gym; stored values keep "One Air" so existing data and backups stay compatible.</summary>
    public static string GymName(string? gym) => gym switch { "One Air" => "On Air", "Toutes" => T("Gym.All"), null => "", _ => gym };
    public static string Error(string? token)
    {
        if (token is null) return T("Error.Default");
        var key = "Error." + token;
        var text = T(key);
        return text == key ? T("Error.Default") : text;
    }
    /// <summary>Display name of a tag or muscle group; stored tags stay in French, custom tags are shown as typed.</summary>
    public static string TagName(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return "";
        var key = "Tag." + tag;
        var text = T(key);
        return text == key ? tag : text;
    }
    public static decimal Number(string? value) => decimal.TryParse(value?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : 0;
    public static string Decimal(decimal value) => value.ToString("0.##", DisplayLanguage.Culture);
    public static string NormalizeName(string name) => string.Concat(name.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)));
    public static string Gym(ExercisePerformance performance) => performance.Gym ?? "One Air";
    public static int RestSeconds(string? rest) => Performance.Application.RestDuration.Parse(rest);
    public static bool HasData(TrainingSet set) => !string.IsNullOrWhiteSpace(set.Weight) || !string.IsNullOrWhiteSpace(set.Reps);
    public static bool IsWorkoutSetCompleted(TrainingSet set) => !string.IsNullOrWhiteSpace(set.Weight) && Number(set.Reps) > 0;
    public static IEnumerable<string> ExerciseGyms(Exercise exercise) => exercise.Variants.Count == 0 || exercise.Variants.Any(variant => variant.Gym == "Toutes") ? Gyms : exercise.Variants.Select(variant => variant.Gym).Distinct();
    public static IEnumerable<TrainingSet> CompletedSets(TrainingSessionLog log) => log.Exercises.SelectMany(exercise => exercise.Sets).Where(set => Number(set.Reps) > 0);
    public static decimal Volume(IEnumerable<TrainingSet> sets) => sets.Sum(set => Number(set.Weight) * Number(set.Reps));
}
