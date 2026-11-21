using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(ExerciseMuscles))]
public sealed class ExerciseMusclesTests
{
    [Theory]
    [InlineData("Pectoraux", "Pectoraux")]
    [InlineData("biceps", "Biceps")]
    [InlineData("Epaile", "Épaules")]
    [InlineData("Isciaux", "Ischio-jambiers")]
    [InlineData("Ischio", "Ischio-jambiers")]
    [InlineData("Ischios / fessiers", "Ischio-jambiers")]
    [InlineData("Mollet", "Mollets")]
    [InlineData("Haut pectoraux", "Pectoraux")]
    [InlineData("Arrière d'épaule", "Arrière d’épaule")]
    public void Canonical_WithKnownOrMistypedMuscle_ShouldReturnKnownMuscle(string value, string expected) =>
        ExerciseMuscles.Canonical(value).ShouldBe(expected);

    [Theory]
    [InlineData("Machine")]
    [InlineData("Polyarticulaire")]
    [InlineData("Isolation")]
    [InlineData("Unilatéral")]
    [InlineData("Tirage vertical")]
    [InlineData("Haltères")]
    [InlineData("")]
    [InlineData(null)]
    public void Canonical_WithNonMuscleTag_ShouldReturnNull(string? value) => ExerciseMuscles.Canonical(value).ShouldBeNull();

    [Fact]
    public void Primary_WithStoredMuscleStillTagged_ShouldKeepStoredMuscle()
    {
        var exercise = new Exercise { Id = "abduction", Name = "Abduction", MuscleGroup = "Fessiers", Tags = ["Jambes", "Fessiers"] };

        ExerciseMuscles.Primary(exercise).ShouldBe("Fessiers");
    }

    [Fact]
    public void Primary_WithStaleStoredMuscle_ShouldUseFirstPreciseMuscleTag()
    {
        var exercise = new Exercise { Id = "x", Name = "X", MuscleGroup = "Dos", Tags = ["Machine", "Jambes", "Quadriceps"] };

        ExerciseMuscles.Primary(exercise).ShouldBe("Quadriceps");
    }

    [Fact]
    public void Primary_WithoutMuscleTagOrKnownStoredMuscle_ShouldReturnStoredText()
    {
        var exercise = new Exercise { Id = "x", Name = "X", MuscleGroup = " Cardio ", Tags = ["Machine"] };

        ExerciseMuscles.Primary(exercise).ShouldBe("Cardio");
    }

    [Fact]
    public void EditableTags_WithoutMuscleTag_ShouldPrependStoredMuscle()
    {
        var exercise = new Exercise { Id = "x", Name = "Poulie haut", MuscleGroup = "Haut pectoraux", Tags = ["Poulie"] };

        ExerciseMuscles.EditableTags(exercise).ShouldBe(["Pectoraux", "Poulie"]);
    }

    [Fact]
    public void EditableTags_WithPrimaryAfterOtherTags_ShouldMovePrimaryFirstAndKeepItsSpelling()
    {
        var exercise = new Exercise { Id = "x", Name = "Traction", MuscleGroup = "Biceps", Tags = ["Dos", "Poulie", "biceps"] };

        ExerciseMuscles.EditableTags(exercise).ShouldBe(["biceps", "Dos", "Poulie"]);
    }

    [Fact]
    public void FromTags_ShouldReturnFirstMuscleTagOrEmpty()
    {
        ExerciseMuscles.FromTags(["Machine", "épaules", "Triceps"]).ShouldBe("Épaules");
        ExerciseMuscles.FromTags(["Machine"]).ShouldBe("");
    }
}
