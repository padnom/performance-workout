using Performance.Domain;

namespace Performance.Application;

public sealed partial class WorkoutStateService
{
    public Task<WorkoutResult> ImportProgramsAsync(ProgramPackage package, CancellationToken cancellationToken = default) =>
        MutateAsync(() => ImportPrograms(package), cancellationToken);

    private Change ImportPrograms(ProgramPackage package)
    {
        try { ProgramPackageValidation.Validate(package); }
        catch (FormatException) { return Change.Failure("InvalidProgramPackage"); }

        var imported = MapImportedExercises(package.Exercises);
        var copies = package.Programs.Select(program => program with
        {
            Id = $"imported-program-{Guid.NewGuid():N}",
            Sessions = program.Sessions.Select(session => session with
            {
                Id = $"imported-session-{Guid.NewGuid():N}",
                Exercises = Remap(session.Exercises, imported.Map),
                OptionalExercises = Remap(session.OptionalExercises, imported.Map)
            }).ToArray()
        }).ToArray();
        return new Change(Data with { Programs = [.. Data.Programs, .. copies], Exercises = [.. Data.Exercises, .. imported.Additions] });
    }

    private ImportedExercises MapImportedExercises(IEnumerable<Exercise> definitions)
    {
        var existing = AllExercises.ToDictionary(exercise => exercise.Id, StringComparer.Ordinal);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var additions = new List<Exercise>();
        foreach (var definition in definitions)
        {
            if (existing.TryGetValue(definition.Id, out var local) && SameDefinition(local, definition))
            {
                map.Add(definition.Id, local.Id);
                continue;
            }
            var id = $"imported-exercise-{Guid.NewGuid():N}";
            map.Add(definition.Id, id);
            additions.Add(definition with { Id = id, IsCustom = true });
        }
        return new ImportedExercises(map, additions);
    }

    private sealed record ImportedExercises(IReadOnlyDictionary<string, string> Map, IReadOnlyList<Exercise> Additions);

    private static WorkoutExercise[] Remap(IReadOnlyList<WorkoutExercise> plans, IReadOnlyDictionary<string, string> map) =>
        plans.Select(plan => plan with { ExerciseId = map[plan.ExerciseId] }).ToArray();

    private static bool SameDefinition(Exercise first, Exercise second) => first.Name == second.Name
        && first.AlternativeName == second.AlternativeName && first.MuscleGroup == second.MuscleGroup
        && first.RepTarget == second.RepTarget && first.Sets == second.Sets && first.Rest == second.Rest && first.Link == second.Link
        && first.Tags.Order(StringComparer.Ordinal).SequenceEqual(second.Tags.Order(StringComparer.Ordinal), StringComparer.Ordinal)
        && first.Variants.OrderBy(variant => variant.Gym, StringComparer.Ordinal)
            .SequenceEqual(second.Variants.OrderBy(variant => variant.Gym, StringComparer.Ordinal));
}
