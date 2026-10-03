namespace Performance.Application;

public sealed record WorkoutResult(bool IsSuccess, string? Error = null)
{
    public static WorkoutResult Success() => new(true);
    public static WorkoutResult Failure(string error) => new(false, error);
}
