using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(JsonSessionPackageCodec))]
public sealed class JsonSessionPackageCodecTests
{
    private readonly JsonSessionPackageCodec _codec = new();

    [Fact]
    [Trait("AC", "AC-41")]
    public void Encode_WithOptionalExercise_ShouldRoundTripSessionAndDefinitions()
    {
        // Arrange
        var package = Package();

        // Act
        var decoded = _codec.Decode(_codec.Encode(package));

        // Assert
        decoded.Kind.ShouldBe("performance-session");
        decoded.SchemaVersion.ShouldBe(1);
        decoded.Session.Name.ShouldBe("Push");
        decoded.Session.Exercises.Single().Gym.ShouldBe("One Air");
        decoded.Session.OptionalExercises.Single().ExerciseId.ShouldBe("crunch");
        decoded.Exercises.Select(exercise => exercise.Id).ShouldBe(["press", "crunch"]);
        decoded.Exercises[1].Tags.ShouldBe(["Abdos"]);
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Decode_WithProgramFile_ShouldRejectAsIncompatible()
    {
        // Arrange
        var json = new JsonProgramPackageCodec().Encode(ProgramPackage());

        // Act
        var action = () => _codec.Decode(json);

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Ce fichier n'est pas un partage de séance compatible.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{invalid")]
    [InlineData("[]")]
    [InlineData("{\"kind\":\"performance-session\",\"schemaVersion\":\"1\"}")]
    [InlineData("{\"kind\":\"performance-session\",\"schemaVersion\":2}")]
    [Trait("AC", "AC-44")]
    public void Decode_WithInvalidEnvelope_ShouldThrowFormatException(string json)
    {
        // Arrange / Act
        var action = () => _codec.Decode(json);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Decode_WithUnknownField_ShouldThrowFormatException()
    {
        // Arrange
        var document = JsonNode.Parse(_codec.Encode(Package()))!.AsObject();
        document["history"] = new JsonObject();

        // Act
        var action = () => _codec.Decode(document.ToJsonString());

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Decode_WithOversizedFile_ShouldThrowFormatException()
    {
        // Arrange
        var json = new string(' ', JsonSessionPackageCodec.MaximumBytes + 1);

        // Act
        var action = () => _codec.Decode(json);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Encode_WithInvalidPackage_ShouldThrowFormatException()
    {
        // Arrange
        var package = Package() with { Exercises = [] };

        // Act
        var action = () => _codec.Encode(package);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void Execute_WithSessionFile_ShouldRouteToSession()
    {
        // Arrange
        var json = _codec.Encode(Package());

        // Act
        var shared = new ReadSharedFileUseCase(new JsonProgramPackageCodec(), _codec).Execute(json);

        // Assert
        shared.ShouldBeOfType<SharedSession>().Package.Session.Name.ShouldBe(Package().Session.Name);
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void Execute_WithProgramFile_ShouldRouteToPrograms()
    {
        // Arrange
        var json = new JsonProgramPackageCodec().Encode(ProgramPackage());

        // Act
        var shared = new ReadSharedFileUseCase(new JsonProgramPackageCodec(), _codec).Execute(json);

        // Assert
        shared.ShouldBeOfType<SharedPrograms>().Package.Programs.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Execute_WithGarbage_ShouldThrowFormatException()
    {
        // Arrange
        var useCase = new ReadSharedFileUseCase(new JsonProgramPackageCodec(), _codec);

        // Act
        var action = () => useCase.Execute("{\"hello\":1}");

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Ce fichier n'est pas un partage Performance compatible.");
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Execute_WithInvalidSessionFile_ShouldKeepSessionError()
    {
        // Arrange
        var useCase = new ReadSharedFileUseCase(new JsonProgramPackageCodec(), _codec);
        var json = "{\"kind\":\"performance-session\",\"schemaVersion\":1,\"session\":{\"id\":\"s\",\"name\":\"S\",\"exercises\":[]},\"exercises\":[]}";

        // Act
        var action = () => useCase.Execute(json);

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Fichier de séance invalide ou version non prise en charge.");
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Execute_WithMalformedJson_ShouldThrowCompatibilityMessage()
    {
        // Arrange
        var useCase = new ReadSharedFileUseCase(new JsonProgramPackageCodec(), _codec);

        // Act
        var action = () => useCase.Execute("{not json");

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Ce fichier n'est pas un partage Performance compatible.");
    }

    private static readonly Exercise Press = new() { Id = "press", Name = "Chest Press", MuscleGroup = "Pectoraux", Sets = 4, RepTarget = "10", Rest = "2 min 30", Link = "https://example.org/press" };
    private static readonly Exercise Crunch = new() { Id = "crunch", Name = "Crunch inversé", IsCustom = true, Tags = ["Abdos"], Sets = 2, RepTarget = "10–15", Rest = "1 min" };

    private static SessionPackage Package() => new()
    {
        Session = new WorkoutSession
        {
            Id = "shared-session-1",
            Name = "Push",
            Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 4, RepTarget = "10", Rest = "2 min 30", Gym = "One Air" }],
            OptionalExercises = [new WorkoutExercise { ExerciseId = "crunch", Order = 1, Sets = 2 }]
        },
        Exercises = [Press, Crunch]
    };

    private static ProgramPackage ProgramPackage() => new()
    {
        Exercises = [Press],
        Programs = [new WorkoutProgram { Id = "program", Name = "Programme", Sessions = [new WorkoutSession { Id = "session", Name = "Push", Exercises = [new WorkoutExercise { ExerciseId = "press" }] }] }]
    };
}
