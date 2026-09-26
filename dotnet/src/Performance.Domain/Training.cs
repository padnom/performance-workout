namespace Performance.Domain;

public sealed record WorkoutProgram
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<WorkoutSession> Sessions { get; init; } = [];
}

public sealed record WorkoutSession
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Focus { get; init; }
    public IReadOnlyList<WorkoutExercise> Exercises { get; init; } = [];
}

public sealed record WorkoutExercise
{
    public required string ExerciseId { get; init; }
    public int Order { get; init; }
    public int? Sets { get; init; }
    public string? RepTarget { get; init; }
    public string? Gym { get; init; }
}

public sealed record TrainingSet
{
    public string Weight { get; init; } = "";
    public string Reps { get; init; } = "";
    public string Rir { get; init; } = "";
}

public sealed record ExercisePerformance
{
    public required string ExerciseId { get; init; }
    public string? Gym { get; init; }
    public IReadOnlyList<TrainingSet> Sets { get; init; } = [];
}

public sealed record TrainingSessionLog
{
    public required string Id { get; init; }
    public DateTimeOffset Date { get; init; }
    public required string SessionId { get; init; }
    public int DurationMinutes { get; init; }
    public IReadOnlyList<ExercisePerformance> Exercises { get; init; } = [];
}

public sealed record ActiveWorkout
{
    public WorkoutSession? Session { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<TrainingSet>> Draft { get; init; } = new Dictionary<string, IReadOnlyList<TrainingSet>>();
    public string? DefaultGym { get; init; }
    public IReadOnlyDictionary<string, string> ExerciseGyms { get; init; } = new Dictionary<string, string>();
}
