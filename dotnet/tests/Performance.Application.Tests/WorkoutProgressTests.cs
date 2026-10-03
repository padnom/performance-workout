using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.Tests;

[TestSubject(typeof(WorkoutProgress))]
public sealed class WorkoutProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Exercise Press = new() { Id = "press", Name = "Press", MuscleGroup = "Pectoraux" };

    [Theory]
    [InlineData(-29, 0, 0)]
    [InlineData(-28, 0, 0)]
    [InlineData(-28, 1, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 0)]
    public void Summarize_WithDateAtWindowBoundary_ShouldExcludeOldAndFutureLogs(int days, int ticks, int expected)
    {
        // Arrange
        var history = new[] { Log(date: Now.AddDays(days).AddTicks(ticks)) };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now);

        // Assert
        summary.Sessions.ShouldBe(expected);
        summary.EnteredSets.ShouldBe(expected);
        summary.Volume.ShouldBe(expected * 100m);
        summary.Areas.Single(area => area.Name == "Pectoraux").Count.ShouldBe(expected);
    }

    [Fact]
    public void Summarize_WithoutHistory_ShouldReturnZeroAndEveryAreaInWebOrder()
    {
        // Arrange
        TrainingSessionLog[] history = [];

        // Act
        var summary = WorkoutProgress.Summarize(history, [], [], Now);

        // Assert
        summary.Sessions.ShouldBe(0);
        summary.EnteredSets.ShouldBe(0);
        summary.AverageMinutes.ShouldBe(0);
        summary.Volume.ShouldBe(0);
        summary.Programs.ShouldBeEmpty();
        summary.Areas.ShouldBe([new("Pectoraux", 0), new("Dos", 0), new("Épaules", 0), new("Biceps", 0), new("Triceps", 0),
            new("Quadriceps", 0), new("Ischios / fessiers", 0)]);
    }

    [Theory]
    [InlineData("20", "", "", 1)]
    [InlineData("", "12", "", 1)]
    [InlineData("0", "", "", 1)]
    [InlineData("", "0", "", 1)]
    [InlineData(" ", " ", "", 0)]
    [InlineData("", "", "3", 0)]
    [InlineData("invalid", "", "", 1)]
    public void Summarize_WithPartialOrLegacySet_ShouldCountOnlyEnteredWeightOrReps(string weight, string reps, string rir, int expected)
    {
        // Arrange
        var history = new[] { Log(sets: [new TrainingSet { Weight = weight, Reps = reps, Rir = rir }]) };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now);

        // Assert
        summary.EnteredSets.ShouldBe(expected);
        summary.Areas.Single(area => area.Name == "Pectoraux").Count.ShouldBe(expected);
    }

    [Theory]
    [InlineData("45,5", "8", 364)]
    [InlineData("12.5", "8", 100)]
    [InlineData(" 12,5 ", " 8 ", 100)]
    [InlineData("10", "2,5", 25)]
    [InlineData("", "8", 0)]
    [InlineData("40", "", 0)]
    [InlineData("0", "8", 0)]
    [InlineData("bad", "8", 0)]
    [InlineData("-4", "8", 0)]
    [InlineData("40", "-3", 0)]
    [InlineData("NaN", "Infinity", 0)]
    public void Summarize_WithWeightAndReps_ShouldCalculateValidNonNegativeVolume(string weight, string reps, decimal expected)
    {
        // Arrange
        var history = new[] { Log(sets: [new TrainingSet { Weight = weight, Reps = reps }]) };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now);

        // Assert
        summary.Volume.ShouldBe(expected);
    }

    [Theory]
    [InlineData(20, 21, 21)]
    [InlineData(0, 1, 1)]
    [InlineData(30, 40, 35)]
    public void Summarize_WithAverageDuration_ShouldRoundHalfAwayFromZero(int first, int second, int expected)
    {
        // Arrange
        var history = new[] { Log(duration: first), Log(duration: second) };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now);

        // Assert
        summary.AverageMinutes.ShouldBe(expected);
    }

    [Fact]
    public void Summarize_WithLegacyAndVariantIds_ShouldGroupByProgramFamilyIgnoringWeekPlan()
    {
        // Arrange
        var push = Program("push", "Push", "push", "pa", "pb", "pc", "pd");
        var pull = Program("pull", "Pull", "pull", "pua");
        var weekly = Program("react-week-plan", "My week", "pa", "pull");
        var history = new[] { Log(session: "pa"), Log(session: "push"), Log(session: "pb"), Log(session: "pull"), Log(session: "pua") };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [weekly, push, pull], Now);

        // Assert
        summary.Programs.ShouldBe([new ProgressCount("Push", 3), new ProgressCount("Pull", 2)]);
    }

    [Fact]
    public void Summarize_WithCustomUnknownAndFreeSessions_ShouldUseKnownProgramNameOrFree()
    {
        // Arrange
        var custom = Program("custom", "My custom split", "custom-session", "free");
        var history = new[] { Log(session: "custom-session"), Log(session: "unknown"), Log(session: "free") };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [custom], Now);

        // Assert
        summary.Programs.ShouldBe([new ProgressCount("Séance libre", 2), new ProgressCount("My custom split", 1)]);
    }

    [Fact]
    public void Summarize_WithEqualProgramCounts_ShouldRetainFirstOccurrenceOrder()
    {
        // Arrange
        var history = new[] { Log(session: "unknown"), Log(session: "pull"), Log(session: "push") };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [Program("push", "Push", "push"), Program("pull", "Pull", "pull")], Now);

        // Assert
        summary.Programs.Select(program => program.Name).ShouldBe(["Séance libre", "Pull", "Push"]);
    }

    [Fact]
    public void Summarize_WithGymFilter_ShouldUseOnlyMatchingSetsAndFullSelectedSessionDuration()
    {
        // Arrange
        var history = new[]
        {
            Log(duration: 40) with { Exercises = [Performance("press", null, "10", "10"), Performance("press", "Basic-Fit", "20", "10")] },
            Log(duration: 60) with { Exercises = [Performance("press", "Basic-Fit", "30", "10")] }
        };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now, "One Air");

        // Assert
        summary.Sessions.ShouldBe(1);
        summary.EnteredSets.ShouldBe(1);
        summary.AverageMinutes.ShouldBe(40);
        summary.Volume.ShouldBe(100);
        summary.Programs.ShouldBe([new ProgressCount("Séance libre", 1)]);
        summary.Areas.Single(area => area.Name == "Pectoraux").Count.ShouldBe(1);
    }

    [Fact]
    public void Summarize_WithAllGyms_ShouldCountMixedGymSessionOnlyOnce()
    {
        // Arrange
        var history = new[] { Log() with { Exercises = [Performance("press", "One Air", "10", "10"), Performance("press", "Basic-Fit", "20", "10")] } };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now);

        // Assert
        summary.Sessions.ShouldBe(1);
        summary.EnteredSets.ShouldBe(2);
        summary.Volume.ShouldBe(300);
    }

    [Fact]
    public void Summarize_WithEmptyMatchingPerformance_ShouldKeepSessionAndDuration()
    {
        // Arrange
        var history = new[] { Log(duration: 35, sets: []) };

        // Act
        var summary = WorkoutProgress.Summarize(history, [Press], [], Now, "One Air");

        // Assert
        summary.Sessions.ShouldBe(1);
        summary.EnteredSets.ShouldBe(0);
        summary.AverageMinutes.ShouldBe(35);
    }

    [Fact]
    public void Summarize_WithEmptyGlobalSession_ShouldMatchWebSessionCounting()
    {
        // Arrange
        var history = new[] { Log(duration: 15) with { Exercises = [] } };

        // Act
        var summary = WorkoutProgress.Summarize(history, [], [], Now);

        // Assert
        summary.Sessions.ShouldBe(1);
        summary.AverageMinutes.ShouldBe(15);
    }

    [Theory]
    [InlineData("rdl", "Pectoraux", "Ischios / fessiers")]
    [InlineData("lying-legcurl", "Jambes", "Ischios / fessiers")]
    [InlineData("glute-machine", "Jambes", "Ischios / fessiers")]
    [InlineData("hipthrust", "Jambes", "Ischios / fessiers")]
    [InlineData("backext", "Dos", "Ischios / fessiers")]
    [InlineData("abduction", "Jambes", "Ischios / fessiers")]
    [InlineData("squat", "Ischios", "Quadriceps")]
    [InlineData("legpress", "Fessiers", "Quadriceps")]
    [InlineData("legext", "Jambes", "Quadriceps")]
    [InlineData("split-squat", "Fessiers", "Quadriceps")]
    [InlineData("goblet", "Jambes", "Quadriceps")]
    [InlineData("custom", "Jambes", "Quadriceps")]
    [InlineData("custom", "Mollets", "Quadriceps")]
    [InlineData("custom", "Ischio-jambiers", "Ischios / fessiers")]
    [InlineData("custom", "Fessiers", "Ischios / fessiers")]
    [InlineData("custom", "Lombaires", "Ischios / fessiers")]
    [InlineData("custom", "Pectoraux", "Pectoraux")]
    [InlineData("custom", "Dos", "Dos")]
    [InlineData("custom", "Épaules", "Épaules")]
    [InlineData("custom", "Biceps", "Biceps")]
    [InlineData("custom", "Triceps", "Triceps")]
    [InlineData("custom", "Quadriceps", "Quadriceps")]
    [InlineData("custom", "Ischios / fessiers", "Ischios / fessiers")]
    public void Summarize_WithPrimaryAreaOrSpecialId_ShouldFollowWebClassification(string id, string muscle, string expected)
    {
        // Arrange
        var exercise = new Exercise { Id = id, Name = "Movement", MuscleGroup = muscle };
        var history = new[] { Log() with { Exercises = [Performance(id, null, "10", "10")] } };

        // Act
        var summary = WorkoutProgress.Summarize(history, [exercise], [], Now);

        // Assert
        summary.Areas.Single(area => area.Name == expected).Count.ShouldBe(1);
        summary.Areas.Sum(area => area.Count).ShouldBe(1);
    }

    [Fact]
    public void Summarize_WithSecondaryTags_ShouldCountOnlyPrimaryArea()
    {
        // Arrange
        var exercise = Press with { Tags = ["Triceps", "Épaules"] };

        // Act
        var summary = WorkoutProgress.Summarize([Log()], [exercise], [], Now);

        // Assert
        summary.Areas.Single(area => area.Name == "Pectoraux").Count.ShouldBe(1);
        summary.Areas.Single(area => area.Name == "Triceps").Count.ShouldBe(0);
        summary.Areas.Single(area => area.Name == "Épaules").Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("Abdominaux")]
    [InlineData("Unknown")]
    [InlineData("")]
    public void Summarize_WithUntrackedArea_ShouldKeepGlobalSetAndVolume(string muscle)
    {
        // Arrange
        var exercise = Press with { MuscleGroup = muscle };

        // Act
        var summary = WorkoutProgress.Summarize([Log()], [exercise], [], Now);

        // Assert
        summary.EnteredSets.ShouldBe(1);
        summary.Volume.ShouldBe(100);
        summary.Areas.Sum(area => area.Count).ShouldBe(0);
    }

    [Fact]
    public void Summarize_WithMissingExercise_ShouldKeepGlobalSetWithoutInventingArea()
    {
        // Arrange
        var history = new[] { Log() };

        // Act
        var summary = WorkoutProgress.Summarize(history, [], [], Now);

        // Assert
        summary.EnteredSets.ShouldBe(1);
        summary.Areas.Sum(area => area.Count).ShouldBe(0);
    }

    [Fact]
    public void Summarize_WithCustomDefinitionOverride_ShouldUseLatestPrimaryMuscle()
    {
        // Arrange
        var customized = Press with { MuscleGroup = "Épaules", IsCustom = true };

        // Act
        var summary = WorkoutProgress.Summarize([Log()], [Press, customized], [], Now);

        // Assert
        summary.Areas.Single(area => area.Name == "Pectoraux").Count.ShouldBe(0);
        summary.Areas.Single(area => area.Name == "Épaules").Count.ShouldBe(1);
    }

    private static WorkoutProgram Program(string id, string name, params string[] sessions) => new()
    {
        Id = id, Name = name, Sessions = sessions.Select(session => new WorkoutSession { Id = session, Name = $"Variant {session}" }).ToArray()
    };
    private static ExercisePerformance Performance(string id, string? gym, string weight, string reps) => new()
    {
        ExerciseId = id, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = reps }]
    };
    private static TrainingSessionLog Log(DateTimeOffset? date = null, int duration = 30, string session = "free", IReadOnlyList<TrainingSet>? sets = null) => new()
    {
        Id = "log", Date = date ?? Now, DurationMinutes = duration, SessionId = session,
        Exercises = [new ExercisePerformance { ExerciseId = "press", Sets = sets ?? [new TrainingSet { Weight = "10", Reps = "10" }] }]
    };
}
