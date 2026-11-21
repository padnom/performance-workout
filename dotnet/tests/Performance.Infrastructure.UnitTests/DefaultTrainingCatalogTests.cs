using JetBrains.Annotations;
using Performance.Infrastructure;
using Shouldly;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(DefaultTrainingCatalog))]
public sealed class DefaultTrainingCatalogTests
{
    [Fact]
    public void Exercises_OriginalWebIdentifiers_ShouldPreserveCompleteCatalogAndLegacyAliases()
    {
        // Arrange
        var expectedIds = """
            bench,incline,fly,shoulder,lateral,rope,plank,pulldown,lowrow,row,rear,curl,hammer-curl,abs,
            squat,rdl,legcurl,glute,legext,calf,abduction,incline-db,pulldown-u,dip,row-u,lateral-u,
            biceps-u,triceps-u,pallof,lateral-a,rear-a,curl-a,hammer-a,rope-a,overhead-a,abs-a,
            lf-abdominal,lf-torso-rotation,lf-chest-press,lf-incline-press,lf-shoulder-press,
            lf-lateral-raise,lf-pulldown,lf-row,lf-biceps,lf-triceps,lf-leg-press,lf-leg-extension,
            lf-seated-leg-curl,lf-lying-leg-curl,lf-glute,lf-adduction,lf-abduction,lf-calf,
            lf-back-extension,bench-db,incline-bar,cable-fly,pushup,shoulder-db,lateral-db,pullup,
            cable-row,row-db,pullover,facepull,curl-cable,legpress,goblet,split-squat,hipthrust,
            backext,close-bench,preacher,pushdown-bar
            """.Split(',', StringSplitOptions.TrimEntries);
        var catalog = new DefaultTrainingCatalog();

        // Act
        var exerciseIds = catalog.Exercises.Where(exercise => expectedIds.Contains(exercise.Id)).Select(exercise => exercise.Id);

        // Assert
        exerciseIds.ShouldBe(expectedIds);
    }

    [Theory]
    [InlineData("push", "pa,pb,pc,pd")]
    [InlineData("pull", "pua,pub,puc,pud")]
    [InlineData("legs", "la,lb,lc,ld")]
    [InlineData("upper", "ua,ub,uc,ud")]
    [InlineData("arms", "aa,ab,ac,ad")]
    [InlineData("full", "fa,fb,fc,fd")]
    [InlineData("chest-biceps", "cba,cbb,cbc,cbd")]
    [InlineData("back-triceps", "bta,btb,btc,btd")]
    [InlineData("chest-back", "cda,cdb,cdc,cdd")]
    [InlineData("quads-calves", "qca,qcb,qcc,qcd")]
    [InlineData("hams-glutes", "hga,hgb,hgc,hgd")]
    public void Programs_WebCategory_ShouldPreserveBaseAndOfferedCoachingVariants(string categoryId, string variantIds)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();
        var expectedIds = new[] { categoryId }.Concat(variantIds.Split(','));

        // Act
        var program = catalog.Programs.Single(program => program.Id == categoryId);

        // Assert
        program.Sessions.Select(session => session.Id).ShouldBe(expectedIds);
    }

    [Fact]
    public void Programs_AllSessionExercises_ShouldResolveToOriginalExerciseIdentifiers()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();
        var exerciseIds = catalog.Exercises.Select(exercise => exercise.Id).ToHashSet();

        // Act
        var unresolvedIds = catalog.Programs.SelectMany(program => program.Sessions)
            .SelectMany(session => session.Exercises)
            .Select(exercise => exercise.ExerciseId)
            .Where(exerciseId => !exerciseIds.Contains(exerciseId));

        // Assert
        unresolvedIds.ShouldBeEmpty();
    }

    [Fact]
    public void Programs_PushCoachingVariant_ShouldUseWebExerciseOrderAndCanonicalIdentifiers()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var session = catalog.Programs.Single(program => program.Id == "push")
            .Sessions.Single(session => session.Id == "pa");

        // Assert
        session.Exercises.Select(exercise => exercise.ExerciseId)
            .ShouldBe(["bench", "incline", "cable-fly", "shoulder-db", "lateral", "rope"]);
        session.Exercises.Select(exercise => exercise.Order).ShouldBe([0, 1, 2, 3, 4, 5]);
    }

    [Theory]
    [InlineData("bench", "4 × 8–12", "2–3 min")]
    [InlineData("incline", "4 × 8–12", "2–3 min")]
    [InlineData("cable-fly", "4 × 10–15", "1–2 min")]
    [InlineData("lateral", "4 × 12–20", "1–2 min")]
    public void Programs_PushCoachingExercise_ShouldPreserveWebVariantTargetsAndRest(string exerciseId, string target, string rest)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var exercise = catalog.Programs.Single(program => program.Id == "push")
            .Sessions.Single(session => session.Id == "pa").Exercises.Single(exercise => exercise.ExerciseId == exerciseId);

        // Assert
        exercise.RepTarget.ShouldBe(target);
        exercise.Rest.ShouldBe(rest);
        exercise.Sets.ShouldBe(4);
    }

    [Theory]
    [InlineData("push", "Push", "Pectoraux · Épaules · Triceps", "#ff5c57")]
    [InlineData("pa", "Push · A · Barre & incliné", "Barre, presse inclinée, poulie, épaules et triceps", "#ff5c57")]
    [InlineData("cba", "Pectoraux + biceps · A · Équilibré", "Trois angles pectoraux et deux angles de curl", "#ff6b76")]
    public void Programs_WebSession_ShouldPreserveDisplayedMetadata(string id, string name, string focus, string color)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var session = catalog.Programs.SelectMany(program => program.Sessions).Single(session => session.Id == id);

        // Assert
        session.Name.ShouldBe(name);
        session.Focus.ShouldBe(focus);
        session.Color.ShouldBe(color);
    }

    [Fact]
    public void Exercises_LegacyUpperPulldown_ShouldPreserveBaselinePrescriptionAndMachineDetails()
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var exercise = catalog.Exercises.Single(exercise => exercise.Id == "pulldown-u");

        // Assert
        exercise.Name.ShouldBe("Hammer Front Lat Pulldown");
        exercise.AlternativeName.ShouldBe("Life Fitness Pulldown / tractions");
        exercise.MuscleGroup.ShouldBe("Dos");
        exercise.RepTarget.ShouldBe("4 × 8–12");
        exercise.Rest.ShouldBe("2 min");
        exercise.Variants.Select(variant => variant.Gym).ShouldBe(["One Air"]);
        exercise.Tags.ShouldContain("Tirage vertical");
        exercise.Link.ShouldBe("https://shop.lifefitness.com/products/hammer-strength-plate-loaded-iso-lateral-wide-pulldown");
    }

    [Theory]
    [InlineData("push")]
    [InlineData("pull")]
    [InlineData("legs")]
    [InlineData("upper")]
    [InlineData("arms")]
    public void Programs_ClassicFamilies_ShouldNameEveryVariantWithSuffixAndFocus(string programId)
    {
        // Arrange
        var catalog = new DefaultTrainingCatalog();

        // Act
        var variants = catalog.Programs.Single(program => program.Id == programId).Sessions.Where(session => session.Id != programId).ToArray();

        // Assert
        variants.Length.ShouldBe(4);
        variants.ShouldAllBe(session => session.Name.Split(" · ", StringSplitOptions.None).Length == 3 && !string.IsNullOrWhiteSpace(session.Focus));
    }
}
