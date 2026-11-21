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

namespace Performance.App.UnitTests.Pages;

public sealed class ProgramLibraryVariantsTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;

    public ProgramLibraryVariantsTests()
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
    public void Library_LegsVariantB_ShowsFocusAndSpecificExercises()
    {
        // Act
        var page = _context.Render<Programs>();
        page.WaitForAssertion(() => page.FindAll(".library-row").Count.ShouldBeGreaterThan(0));

        // Assert
        var row = page.Find("[data-session=lb] .library-open");
        row.QuerySelector("strong")!.TextContent.ShouldBe("Jambes · B · Presse & fente");
        row.QuerySelector(".library-focus")!.TextContent.ShouldBe("Squat, presse, fente bulgare, leg curl et leg extension");
        row.QuerySelector(".library-specific")!.TextContent.ShouldBe("Spécifique : Presse à cuisses · Fente bulgare");
    }

    [Fact]
    public void Library_GroupsWithoutVariants_HaveNoSpecificLine()
    {
        // Act
        var page = _context.Render<Programs>();
        page.WaitForAssertion(() => page.FindAll(".library-row").Count.ShouldBeGreaterThan(0));

        // Assert
        var ownGroups = page.FindAll(".library-group").Where(group => group.GetAttribute("data-group") is not ("push" or "pull" or "legs" or "upper" or "arms" or "full"
            or "chest-biceps" or "back-triceps" or "chest-back" or "quads-calves" or "hams-glutes"));
        ownGroups.SelectMany(group => group.QuerySelectorAll(".library-specific")).ShouldBeEmpty();
    }

    [Fact]
    public void Library_InEnglish_ShowsOnlyHereLabel()
    {
        using (DisplayLanguage.Use("en-US"))
        {
            // Act
            var page = _context.Render<Programs>();
            page.WaitForAssertion(() => page.FindAll(".library-row").Count.ShouldBeGreaterThan(0));

            // Assert
            page.Find("[data-session=lb] .library-specific").TextContent.ShouldStartWith("Only here: ");
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
