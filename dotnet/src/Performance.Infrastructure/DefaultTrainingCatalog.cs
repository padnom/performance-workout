using System.Text.Json;
using System.Text.Json.Serialization;
using Performance.Application;
using Performance.Domain;

namespace Performance.Infrastructure;

public sealed class DefaultTrainingCatalog : ITrainingCatalog
{
    private static readonly CatalogDocument _catalog = LoadCatalog();

    public IReadOnlyList<Exercise> Exercises => _catalog.Exercises;
    public IReadOnlyList<WorkoutProgram> Programs => _catalog.Programs;

    private static CatalogDocument LoadCatalog()
    {
        using var stream = typeof(DefaultTrainingCatalog).Assembly.GetManifestResourceStream(
            "Performance.Infrastructure.default-training-catalog.json")
            ?? throw new InvalidOperationException("The default training catalog resource is missing.");
        var catalog = JsonSerializer.Deserialize(stream, CatalogJsonContext.Default.CatalogDocument)
            ?? throw new InvalidOperationException("The default training catalog resource is invalid.");

        return catalog with
        {
            Exercises = Array.AsReadOnly(catalog.Exercises.ToArray()),
            Programs = Array.AsReadOnly(catalog.Programs.Select(program => program with
            {
                Sessions = program.Sessions.Select(session => session with
                {
                    OptionalExercises = session.OptionalExercises ?? []
                }).ToArray()
            }).ToArray())
        };
    }
}

internal sealed record CatalogDocument
{
    public required IReadOnlyList<Exercise> Exercises { get; init; }
    public required IReadOnlyList<WorkoutProgram> Programs { get; init; }
}

[JsonSerializable(typeof(CatalogDocument))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
