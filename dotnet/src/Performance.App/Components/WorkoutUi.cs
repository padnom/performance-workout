using System.Globalization;
using Performance.Domain;

namespace Performance.App.Components;

public static class WorkoutUi
{
    public static readonly string[] Gyms = ["One Air", "Basic-Fit", "Toutes"];
    public static string Error(string? token) => token switch
    {
        "ActiveWorkoutExists" => "Une séance est déjà en cours. Reprends-la ou abandonne-la avant d’en démarrer une autre.",
        "NoActiveWorkout" => "Aucune séance en cours.",
        "InvalidSetIndex" => "Cette série n’est plus disponible.",
        "InvalidSet" => "Vérifie les valeurs : charge et répétitions positives.",
        "EmptyWorkout" => "Renseigne au moins une série avec des répétitions avant de terminer.",
        "UnknownExercise" => "Cet exercice n’est plus disponible.",
        "DuplicateExercise" => "Cet exercice est déjà dans la séance.",
        "InvalidExercise" => "Renseigne un nom et un nombre de séries valide.",
        "ExerciseAlreadyExists" => "Un exercice portant ce nom existe déjà. Utilise-le depuis la recherche.",
        "InvalidGym" => "Choisis une salle disponible pour cet exercice.",
        "IncompatibleGym" => "Ce programme ne correspond pas à la salle choisie. Choisis One Air ou Basic-Fit, ou remplace les exercices concernés.",
        "NoOptionalExercises" => "Les exercices optionnels sont déjà ajoutés ou indisponibles.",
        "UnknownGymProgram" => "Aucun programme prévu pour cette salle. Choisis One Air ou Basic-Fit.",
        "InvalidProgramPackage" => "Fichier de programmes invalide. Tes données sont conservées.",
        "InvalidRestDuration" => "Choisis un repos de 15 à 600 secondes.",
        "NoActiveRest" => "Le repos est déjà terminé. Démarre un nouveau repos si nécessaire.",
        "UnknownSession" => "Cette séance n’est plus disponible.",
        "InvalidWeekSessionIndex" => "Cette séance n’est plus dans ta semaine.",
        "InvalidWeekMove" => "Cette séance ne peut pas être déplacée dans cette direction.",
        _ => "L’enregistrement a échoué. Tes données précédemment enregistrées sont conservées. Réessaie."
    };
    public static decimal Number(string? value) => decimal.TryParse(value?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : 0;
    public static string Decimal(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR"));
    public static string NormalizeName(string name) => string.Concat(name.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)));
    public static string Gym(ExercisePerformance performance) => performance.Gym ?? "One Air";
    public static int RestSeconds(string? rest) => Performance.Application.RestDuration.Parse(rest);
    public static bool HasData(TrainingSet set) => !string.IsNullOrWhiteSpace(set.Weight) || !string.IsNullOrWhiteSpace(set.Reps);
    public static bool IsWorkoutSetCompleted(TrainingSet set) => !string.IsNullOrWhiteSpace(set.Weight) && Number(set.Reps) > 0;
    public static IEnumerable<string> ExerciseGyms(Exercise exercise) => exercise.Variants.Count == 0 || exercise.Variants.Any(variant => variant.Gym == "Toutes") ? Gyms : exercise.Variants.Select(variant => variant.Gym).Distinct();
    public static IEnumerable<TrainingSet> CompletedSets(TrainingSessionLog log) => log.Exercises.SelectMany(exercise => exercise.Sets).Where(set => Number(set.Reps) > 0);
    public static decimal Volume(IEnumerable<TrainingSet> sets) => sets.Sum(set => Number(set.Weight) * Number(set.Reps));
}
