using System.Globalization;
using System.Resources;
using Performance.Application;

namespace Performance.App.Resources;

/// <summary>
/// Interface texts, read from Strings.resx (French, default) and Strings.en.resx (English) in the
/// current <see cref="DisplayLanguage"/>. Use <c>@T("Key")</c> in Razor files.
/// </summary>
public static class Text
{
    public const string BaseName = "Performance.App.Resources.Strings";
    private static readonly ResourceManager Manager = new(BaseName, typeof(Text).Assembly);

    public static string T(string key) => Manager.GetString(key, DisplayLanguage.Culture) ?? key;

    public static string T(string key, params object?[] arguments) => string.Format(DisplayLanguage.Culture, T(key), arguments);

    /// <summary>Every key of a language's resource file, without falling back to French; used to check translations.</summary>
    public static IReadOnlyDictionary<string, string> All(string culture)
    {
        var set = Manager.GetResourceSet(culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture), true, false);
        return set is null ? new Dictionary<string, string>()
            : set.Cast<System.Collections.DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => entry.Value as string ?? "");
    }
}
