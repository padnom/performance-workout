using Performance.Domain;

namespace Performance.Application;

/// <summary>Shareable single-session structure. Loads, repetitions and effort are deliberately absent.</summary>
public sealed record SessionPackage
{
    public const string DocumentKind = "performance-session";
    public string Kind { get; init; } = DocumentKind;
    public int SchemaVersion { get; init; } = 1;
    public required WorkoutSession Session { get; init; }
    public IReadOnlyList<Exercise> Exercises { get; init; } = [];
}

public interface ISessionPackageCodec
{
    SessionPackage Decode(string json);
    string Encode(SessionPackage package);
}

public sealed class ImportWorkoutSessionUseCase(ISessionPackageCodec codec)
{
    public SessionPackage Execute(string json) => codec.Decode(json);
}

public sealed class ExportWorkoutSessionUseCase(ISessionPackageCodec codec)
{
    public string Execute(WorkoutSession session, IEnumerable<Exercise> effectiveCatalog)
    {
        var needed = session.Exercises.Concat(session.OptionalExercises)
            .Select(plan => plan.ExerciseId).ToHashSet(StringComparer.Ordinal);
        var definitions = effectiveCatalog.Where(exercise => needed.Contains(exercise.Id)).ToArray();
        return codec.Encode(new SessionPackage { Session = session, Exercises = definitions });
    }
}
