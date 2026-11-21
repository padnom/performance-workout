using Performance.Application;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

public sealed class DisplayLanguageTests
{
    [Fact]
    public void SessionText_InEnglish_TranslatesLabelsButKeepsExerciseNames()
    {
        var session = new WorkoutSession
        {
            Id = "s", Name = "Jambes",
            Exercises = [new WorkoutExercise { ExerciseId = "squat", Sets = 4, RepTarget = "6", Rest = "3 min", Gym = "Toutes" }],
            OptionalExercises = [new WorkoutExercise { ExerciseId = "abs" }]
        };
        var catalog = new[] { new Exercise { Id = "squat", Name = "Squat" }, new Exercise { Id = "abs", Name = "Abdos", Sets = 2, RepTarget = "15", Rest = "1 min" } };

        using var _ = DisplayLanguage.Use("en-US");
        SessionText.Format(session, catalog).ShouldBe(string.Join("\n",
            "Performance · Jambes",
            "1. Squat — 4 × 6 · rest 3 min · All gyms",
            "Optional:",
            "2. Abdos — 2 × 15 · rest 1 min"));
    }

    [Fact]
    public void ReleaseNotes_ShowEnglishWhenTranslatedAndFallBackToFrench()
    {
        var translated = ReleaseNotes.For("1.5.0").ShouldNotBeNull();
        var untranslated = new ReleaseNote(new Version(9, 0, 0), ["Seulement en français"]);
        using (DisplayLanguage.Use("en-US"))
        {
            translated.DisplayedItems.ShouldBe(translated.EnglishItems);
            untranslated.DisplayedItems.ShouldBe(["Seulement en français"]);
        }
        using (DisplayLanguage.Use("fr-FR")) translated.DisplayedItems.ShouldBe(translated.Items);
    }

    [Fact]
    public void ReleaseNotes_EnglishTranslationsMatchTheFrenchItems() =>
        ReleaseNotes.All.Where(note => note.EnglishItems is not null)
            .ShouldAllBe(note => note.EnglishItems!.Count == note.Items.Count && note.EnglishItems.All(item => item.Trim().Length > 0));

    [Fact]
    public void ReleaseMarkdown_AddsTheEnglishSectionAfterTheFrenchOne()
    {
        var markdown = ReleaseNotes.ToMarkdown(ReleaseNotes.For("1.5.0").ShouldNotBeNull());
        markdown.ShouldStartWith("## Nouveautés de la version 1.5.0\n\n- ");
        markdown.IndexOf("## What’s new in version 1.5.0", StringComparison.Ordinal).ShouldBeGreaterThan(markdown.IndexOf("Refaire cette séance", StringComparison.Ordinal));
    }
}
