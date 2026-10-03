using Performance.Domain;

namespace Performance.Application;

public sealed record ExerciseHistoryEntry(DateTimeOffset Date, string SessionId, ExercisePerformance Performance);
