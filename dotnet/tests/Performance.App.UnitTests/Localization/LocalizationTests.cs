using System.Globalization;
using Performance.App.Components;
using Performance.App.Resources;
using Performance.Application;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Localization;

public sealed class LocalizationTests
{
    [Fact]
    public void Every_french_text_has_a_non_empty_english_translation()
    {
        var french = Text.All("");
        var english = Text.All("en");
        french.Count.ShouldBeGreaterThan(0);
        french.Keys.Except(english.Keys).ShouldBeEmpty();
        english.Keys.Except(french.Keys).ShouldBeEmpty();
        english.Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => entry.Key).ShouldBeEmpty();
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        var english = Text.All("en");
        foreach (var (key, french) in Text.All(""))
            Placeholders(english[key]).ShouldBe(Placeholders(french), key);
    }

    [Fact]
    public void Texts_follow_the_display_language()
    {
        using (DisplayLanguage.Use("fr-FR")) WorkoutUi.Error("NoActiveWorkout").ShouldBe("Aucune séance en cours.");
        using (DisplayLanguage.Use("en-US")) WorkoutUi.Error("NoActiveWorkout").ShouldBe("No workout in progress.");
    }

    [Fact]
    public void Unknown_error_tokens_use_the_generic_message()
    {
        using var _ = DisplayLanguage.Use("en-US");
        WorkoutUi.Error("SomethingNew").ShouldBe(WorkoutUi.Error(null));
        WorkoutUi.Error(null).ShouldStartWith("Saving failed");
    }

    [Fact]
    public void Known_tags_and_the_all_gyms_value_are_translated_while_custom_tags_stay_as_typed()
    {
        using var _ = DisplayLanguage.Use("en-US");
        WorkoutUi.TagName("Pectoraux").ShouldBe("Chest");
        WorkoutUi.TagName("Mon tag").ShouldBe("Mon tag");
        WorkoutUi.GymName("Toutes").ShouldBe("All gyms");
        WorkoutUi.GymName("One Air").ShouldBe("On Air");
        WorkoutUi.Decimal(12.5m).ShouldBe("12.5");
    }

    [Fact]
    public void French_stays_the_default_and_keeps_french_formats()
    {
        using var _ = DisplayLanguage.Use("fr-FR");
        WorkoutUi.GymName("Toutes").ShouldBe("Toutes");
        WorkoutUi.Decimal(12.5m).ShouldBe("12,5");
    }

    [Theory]
    [InlineData("auto", "fr-FR", "fr")]
    [InlineData("auto", "fr-CA", "fr")]
    [InlineData("auto", "en-GB", "en")]
    [InlineData("auto", "de-DE", "en")]
    [InlineData(null, "de-DE", "en")]
    [InlineData("fr", "en-US", "fr")]
    [InlineData("en", "fr-FR", "en")]
    public void Language_follows_the_device_unless_chosen(string? preference, string device, string expected) =>
        DisplayLanguage.Resolve(preference, CultureInfo.GetCultureInfo(device)).ShouldBe(expected);

    [Fact]
    public void Regional_formats_of_the_device_are_kept_when_it_speaks_the_language()
    {
        DisplayLanguage.CultureFor("en", CultureInfo.GetCultureInfo("en-GB")).Name.ShouldBe("en-GB");
        DisplayLanguage.CultureFor("en", CultureInfo.GetCultureInfo("de-DE")).Name.ShouldBe("en-US");
        DisplayLanguage.CultureFor("fr", CultureInfo.GetCultureInfo("de-DE")).Name.ShouldBe("fr-FR");
    }

    private static string[] Placeholders(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+[^}]*\}").Select(match => match.Value).Order().ToArray();
}
