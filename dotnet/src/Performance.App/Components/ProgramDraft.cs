using System.Globalization;
using Performance.Domain;

namespace Performance.App.Components;

public sealed class ProgramDraft
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Gym { get; set; } = "One Air";
    public bool Favorite { get; set; }
    public bool SingleSession { get; set; }
    public List<ProgramSessionDraft> Sessions { get; } = [new()];

    public WorkoutProgram ToProgram() => new()
    {
        Id = "draft", Name = Name, Description = Description, Gym = Gym,
        Sessions = Sessions.Select(ToSession).ToArray()
    };

    public WorkoutSession ToSession(ProgramSessionDraft session) => new()
    {
        Id = session.Key, Name = session.Name,
        Exercises = session.Exercises.Select((exercise, index) => new WorkoutExercise
        {
            ExerciseId = exercise.ExerciseId, Order = index, Sets = exercise.Sets,
            RepTarget = exercise.Repetitions, Rest = exercise.Rest, Gym = Gym
        }).ToArray()
    };

    public string ValidateSingleSession(IReadOnlyList<Exercise> catalog) =>
        Sessions.Count == 1 ? ValidateSession(Sessions[0], catalog) : "Une seule séance peut être créée à la fois.";

    public string Validate(IReadOnlyList<Exercise> catalog)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 160) return "Renseigne un nom de programme de 1 à 160 caractères.";
        if (Description.Length > 2000) return "La description du programme est limitée à 2 000 caractères.";
        if (Sessions.Count is < 1 or > 100) return "Ajoute de 1 à 100 séances au programme.";
        foreach (var session in Sessions)
        {
            var message = ValidateSession(session, catalog);
            if (message.Length > 0) return message;
        }
        return "";
    }

    private string ValidateSession(ProgramSessionDraft session, IReadOnlyList<Exercise> catalog)
    {
        if (string.IsNullOrWhiteSpace(session.Name) || session.Name.Length > 160) return "Renseigne un nom de 1 à 160 caractères pour chaque séance.";
        if (session.Exercises.Count is < 1 or > 80) return "Ajoute de 1 à 80 exercices dans chaque séance.";
        if (session.Exercises.Select(exercise => exercise.ExerciseId).Distinct().Count() != session.Exercises.Count) return "Un exercice ne peut apparaître qu’une fois dans une séance.";
        foreach (var exercise in session.Exercises)
        {
            var message = ValidateExercise(exercise, catalog);
            if (message.Length > 0) return message;
        }
        return "";
    }

    private string ValidateExercise(ProgramExerciseDraft exercise, IReadOnlyList<Exercise> catalog)
    {
        var definition = catalog.FirstOrDefault(item => item.Id == exercise.ExerciseId);
        if (definition is null || !Compatible(definition, Gym)) return "Un exercice ne correspond pas à cette salle. Choisis une autre salle ou retire cet exercice.";
        if (exercise.Sets is < 1 or > 20) return "Choisis de 1 à 20 séries pour chaque exercice.";
        if (string.IsNullOrWhiteSpace(exercise.Repetitions) || exercise.Repetitions.Length > 100) return "Renseigne les répétitions de chaque exercice (100 caractères maximum).";
        return ValidRest(exercise.Rest) ? "" : "Renseigne un repos au format M:ss entre 0:15 et 10:00 (100 caractères maximum).";
    }

    public static bool Compatible(Exercise exercise, string gym) => gym == "Toutes" || exercise.Variants.Count == 0
        || exercise.Variants.Any(variant => variant.Gym == "Toutes" || NormalizeGym(variant.Gym) == NormalizeGym(gym));

    internal static string NormalizeGym(string gym) => gym.Trim() == "Basic Fit" ? "Basic-Fit" : gym.Trim();

    private static bool ValidRest(string rest) => rest.Length <= 100 && ParseRestSeconds(rest) is >= 15 and <= 600;

    private static int? ParseRestSeconds(string rest)
    {
        var parts = rest.Trim().Split(':');
        if (parts.Length != 2 || parts[1].Length != 2) return null;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return null;
        if (minutes is not (>= 0 and <= 10) || seconds >= 60) return null;
        return minutes * 60 + seconds;
    }
}

public sealed class ProgramSessionDraft
{
    public string Key { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Séance 1";
    public string Search { get; set; } = "";
    public List<ProgramExerciseDraft> Exercises { get; } = [];
}

public sealed class ProgramExerciseDraft
{
    public required string ExerciseId { get; init; }
    public int Sets { get; set; } = 4;
    public string Repetitions { get; set; } = "10";
    public string Rest { get; set; } = "1:30";
}
