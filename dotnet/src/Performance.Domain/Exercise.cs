namespace Performance.Domain;

/// <summary>Canonical movement. Gym-specific machine, load history and image remain variants.</summary>
public sealed record Exercise
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? AlternativeName { get; init; }
    public string MuscleGroup { get; init; } = "";
    public string RepTarget { get; init; } = "";
    public int Sets { get; init; } = 4;
    public string Rest { get; init; } = "";
    public string? Link { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<GymVariant> Variants { get; init; } = [];
    public bool IsCustom { get; init; }
}

public sealed record GymVariant
{
    public required string Gym { get; init; }
    public string? MachineName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Equipment { get; init; }
}
