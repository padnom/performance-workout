using JetBrains.Annotations;
using Performance.Application;
using Performance.Infrastructure;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(DefaultTrainingCatalog))]
public sealed class GymTrainingCatalogTests
{
    [Theory]
    [InlineData("gym-one-air", "One Air")]
    [InlineData("gym-basic-fit", "Basic-Fit")]
    public void RecommendedProgram_ShouldHaveFourBalancedSessionsAndOptionalAbs(string id, string gym)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var program = catalog.Programs.Single(program => program.Id == id);

        // Assert
        program.Gym.ShouldBe(gym);
        program.Sessions.Select(session => session.Name).ShouldBe(["Push", "Pull", "Jambes", "Upper"]);
        program.Sessions.Select(session => session.Exercises.Sum(exercise => exercise.Sets)).ShouldBe([20, 16, 20, 20]);
        program.Sessions.ShouldAllBe(session => session.Exercises.Select(exercise => exercise.Order)
            .SequenceEqual(Enumerable.Range(0, session.Exercises.Count)));
        program.Sessions.SelectMany(session => session.Exercises)
            .ShouldAllBe(exercise => exercise.Sets == 4 && exercise.RepTarget == "4 × 10" && exercise.Gym == gym);
        program.Sessions.ShouldAllBe(session => session.OptionalExercises.Count == 2);
        program.Sessions.SelectMany(session => session.OptionalExercises)
            .ShouldAllBe(exercise => exercise.Sets == 2 && exercise.RepTarget == "2 × 10–15" && RestDuration.Parse(exercise.Rest) == 60);
        program.Sessions.SelectMany(session => session.Exercises.Concat(session.OptionalExercises))
            .ShouldAllBe(plan => catalog.Exercises.Single(exercise => exercise.Id == plan.ExerciseId).Variants
                .Any(variant => variant.Gym == gym || variant.Gym == "Toutes"));
    }

    [Theory]
    [InlineData("one-air-push", "lf-chest-press,fly,shoulder,lateral,lf-triceps")]
    [InlineData("one-air-pull", "pulldown,lowrow,rear,lf-biceps")]
    [InlineData("one-air-legs", "squat,rdl,lf-seated-leg-curl,legext,calf")]
    [InlineData("one-air-upper", "incline,lf-row,lf-lateral-raise,rear,overhead-a")]
    public void OneAirSession_ShouldUseMappedMachinesInApprovedOrder(string id, string expected)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var session = catalog.Programs.SelectMany(program => program.Sessions).Single(session => session.Id == id);

        // Assert
        session.Exercises.Select(exercise => exercise.ExerciseId).ShouldBe(expected.Split(','));
        session.OptionalExercises.Select(exercise => exercise.ExerciseId).ShouldBe(["abs", "reverse-crunch"]);
    }

    [Fact]
    public void BasicFitMachines_ShouldRemainGenericAndNotClaimMappedBrands()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var machines = catalog.Exercises.Where(exercise => exercise.Id.StartsWith("basic-fit-", StringComparison.Ordinal)).ToArray();

        // Assert
        machines.Length.ShouldBe(8);
        machines.ShouldAllBe(exercise => exercise.Variants.All(variant => variant.Gym == "Basic-Fit")
            && !exercise.Name.Contains("Hammer", StringComparison.Ordinal) && !exercise.Name.Contains("Life Fitness", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("gym-one-air", "lateral", "lf-lateral-raise", "rear")]
    [InlineData("gym-basic-fit", "lateral-db", "lateral-db", "basic-fit-reverse-pec-deck")]
    public void RecommendedProgram_ShouldTrainLateralAndRearShouldersTwice(string id, string firstLateral, string secondLateral, string rear)
    {
        // Arrange
        var program = new DefaultTrainingCatalog().Programs.Single(program => program.Id == id);

        // Act
        var exercises = program.Sessions.SelectMany(session => session.Exercises).ToArray();

        // Assert
        exercises.Count(exercise => exercise.ExerciseId == rear).ShouldBe(2);
        exercises.Count(exercise => exercise.ExerciseId == firstLateral || exercise.ExerciseId == secondLateral).ShouldBe(2);
        exercises.Where(exercise => exercise.ExerciseId == "rdl").ShouldHaveSingleItem().Rest.ShouldBe("3:00");
    }
}
