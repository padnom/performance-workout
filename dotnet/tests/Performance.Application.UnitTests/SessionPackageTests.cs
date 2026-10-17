using System.Text.Json;
using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(SessionExport))]
public sealed class SessionExportTests
{
    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithVeryLongName_ShouldBoundNameLength()
    {
        // Arrange
        var log = Log(Performance("squat", 3));

        // Act
        var session = SessionExport.FromLog(log, null, new string('a', 299) + " · 17/10", Catalog());

        // Assert
        session.Name.Length.ShouldBe(160);
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithSeveralExercises_ShouldKeepLogOrderAndReindex()
    {
        // Arrange
        var log = Log(Performance("squat", 3), Performance("press", 4));

        // Act
        var session = SessionExport.FromLog(log, null, "  Ma séance  ", Catalog());

        // Assert
        session.Name.ShouldBe("Ma séance");
        session.Id.ShouldStartWith("shared-session-");
        session.Exercises.Select(plan => plan.ExerciseId).ShouldBe(["squat", "press"]);
        session.Exercises.Select(plan => plan.Order).ShouldBe([0, 1]);
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithSameExerciseAtTwoGyms_ShouldKeepFirstOccurrence()
    {
        // Arrange
        var log = Log(Performance("squat", 3, "One Air"), Performance("squat", 5, "Basic-Fit"));

        // Act
        var session = SessionExport.FromLog(log, null, "Jambes", Catalog());

        // Assert
        var plan = session.Exercises.Single();
        plan.Gym.ShouldBe("One Air");
        plan.Sets.ShouldBe(3);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 7)]
    [InlineData(50, 20)]
    [Trait("AC", "AC-40")]
    public void FromLog_WithSetCount_ShouldClampBetweenOneAndTwenty(int setCount, int expected)
    {
        // Arrange
        var log = Log(Performance("squat", setCount));

        // Act
        var session = SessionExport.FromLog(log, null, "Jambes", Catalog());

        // Assert
        session.Exercises.Single().Sets.ShouldBe(expected);
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithPlannedExercise_ShouldTakeRepTargetAndRestFromPlan()
    {
        // Arrange
        var planned = new WorkoutSession
        {
            Id = "planned",
            Name = "Prévue",
            OptionalExercises = [new WorkoutExercise { ExerciseId = "squat", RepTarget = "5", Rest = "3 min" }]
        };

        // Act
        var session = SessionExport.FromLog(Log(Performance("squat", 3)), planned, "Jambes", Catalog());

        // Assert
        session.Exercises.Single().RepTarget.ShouldBe("5");
        session.Exercises.Single().Rest.ShouldBe("3 min");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithPlannedValuesMissing_ShouldFallBackToDefinition()
    {
        // Arrange
        var planned = new WorkoutSession
        {
            Id = "planned",
            Name = "Prévue",
            Exercises = [new WorkoutExercise { ExerciseId = "squat" }]
        };

        // Act
        var session = SessionExport.FromLog(Log(Performance("squat", 3)), planned, "Jambes", Catalog());

        // Assert
        session.Exercises.Single().RepTarget.ShouldBe("8–10");
        session.Exercises.Single().Rest.ShouldBe("2 min");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithoutPlannedSession_ShouldTakeValuesFromDefinition()
    {
        // Arrange
        var log = Log(Performance("press", 4));

        // Act
        var session = SessionExport.FromLog(log, null, "Push", Catalog());

        // Assert
        session.Exercises.Single().RepTarget.ShouldBe("10–12");
        session.Exercises.Single().Rest.ShouldBe("90 s");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithExerciseAbsentFromCatalog_ShouldSkipIt()
    {
        // Arrange
        var log = Log(Performance("ghost", 3), Performance("press", 4));

        // Act
        var session = SessionExport.FromLog(log, null, "Push", Catalog());

        // Assert
        session.Exercises.Select(plan => plan.ExerciseId).ShouldBe(["press"]);
        session.Exercises.Single().Order.ShouldBe(0);
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithNoShareableExercise_ShouldThrowFormatException()
    {
        // Arrange
        var log = Log(Performance("ghost", 3));

        // Act
        var action = () => SessionExport.FromLog(log, null, "Vide", Catalog());

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Cette séance ne contient aucun exercice partageable.");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void FromLog_WithLoggedWeightsAndReps_ShouldNotLeakPersonalValues()
    {
        // Arrange
        var performance = new ExercisePerformance
        {
            ExerciseId = "squat",
            Sets = [new TrainingSet { Weight = "137.5", Reps = "8", Rir = "2" }, new TrainingSet { Weight = "137.5", Reps = "7", Rir = "1" }]
        };

        // Act
        var session = SessionExport.FromLog(Log(performance), null, "Jambes", Catalog());
        var json = JsonSerializer.Serialize(new SessionPackage { Session = session, Exercises = [Squat] });

        // Assert
        json.ShouldNotContain("137.5");
        json.ShouldNotContain("Weight", Case.Insensitive);
        json.ShouldNotContain("Rir", Case.Insensitive);
    }

    [Fact]
    [Trait("AC", "AC-41")]
    public void FromPlan_WithOptionalExercises_ShouldKeepThemOptional()
    {
        // Arrange
        var source = PlannedSession();

        // Act
        var session = SessionExport.FromPlan(source, "One Air", Catalog());

        // Assert
        session.Exercises.Select(plan => plan.ExerciseId).ShouldBe(["squat"]);
        session.OptionalExercises.Select(plan => plan.ExerciseId).ShouldBe(["press"]);
        session.OptionalExercises.Select(plan => plan.Order).ShouldBe([0]);
    }

    [Fact]
    [Trait("AC", "AC-41")]
    public void FromPlan_WithMissingValues_ShouldFillFromDefinitionAndFallBackToGym()
    {
        // Arrange
        var source = PlannedSession();

        // Act
        var session = SessionExport.FromPlan(source, "One Air", Catalog());

        // Assert
        var plan = session.Exercises.First();
        plan.Sets.ShouldBe(5);
        plan.RepTarget.ShouldBe("8–10");
        plan.Rest.ShouldBe("2 min");
        plan.Gym.ShouldBe("One Air");
        session.OptionalExercises[0].Gym.ShouldBe("Basic-Fit");
    }

    [Fact]
    [Trait("AC", "AC-41")]
    public void FromPlan_WithExerciseAbsentFromCatalog_ShouldSkipPlanAndCreateNewId()
    {
        // Arrange
        var source = PlannedSession() with
        {
            Exercises = [new WorkoutExercise { ExerciseId = "ghost" }, new WorkoutExercise { ExerciseId = "squat", Order = 5 }]
        };

        // Act
        var session = SessionExport.FromPlan(source, "One Air", Catalog());

        // Assert
        session.Id.ShouldStartWith("shared-session-");
        session.Id.ShouldNotBe(source.Id);
        session.Exercises.Select(plan => plan.ExerciseId).ShouldBe(["squat"]);
        session.Exercises[0].Order.ShouldBe(0);
    }

    private static WorkoutSession PlannedSession() => new()
    {
        Id = "push-day",
        Name = "Jambes",
        Color = "red",
        Focus = "Quadriceps",
        Exercises = [new WorkoutExercise { ExerciseId = "squat", Sets = 5, Order = 0 }],
        OptionalExercises = [new WorkoutExercise { ExerciseId = "press", Sets = 3, RepTarget = "12", Rest = "60 s", Gym = "Basic-Fit", Order = 1 }]
    };

    private static ExercisePerformance Performance(string exerciseId, int setCount, string? gym = null) => new()
    {
        ExerciseId = exerciseId,
        Gym = gym,
        Sets = Enumerable.Range(0, setCount).Select(_ => new TrainingSet { Weight = "60", Reps = "10", Rir = "1" }).ToArray()
    };

    private static TrainingSessionLog Log(params ExercisePerformance[] performances) => new()
    {
        Id = "log",
        SessionId = "push-day",
        Date = new DateTimeOffset(2026, 1, 5, 18, 0, 0, TimeSpan.Zero),
        Exercises = performances
    };

    private static readonly Exercise Squat = new() { Id = "squat", Name = "Squat", Sets = 5, RepTarget = "8–10", Rest = "2 min" };
    private static readonly Exercise Press = new() { Id = "press", Name = "Développé", Sets = 4, RepTarget = "10–12", Rest = "90 s" };

    private static IReadOnlyList<Exercise> Catalog() => [Squat, Press];
}

[TestSubject(typeof(SessionText))]
public sealed class SessionTextTests
{
    [Fact]
    [Trait("AC", "AC-42")]
    public void Format_WithMainAndOptionalExercises_ShouldProduceExactText()
    {
        // Arrange
        var session = new WorkoutSession
        {
            Id = "s",
            Name = "Jambes",
            Exercises =
            [
                new WorkoutExercise { ExerciseId = "squat", Sets = 4, RepTarget = "6", Rest = "3 min", Gym = "One Air" },
                new WorkoutExercise { ExerciseId = "press" }
            ],
            OptionalExercises = [new WorkoutExercise { ExerciseId = "abs", Gym = "" }]
        };
        var catalog = new[]
        {
            new Exercise { Id = "squat", Name = "Squat", Sets = 5, RepTarget = "8–10", Rest = "2 min" },
            new Exercise { Id = "press", Name = "Développé", Sets = 4, RepTarget = "10–12", Rest = "90 s" },
            new Exercise { Id = "abs", Name = "Abdos", Sets = 2, RepTarget = "15", Rest = "1 min" }
        };

        // Act
        var text = SessionText.Format(session, catalog);

        // Assert
        text.ShouldBe(string.Join("\n",
            "Performance · Jambes",
            "1. Squat — 4 × 6 · repos 3 min · One Air",
            "2. Développé — 4 × 10–12 · repos 90 s",
            "Optionnels :",
            "3. Abdos — 2 × 15 · repos 1 min"));
    }

    [Fact]
    [Trait("AC", "AC-42")]
    public void Format_WithExerciseMissingFromCatalog_ShouldShowPlaceholders()
    {
        // Arrange
        var session = new WorkoutSession { Id = "s", Name = "Bras", Exercises = [new WorkoutExercise { ExerciseId = "ghost" }] };

        // Act
        var text = SessionText.Format(session, []);

        // Assert
        text.ShouldBe("Performance · Bras\n1. ghost — — × — · repos —");
    }
}

[TestSubject(typeof(SessionPackageValidation))]
public sealed class SessionPackageValidationTests
{
    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithConsistentPackage_ShouldNotThrow()
    {
        // Arrange
        var package = ValidPackage();

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldNotThrow();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithNullPackage_ShouldThrowFormatException()
    {
        // Arrange
        SessionPackage package = null!;

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Fichier de séance invalide.");
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithWrongKind_ShouldThrowFormatException()
    {
        // Arrange
        var package = ValidPackage() with { Kind = "performance-programs" };

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>().Message.ShouldBe("Fichier de séance invalide ou version non prise en charge.");
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithoutExercise_ShouldThrowFormatException()
    {
        // Arrange
        var package = ValidPackage() with
        {
            Session = ValidPackage().Session with { Exercises = [], OptionalExercises = [] },
            Exercises = []
        };

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithUndefinedReference_ShouldThrowFormatException()
    {
        // Arrange
        var package = ValidPackage() with { Exercises = [] };

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithUnreferencedDefinition_ShouldThrowFormatException()
    {
        // Arrange
        var package = ValidPackage();
        package = package with { Exercises = [.. package.Exercises, new Exercise { Id = "extra", Name = "Extra" }] };

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public void Validate_WithDuplicateDefinitionIds_ShouldThrowFormatException()
    {
        // Arrange
        var package = ValidPackage();
        package = package with { Exercises = [.. package.Exercises, .. package.Exercises] };

        // Act
        var action = () => SessionPackageValidation.Validate(package);

        // Assert
        action.ShouldThrow<FormatException>();
    }

    private static SessionPackage ValidPackage() => new()
    {
        Session = new WorkoutSession
        {
            Id = "shared-session-1",
            Name = "Jambes",
            Exercises = [new WorkoutExercise { ExerciseId = "squat", Sets = 4 }],
            OptionalExercises = [new WorkoutExercise { ExerciseId = "abs", Order = 1 }]
        },
        Exercises =
        [
            new Exercise { Id = "squat", Name = "Squat", Sets = 4 },
            new Exercise { Id = "abs", Name = "Abdos", Sets = 2 }
        ]
    };
}

[TestSubject(typeof(ExportWorkoutSessionUseCase))]
public sealed class ExportWorkoutSessionUseCaseTests
{
    [Fact]
    [Trait("AC", "AC-41")]
    public void Execute_WithLargerCatalog_ShouldPackageOnlyReferencedDefinitions()
    {
        // Arrange
        var codec = new CapturingCodec();
        var session = new WorkoutSession
        {
            Id = "s",
            Name = "Jambes",
            Exercises = [new WorkoutExercise { ExerciseId = "squat" }],
            OptionalExercises = [new WorkoutExercise { ExerciseId = "abs" }]
        };
        var catalog = new[]
        {
            new Exercise { Id = "squat", Name = "Squat" },
            new Exercise { Id = "abs", Name = "Abdos" },
            new Exercise { Id = "press", Name = "Développé" }
        };

        // Act
        var json = new ExportWorkoutSessionUseCase(codec).Execute(session, catalog);

        // Assert
        json.ShouldBe("encoded");
        codec.Encoded!.Session.ShouldBe(session);
        codec.Encoded.Exercises.Select(exercise => exercise.Id).ShouldBe(["squat", "abs"]);
    }

    private sealed class CapturingCodec : ISessionPackageCodec
    {
        public SessionPackage? Encoded { get; private set; }
        public SessionPackage Decode(string json) => throw new NotSupportedException();

        public string Encode(SessionPackage package)
        {
            Encoded = package;
            return "encoded";
        }
    }
}
