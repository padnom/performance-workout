using System.Globalization;
using Performance.Application;

namespace Performance.App.Services;

/// <summary>Language chosen in the settings ("auto", "fr" or "en"), kept on the device outside backups.</summary>
public interface ILanguagePreference
{
    string Value { get; set; }
}

public static class LanguagePreferenceExtensions
{
    /// <summary>Saves the choice and switches the displayed language at once.</summary>
    public static void Change(this ILanguagePreference preference, string value)
    {
        preference.Value = DisplayLanguage.Choices.Contains(value) ? value : DisplayLanguage.Auto;
        DisplayLanguage.Apply(preference.Value, CultureInfo.InstalledUICulture);
    }
}
