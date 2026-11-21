using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Application;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Localization;

public sealed class ProgramsEnglishTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;

    public ProgramsEnglishTests()
    {
        var catalog = new DefaultTrainingCatalog();
        _state = new WorkoutStateService(new FakeRepository(), catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton<IWorkoutFeedback>(new FakeWorkoutFeedback());
        _context.Services.AddSingleton<IBackupFiles>(new FakeBackupFiles());
        _context.Services.AddSingleton<IReactLocalStorageAdapter>(new ReactLocalStorageMigrationAdapter(catalog));
        _context.Services.AddTransient<ImportPerformanceBackupUseCase>();
        _context.Services.AddTransient<ExportPerformanceBackupUseCase>();
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/programmes");
    }

    [Fact]
    public void Programs_InEnglish_ShowsEnglishTexts()
    {
        using (DisplayLanguage.Use("en-US"))
        {
            // Act
            var page = _context.Render<Programs>();
            page.WaitForAssertion(() => page.FindAll(".library-row").Count.ShouldBeGreaterThan(0));

            // Assert
            page.Find("h1").TextContent.ShouldBe("Pick your workout");
            page.Find(".library-heading").TextContent.ShouldContain("workouts");
            page.Find(".library-search").GetAttribute("aria-label").ShouldBe("Search workouts");
            page.Find("[data-filter=mine]").TextContent.ShouldBe("My workouts");
            page.Find(".library-row .library-start").GetAttribute("aria-label").ShouldStartWith("Start ");
        }
    }

    public void Dispose() => _context.Dispose();

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            _data = data;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWorkoutFeedback : IWorkoutFeedback
    {
        public void NotifyRestCompleted() { }
    }

    private sealed class FakeBackupFiles : IBackupFiles
    {
        public Task<string?> PickBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareBackupAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
