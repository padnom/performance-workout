using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(JsonProgramPackageCodec))]
public sealed class JsonProgramPackageCodecTests
{
    private readonly JsonProgramPackageCodec _codec = new();

    [Fact]
    public void Export_WithRepeatedWeekSession_ShouldPreserveOrderAndAssignUniqueOccurrenceIds()
    {
        // Arrange
        var session = Package().Programs[0].Sessions[0];
        var week = new WorkoutProgram { Id = "week-share", Name = "Ma semaine", Sessions = [session, session] };

        // Act
        var json = new ExportWorkoutProgramsUseCase(_codec).Execute([week], Package().Exercises);
        var imported = new ImportWorkoutProgramsUseCase(_codec).Execute(json);

        // Assert
        imported.Programs[0].Sessions.Select(item => item.Name).ShouldBe(["Push", "Push"]);
        imported.Programs[0].Sessions.Select(item => item.Id).Distinct().Count().ShouldBe(2);
        imported.Programs[0].Sessions[0].Exercises.ShouldBe(imported.Programs[0].Sessions[1].Exercises);
    }

    [Fact]
    public void Encode_WithOptionalCustomExercise_ShouldRoundTripDefinitionAndPrescription()
    {
        // Arrange
        var package = Package();

        // Act
        var decoded = _codec.Decode(_codec.Encode(package));

        // Assert
        decoded.Kind.ShouldBe("performance-programs");
        decoded.SchemaVersion.ShouldBe(1);
        decoded.Programs[0].Gym.ShouldBe("One Air");
        decoded.Programs[0].Sessions[0].OptionalExercises.Single().Sets.ShouldBe(2);
        decoded.Programs[0].Sessions[0].OptionalExercises.Single().RepTarget.ShouldBe("10–15");
        decoded.Exercises[0].Variants.ShouldBe(package.Exercises[0].Variants);
        decoded.Exercises[1].IsCustom.ShouldBeTrue();
        decoded.Exercises[1].Tags.ShouldBe(["Abdos"]);
    }

    [Fact]
    public void Export_WithUnrelatedEffectiveDefinitions_ShouldIncludeOnlyReferencedDefinitionsAndNoPersonalFields()
    {
        // Arrange
        var package = Package();
        var unrelated = new Exercise { Id = "private-unused", Name = "Inutile" };

        // Act
        var json = new ExportWorkoutProgramsUseCase(_codec).Execute(package.Programs, package.Exercises.Append(unrelated));

        // Assert
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["kind", "schemaVersion", "programs", "exercises"]);
        var properties = PropertyNames(document.RootElement);
        foreach (var forbidden in new[] { "history", "weight", "reps", "rir", "activeWorkout", "settings", "diagnostics", "draft", "startedAt" })
            properties.ShouldNotContain(forbidden);
        json.ShouldNotContain("private-unused");
    }

    [Fact]
    public void Export_WithDefaultLibrary_ShouldSupportAllExistingProgramsAndOptionalDefinitions()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var json = new ExportWorkoutProgramsUseCase(_codec).Execute(catalog.Programs, catalog.Exercises);
        var decoded = _codec.Decode(json);

        // Assert
        decoded.Programs.Select(program => program.Id).ShouldBe(catalog.Programs.Select(program => program.Id));
        decoded.Programs.Sum(program => program.Sessions.Count).ShouldBe(catalog.Programs.Sum(program => program.Sessions.Count));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{invalid")]
    [InlineData("[]")]
    [InlineData("{\"kind\":\"performance-programs\",\"schemaVersion\":\"1\"}")]
    [InlineData("{\"kind\":\"performance-programs\",\"schemaVersion\":2}")]
    [InlineData("{\"schemaVersion\":1,\"history\":[]}")]
    public void Decode_WithInvalidEnvelope_ShouldRejectBeforeImport(string json)
    {
        // Arrange / Act / Assert
        Should.Throw<FormatException>(() => _codec.Decode(json));
    }

    [Theory]
    [InlineData("history")]
    [InlineData("settings")]
    [InlineData("activeWorkout")]
    public void Decode_WithUnexpectedPersonalFields_ShouldRejectDocument(string property)
    {
        // Arrange
        var document = JsonNode.Parse(_codec.Encode(Package()))!.AsObject();
        document[property] = new JsonObject();

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Decode(document.ToJsonString()));
    }

    [Fact]
    public void Decode_WithOversizedFile_ShouldRejectBeforeParsing()
    {
        // Arrange
        var json = new string(' ', JsonProgramPackageCodec.MaximumBytes + 1);

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Decode(json));
    }

    [Fact]
    public void Encode_WithMissingExerciseReference_ShouldReject()
    {
        // Arrange
        var package = Package() with { Exercises = [] };

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Encode(package));
    }

    [Fact]
    public void Encode_WithDuplicateExerciseDefinition_ShouldReject()
    {
        // Arrange
        var package = Package();
        package = package with { Exercises = [.. package.Exercises, package.Exercises[0]] };

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Encode(package));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Encode_WithUnsafeSetCount_ShouldReject(int sets)
    {
        // Arrange
        var package = Package();
        package = package with { Exercises = [package.Exercises[0] with { Sets = sets }, package.Exercises[1]] };

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Encode(package));
    }

    [Fact]
    public void Encode_WithDuplicateMandatoryAndOptionalExercise_ShouldReject()
    {
        // Arrange
        var package = Package();
        var session = package.Programs[0].Sessions[0];
        package = package with { Programs = [package.Programs[0] with { Sessions = [session with { OptionalExercises = session.Exercises }] }] };

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Encode(package));
    }

    [Fact]
    public void Encode_WithExecutableLink_ShouldReject()
    {
        // Arrange
        var package = Package();
        package = package with { Exercises = [package.Exercises[0] with { Link = "javascript:alert(1)" }, package.Exercises[1]] };

        // Act / Assert
        Should.Throw<FormatException>(() => _codec.Encode(package));
    }

    [Fact]
    public async Task ShareBetweenIndependentDatasets_ShouldKeepReceiverOverridesAndOptionalPlans()
    {
        // Arrange
        var sender = Package();
        var receiverOverride = sender.Exercises[0] with { Name = "Ma machine perso" };
        var repository = new Repository { Stored = new PerformanceData { Exercises = [receiverOverride] } };
        var state = new WorkoutStateService(repository, new EmptyCatalog(), TimeProvider.System);
        var json = new ExportWorkoutProgramsUseCase(_codec).Execute(sender.Programs, sender.Exercises);

        // Act
        var preview = new ImportWorkoutProgramsUseCase(_codec).Execute(json);
        var result = await state.ImportProgramsAsync(preview);
        var sharedAgain = new ExportWorkoutProgramsUseCase(_codec).Execute(repository.Stored.Programs, state.AllExercises);
        var secondReceiver = _codec.Decode(sharedAgain);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        state.AllExercises.Single(exercise => exercise.Id == "press").Name.ShouldBe("Ma machine perso");
        secondReceiver.Exercises.Count.ShouldBe(2);
        secondReceiver.Exercises.ShouldContain(exercise => exercise.Name == "Chest Press");
        secondReceiver.Programs[0].Sessions[0].OptionalExercises.Count.ShouldBe(1);
        state.Data.History.ShouldBeEmpty();
        state.Data.ActiveWorkout.ShouldBeNull();
    }

    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var child in PropertyNames(property.Value)) yield return child;
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                foreach (var child in PropertyNames(item)) yield return child;
    }

    private static ProgramPackage Package() => new()
    {
        Exercises = [new Exercise { Id = "press", Name = "Chest Press", MuscleGroup = "Pectoraux", Sets = 4, RepTarget = "10", Rest = "2 min 30", Link = "https://example.org/press", Variants = [new GymVariant { Gym = "One Air", MachineName = "Life Fitness Chest Press", Equipment = "Machine" }] },
            new Exercise { Id = "crunch", Name = "Crunch inversé", IsCustom = true, Tags = ["Abdos"], Sets = 2, RepTarget = "10–15", Rest = "1 min", Variants = [new GymVariant { Gym = "Toutes", Equipment = "Sol" }] }],
        Programs = [new WorkoutProgram { Id = "program", Name = "Programme partagé", Gym = "One Air", Description = "Quatre séances", Sessions = [new WorkoutSession { Id = "session", Name = "Push", Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 4, RepTarget = "10", Rest = "2 min 30", Gym = "One Air" }], OptionalExercises = [new WorkoutExercise { ExerciseId = "crunch", Sets = 2, RepTarget = "10–15", Rest = "1 min" }] }] }]
    };
    private sealed class EmptyCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; set; } = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { Stored = data; return ValueTask.CompletedTask; }
    }
}
