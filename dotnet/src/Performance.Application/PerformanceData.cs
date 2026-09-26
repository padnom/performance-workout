using Performance.Domain;

namespace Performance.Application;

/// <summary>App-level persisted aggregate. Raw React key/value payloads are retained to make migration lossless.</summary>
public sealed record PerformanceData
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<Exercise> Exercises { get; init; } = [];
    public IReadOnlyList<WorkoutProgram> Programs { get; init; } = [];
    public IReadOnlyList<TrainingSessionLog> History { get; init; } = [];
    public ActiveWorkout? ActiveWorkout { get; init; }
    public IReadOnlyList<string> HiddenExerciseIds { get; init; } = [];
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> ReactLocalStorage { get; init; } = new Dictionary<string, string>();
}
