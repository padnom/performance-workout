using System.Globalization;

namespace Performance.Application;

/// <summary>
/// Language used to display the app (French or English). Stored data never changes with it: gyms, tags and
/// error tokens keep their French identifiers and are translated only when shown.
/// </summary>
public static class DisplayLanguage
{
    public const string Auto = "auto";
    public const string French = "fr";
    public const string English = "en";
    public static readonly IReadOnlyList<string> Choices = [Auto, French, English];

    private static readonly CultureInfo DefaultFrench = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo DefaultEnglish = CultureInfo.GetCultureInfo("en-US");
    private static readonly AsyncLocal<CultureInfo?> Scoped = new();
    private static volatile CultureInfo _culture = DefaultFrench;
    private static int _revision;

    /// <summary>Raised after <see cref="Apply"/> so the interface can render again in the new language.</summary>
    public static event Action? Changed;

    public static CultureInfo Culture => Scoped.Value ?? _culture;
    public static string Language => Culture.TwoLetterISOLanguageName == English ? English : French;
    public static bool IsEnglish => Language == English;
    public static int Revision => Volatile.Read(ref _revision);

    /// <summary>Language actually used for a preference ("auto", "fr" or "en"): auto follows the device, English unless it is French.</summary>
    public static string Resolve(string? preference, CultureInfo device) => preference switch
    {
        French => French,
        English => English,
        _ => device.TwoLetterISOLanguageName == French ? French : English,
    };

    /// <summary>Culture for a language, keeping the device's regional formats when it speaks that language.</summary>
    public static CultureInfo CultureFor(string language, CultureInfo device) =>
        device.TwoLetterISOLanguageName == language && !device.IsNeutralCulture && device.Name.Length > 0
            ? device
            : language == English ? DefaultEnglish : DefaultFrench;

    public static void Apply(string? preference, CultureInfo device)
    {
        var culture = CultureFor(Resolve(preference, device), device);
        _culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Interlocked.Increment(ref _revision);
        Changed?.Invoke();
    }

    /// <summary>Uses a culture for the current asynchronous flow only, e.g. in a test, without touching the app-wide language.</summary>
    public static IDisposable Use(string culture)
    {
        var previous = Scoped.Value;
        Scoped.Value = CultureInfo.GetCultureInfo(culture);
        return new Restore(previous);
    }

    private sealed class Restore(CultureInfo? previous) : IDisposable
    {
        public void Dispose() => Scoped.Value = previous;
    }

    /// <summary>Picks the French or English text for the current language.</summary>
    public static string Pick(string french, string english) => IsEnglish ? english : french;
}
