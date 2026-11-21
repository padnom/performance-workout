using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Application;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Localization;

[CollectionDefinition("DisplayLanguage", DisableParallelization = true)]
public sealed class DisplayLanguageCollection;

[Collection("DisplayLanguage")]
public sealed class SettingsEnglishTests
{
    [Fact]
    public void Settings_RendersEnglishTexts()
    {
        using (DisplayLanguage.Use("en-US"))
        {
            using var setup = new Setup();
            var page = setup.Context.Render<Settings>();
            page.WaitForAssertion(() => page.Find(".notification-settings").ShouldNotBeNull());
            page.Find("h1").TextContent.ShouldBe("Settings");
            page.Find(".notification-settings").TextContent.ShouldContain("No sound");
            page.Markup.ShouldContain("Backup and transfer");
            page.Markup.ShouldContain("Import a backup");
            page.Find(".diagnostic-settings").TextContent.ShouldContain("Version 1.1.0 (2)");
        }
    }

    [Fact]
    public void LanguageSelector_IsShownAndChangingItSetsThePreference()
    {
        var preference = new FakePreference { Value = DisplayLanguage.Auto };
        try
        {
            using var setup = new Setup();
            setup.Context.Services.AddSingleton<ILanguagePreference>(preference);
            var page = setup.Context.Render<Settings>();
            page.WaitForAssertion(() => page.Find(".language-settings select").ShouldNotBeNull());
            page.FindAll(".language-settings option").Count.ShouldBe(3);
            page.Find(".language-settings select").Change("en");
            preference.Value.ShouldBe("en");
        }
        finally { DisplayLanguage.Apply("fr", CultureInfo.GetCultureInfo("fr-FR")); }
    }

    private sealed class FakePreference : ILanguagePreference { public string Value { get; set; } = ""; }

    private sealed class Setup : IDisposable
    {
        public BunitContext Context { get; } = new();
        public Setup()
        {
            var catalog = new DefaultTrainingCatalog();
            Context.Services.AddSingleton(new WorkoutStateService(new Repository(), catalog, TimeProvider.System));
            Context.Services.AddSingleton<IAppDiagnostics>(new Diagnostics());
            Context.Services.AddSingleton<IBackupFiles>(new Files());
            Context.Services.AddSingleton<IReactLocalStorageAdapter>(new ReactLocalStorageMigrationAdapter(catalog));
            Context.Services.AddTransient<ImportPerformanceBackupUseCase>();
            Context.Services.AddTransient<ExportPerformanceBackupUseCase>();
        }
        public void Dispose() => Context.Dispose();
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
    private sealed class Diagnostics : IAppDiagnostics
    {
        public string Version => "1.1.0 (2)";
        public void Record(string operation, TimeSpan elapsed, Exception? exception = null) { }
        public Task ShareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Files : IBackupFiles
    {
        public Task<string?> PickBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareBackupAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
