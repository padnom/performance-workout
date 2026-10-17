using Performance.Domain;

namespace Performance.Application;

/// <summary>Shareable training definitions only. Personal performance data is deliberately absent.</summary>
public sealed record ProgramPackage
{
    public const string DocumentKind = "performance-programs";
    public string Kind { get; init; } = DocumentKind;
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<WorkoutProgram> Programs { get; init; } = [];
    public IReadOnlyList<Exercise> Exercises { get; init; } = [];
}

public interface IProgramPackageCodec
{
    ProgramPackage Decode(string json);
    string Encode(ProgramPackage package);
}

public sealed class ImportWorkoutProgramsUseCase(IProgramPackageCodec codec)
{
    public ProgramPackage Execute(string json) => codec.Decode(json);
}

public sealed class ExportWorkoutProgramsUseCase(IProgramPackageCodec codec)
{
    public string Execute(IEnumerable<WorkoutProgram> programs, IEnumerable<Exercise> effectiveCatalog)
    {
        // A week may repeat a session. Each occurrence needs its own identity in the exchange document.
        var sessionIds = new HashSet<string>(StringComparer.Ordinal);
        var selected = programs.Select(program => program with
        {
            Sessions = program.Sessions.Select(session => session with
            {
                Id = sessionIds.Add(session.Id) ? session.Id : $"shared-session-{Guid.NewGuid():N}"
            }).ToArray()
        }).ToArray();
        var needed = selected.SelectMany(program => program.Sessions)
            .SelectMany(session => session.Exercises.Concat(session.OptionalExercises))
            .Select(exercise => exercise.ExerciseId).ToHashSet(StringComparer.Ordinal);
        var definitions = effectiveCatalog.Where(exercise => needed.Contains(exercise.Id)).ToArray();
        return codec.Encode(new ProgramPackage { Programs = selected, Exercises = definitions });
    }
}

public static class ProgramPackageValidation
{
    public static void Validate(ProgramPackage package)
    {
        if (package is null) throw new FormatException("Fichier de programmes invalide.");
        Ensure(package.Kind == ProgramPackage.DocumentKind && package.SchemaVersion == 1);
        Ensure(package.Programs is { Count: >= 1 and <= 50 } && package.Exercises is { Count: <= 2000 });
        Ensure(package.Programs.All(ValidProgram) && package.Exercises.All(ValidExercise));
        Unique(package.Programs.Select(program => program.Id));
        Unique(package.Exercises.Select(exercise => exercise.Id));
        var sessions = package.Programs.SelectMany(program => program.Sessions).ToArray();
        Ensure(sessions.Length <= 500);
        Unique(sessions.Select(session => session.Id));
        var referenced = sessions.SelectMany(session => session.Exercises.Concat(session.OptionalExercises))
            .Select(exercise => exercise.ExerciseId).ToHashSet(StringComparer.Ordinal);
        var defined = package.Exercises.Select(exercise => exercise.Id).ToHashSet(StringComparer.Ordinal);
        // Reject unrelated definitions too: this file exports exactly the selected training plans.
        Ensure(referenced.SetEquals(defined));
    }

    private static bool ValidProgram(WorkoutProgram? program) => program is not null && Text(program.Id, 200)
        && Text(program.Name, 300) && OptionalText(program.Description) && OptionalText(program.Gym, 200)
        && program.Sessions is { Count: >= 1 and <= 100 } && program.Sessions.All(ValidSession);

    internal static bool ValidSession(WorkoutSession? session) => session is not null && Text(session.Id, 200)
        && Text(session.Name, 300) && OptionalText(session.Color, 50) && OptionalText(session.Focus)
        && session.Exercises is { Count: <= 80 } && session.OptionalExercises is { Count: <= 20 }
        && ValidPlans(session.Exercises.Concat(session.OptionalExercises));

    private static bool ValidPlans(IEnumerable<WorkoutExercise> plans)
    {
        var items = plans.ToArray();
        return items.All(plan => plan is not null && Text(plan.ExerciseId, 200) && plan.Order is >= 0 and <= 1000
            && (plan.Sets is null or >= 1 and <= 20) && OptionalText(plan.RepTarget, 100)
            && OptionalText(plan.Rest, 100) && OptionalText(plan.Gym, 200))
            && items.Select(plan => plan.ExerciseId).Distinct(StringComparer.Ordinal).Count() == items.Length;
    }

    internal static bool ValidExercise(Exercise? exercise) => exercise is not null && Text(exercise.Id, 200)
        && Text(exercise.Name, 300) && OptionalText(exercise.AlternativeName, 300)
        && BoundedText(exercise.MuscleGroup, 300) && BoundedText(exercise.RepTarget, 100)
        && BoundedText(exercise.Rest, 100) && exercise.Sets is >= 1 and <= 20 && SafeLink(exercise.Link)
        && exercise.Tags is { Count: <= 50 } && exercise.Tags.All(tag => Text(tag, 100))
        && exercise.Variants is { Count: <= 30 } && exercise.Variants.All(variant => variant is not null
            && Text(variant.Gym, 200) && OptionalText(variant.MachineName, 300)
            && OptionalText(variant.Equipment, 300) && SafeLink(variant.PhotoUrl))
        && exercise.Variants.Select(variant => variant.Gym).Distinct(StringComparer.Ordinal).Count() == exercise.Variants.Count;

    private static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private static bool BoundedText(string? value, int maximum) => value is not null && value.Length <= maximum;
    private static bool OptionalText(string? value, int maximum = 4096) => value is null || value.Length <= maximum;
    private static bool SafeLink(string? value) => string.IsNullOrWhiteSpace(value) || value.Length <= 4096
        && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    private static void Unique(IEnumerable<string> values)
    {
        var ids = values.ToArray();
        Ensure(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length);
    }
    private static void Ensure(bool condition)
    {
        if (!condition) throw new FormatException("Fichier de programmes invalide ou version non prise en charge.");
    }
}
