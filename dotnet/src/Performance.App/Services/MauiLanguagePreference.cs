using Performance.Application;

namespace Performance.App.Services;

public sealed class MauiLanguagePreference : ILanguagePreference
{
    private const string Key = "app-language";

    public string Value
    {
        get
        {
            try { return Preferences.Default.Get(Key, DisplayLanguage.Auto); }
            catch { return DisplayLanguage.Auto; }
        }
        set => Preferences.Default.Set(Key, value);
    }
}
