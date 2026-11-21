namespace Performance.App;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;
    public App(IServiceProvider services)
    {
        _services = services;
        var language = services.GetRequiredService<Services.ILanguagePreference>();
        Performance.Application.DisplayLanguage.Apply(language.Value, System.Globalization.CultureInfo.InstalledUICulture);
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_services.GetRequiredService<MainPage>());
}
