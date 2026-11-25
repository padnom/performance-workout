using Bunit;
using JetBrains.Annotations;
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

[TestSubject(typeof(Performance.App.Components.SessionStart))]
public sealed class SessionRenameTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;

    public SessionRenameTests()
    {
        var catalog = new DefaultTrainingCatalog();
        _state = new WorkoutStateService(new MemoryRepository(), catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton<IWorkoutFeedback>(new FakeWorkoutFeedback());
        _context.Services.AddSingleton<IBackupFiles>(new FakeBackupFiles());
        _context.Services.AddSingleton<IReactLocalStorageAdapter>(new ReactLocalStorageMigrationAdapter(catalog));
        _context.Services.AddTransient<ImportPerformanceBackupUseCase>();
        _context.Services.AddTransient<ExportPerformanceBackupUseCase>();
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/programmes");
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task<WorkoutSession> SaveMineAsync()
    {
        var plan = new WorkoutExercise { ExerciseId = _state.AllExercises.First().Id, Sets = 3, RepTarget = "10", Rest = "1:30" };
        var session = new WorkoutSession { Id = "mine-1", Name = "Ma séance", Exercises = [plan] };
        (await _state.FavoriteHistorySessionAsync(session)).IsSuccess.ShouldBeTrue();
        return session;
    }

    [Fact]
    public async Task Preview_MySession_RenameShowsNewNameInPreviewAndLibrary()
    {
        await SaveMineAsync();
        var page = _context.Render<Programs>();
        page.Find("[data-session=mine-1] .library-open").Click();

        page.Find(".start-preview .rename-session-button").Click();
        var input = page.Find(".start-preview .rename-session-input");
        input.GetAttribute("value").ShouldBe("Ma séance");
        input.GetAttribute("maxlength").ShouldBe("160");
        input.Input("  Pecs et dos  ");
        page.Find(".start-preview .rename-session-save").Click();

        page.WaitForAssertion(() => page.Find(".start-preview h2").TextContent.ShouldBe("Pecs et dos"));
        page.Find(".start-preview .notice").TextContent.ShouldBe("Séance renommée.");
        page.FindAll(".rename-session-input").ShouldBeEmpty();
        _state.Data.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions[0].Name.ShouldBe("Pecs et dos");
        page.Find("[data-session=mine-1]").TextContent.ShouldContain("Pecs et dos");
    }

    [Fact]
    public async Task Preview_MySession_InvalidNameShowsErrorAndCancelRestoresTitle()
    {
        await SaveMineAsync();
        var page = _context.Render<Programs>();
        page.Find("[data-session=mine-1] .library-open").Click();
        page.Find(".start-preview .rename-session-button").Click();

        page.Find(".rename-session-input").Input("   ");
        page.Find(".rename-session-save").Click();

        page.WaitForAssertion(() => page.Find(".start-preview .notice").TextContent.ShouldBe("Donne à la séance un nom de 1 à 160 caractères."));
        page.Find(".rename-session-cancel").Click();
        page.FindAll(".rename-session-input").ShouldBeEmpty();
        page.Find(".start-preview h2").TextContent.ShouldBe("Ma séance");
    }

    [Fact]
    public void Preview_CatalogSession_HasNoRenameButton()
    {
        var page = _context.Render<Programs>();

        page.Find("[data-session=pc] .library-open").Click();

        page.Find(".start-preview");
        page.FindAll(".rename-session-button").ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_MySession_RenameButtonIsTranslatedInEnglish()
    {
        await SaveMineAsync();
        using (DisplayLanguage.Use("en-US"))
        {
            var page = _context.Render<Programs>();
            page.Find("[data-session=mine-1] .library-open").Click();

            page.Find(".rename-session-button").GetAttribute("aria-label").ShouldBe("Rename");
        }
    }

    [Fact]
    public async Task Preview_ShowsLabeledIconToolbar_AndFavoriteToggleFlipsPressedState()
    {
        await SaveMineAsync();
        var page = _context.Render<Programs>();
        page.Find("[data-session=mine-1] .library-open").Click();
        var wasFavorite = _state.IsSessionFavorite("mine-1");

        var toolbar = page.Find(".start-preview .icon-toolbar");
        toolbar.QuerySelectorAll(".icon-tool").Length.ShouldBe(4);
        toolbar.QuerySelectorAll(".icon-tool > span[aria-hidden=true]").Select(label => label.TextContent).ShouldBe(["Favori", "Renommer", "Exporter", "Texte"]);
        foreach (var button in toolbar.QuerySelectorAll("button"))
        {
            button.GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
            button.GetAttribute("title").ShouldBe(button.GetAttribute("aria-label"));
            button.ClassList.ShouldContain("icon-button");
        }
        page.Find(".start-preview .session-favorite").GetAttribute("aria-pressed").ShouldBe(wasFavorite ? "true" : "false");

        page.Find(".start-preview .session-favorite").Click();

        page.WaitForAssertion(() => page.Find(".start-preview .session-favorite").GetAttribute("aria-pressed").ShouldBe(wasFavorite ? "false" : "true"));
        _state.IsSessionFavorite("mine-1").ShouldBe(!wasFavorite);
        page.Find(".start-preview .session-favorite").ClassList.Contains("is-on").ShouldBe(!wasFavorite);
    }

    [Fact]
    public async Task Preview_CloseButton_ClosesDialog()
    {
        await SaveMineAsync();
        var page = _context.Render<Programs>();
        page.Find("[data-session=mine-1] .library-open").Click();

        page.Find(".start-preview .start-close").GetAttribute("aria-label").ShouldBe("Retour");
        page.Find(".start-preview .start-close").Click();

        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Library_RowButtons_AreIconButtons()
    {
        await SaveMineAsync();
        var page = _context.Render<Programs>();

        var row = page.Find("[data-session=mine-1]");
        foreach (var selector in new[] { ".session-favorite", ".library-start" })
        {
            var button = row.QuerySelector(selector)!;
            button.ClassList.ShouldContain("icon-button");
            button.GetAttribute("title").ShouldBe(button.GetAttribute("aria-label"));
            button.QuerySelector("svg[aria-hidden=true]").ShouldNotBeNull();
        }
        row.QuerySelector(".library-start")!.ClassList.ShouldContain("is-go");
    }

    public void Dispose() => _context.Dispose();

    private sealed class MemoryRepository : IPerformanceDataRepository
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
