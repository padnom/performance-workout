using Performance.App.UnitTests.Setup;
using JetBrains.Annotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Layout;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(Home))]
public sealed class HomeTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly Clock _clock = new();
    private readonly Repository _repository = new();
    private readonly DefaultTrainingCatalog _catalog = new();
    private readonly WorkoutStateService _state;
    private NavigationManager Navigation => _context.Services.GetRequiredService<NavigationManager>();

    public HomeTests()
    {
        _state = new(_repository, _catalog, _clock);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton<TimeProvider>(_clock);
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
    }

    [Fact]
    public void Home_EmptyData_ShowsWelcomeMetricsAndLinkToCreateWeek()
    {
        var page = _context.Render<RouteHarness>();
        page.Find("h1").TextContent.ShouldBe("Prêt à progresser ?");
        page.Find(".weekly-metric strong").TextContent.ShouldContain("0");
        page.Find(".weekly-metric").TextContent.ShouldContain("4 avant l’objectif");
        page.Find(".sets-metric strong").TextContent.ShouldContain("0");
        page.Markup.ShouldNotContain("tonnes");
        page.Find(".empty-week").GetAttribute("href").ShouldBe("/programmes");
        page.FindAll(".program-library, .week-move-up").ShouldBeEmpty();
        _repository.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task Home_HistoryMetrics_CountSessionsAndSetsInsideSevenDays()
    {
        var now = _clock.GetUtcNow();
        await _state.ReplaceDataAsync(new PerformanceData { History =
        [
            Log("today", now, "42,5", "10"),
            Log("six", now.AddDays(-6), "100", "10"),
            Log("seven", now.AddDays(-7), "100", "10"),
            Log("twenty-nine", now.AddDays(-29), "100", "10"),
            Log("thirty", now.AddDays(-30), "100", "10"),
            Log("future", now.AddDays(1), "100", "10")
        ] });

        var page = _context.Render<RouteHarness>();

        page.Find(".weekly-metric strong").TextContent.ShouldContain("2");
        page.Find(".weekly-metric").TextContent.ShouldContain("2 avant l’objectif");
        page.Find(".sets-metric strong").TextContent.ShouldContain("2");
        page.Find(".sets-metric").TextContent.ShouldContain("Séries avec répétitions enregistrées");
    }

    [Fact]
    public async Task Home_FourCompletedSessions_ShowsReachedGoal()
    {
        await _state.ReplaceDataAsync(new PerformanceData { History = Enumerable.Range(0, 4).Select(index => Log(index.ToString(), _clock.GetUtcNow().AddDays(-index), "20", "12")).ToArray() });
        var page = _context.Render<RouteHarness>();
        page.Find(".weekly-metric").TextContent.ShouldContain("Objectif atteint ✓");
        page.Find(".weekly-metric").TextContent.ShouldNotContain("avant l’objectif");
    }

    [Fact]
    public async Task Home_WeekCards_PreserveOrderDuplicatesAndRequireConfirmation()
    {
        var sessions = _catalog.Programs.First().Sessions.Where(session => session.Id is "pa" or "pc").ToArray();
        await _state.ReplaceDataAsync(new PerformanceData { Programs = [new WorkoutProgram { Id = "react-week-plan", Name = "Ma semaine", Sessions = [sessions[1], sessions[0], sessions[1]] }] });
        var page = _context.Render<RouteHarness>();
        page.FindAll(".dashboard-session h3").Select(title => title.TextContent).ShouldBe(new[] { "Push · C", "Push · A", "Push · C" });
        page.FindAll(".dashboard-session .session-index").Select(number => number.TextContent).ShouldBe(new[] { "01", "02", "03" });
        page.FindAll(".dashboard-session")[1].Click();
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find(".start-preview .primary-button").Click();
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find("[role=dialog] .primary-button").Click();
        _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("pa");
        new Uri(Navigation.Uri).AbsolutePath.ShouldBe("/seance");
        page.Find(".workout-elapsed").TextContent.ShouldBe("0:00");
        _state.WeekSessions.Count.ShouldBe(3);
    }

    [Fact]
    public void Home_FreeWorkout_StillRequiresExplicitStart()
    {
        var page = _context.Render<RouteHarness>();
        page.Find(".free-workout-card").Click();
        page.Find("[role=dialog] h2").TextContent.ShouldBe("Démarrer la séance ?");
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find("[role=dialog] .primary-button").Click();
        _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("free");
    }

    [Fact]
    public void Home_FreeWorkout_SelectedGymOpensExerciseSearchAfterOneConfirmation()
    {
        // Arrange
        var page = _context.Render<RouteHarness>();
        page.Find(".free-workout-card").Click();
        page.Find(".gym-field select").Change("Basic-Fit");

        // Act
        page.Find("[role=dialog] .primary-button").Click();

        // Assert
        page.WaitForAssertion(() => page.Find(".exercise-picker").ShouldNotBeNull());
        _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("free");
        _state.Data.ActiveWorkout.Session.Exercises.ShouldBeEmpty();
        _state.Data.ActiveWorkout.DefaultGym.ShouldBe("Basic-Fit");
        new Uri(Navigation.Uri).AbsolutePath.ShouldBe("/seance");
        new Uri(Navigation.Uri).Query.ShouldBe("?ajouter=1");
        page.Find("[aria-label='Rechercher un exercice à ajouter']").ShouldNotBeNull();
    }

    [Fact]
    public async Task Home_ActiveWorkout_ShowsDashboardAndResumeWithoutLosingDraft()
    {
        var session = _catalog.Programs.First().Sessions.First(session => session.Id == "pa");
        await _state.StartAsync(session, "One Air");
        await _state.UpdateSetAsync(session.Exercises[0].ExerciseId, 0, new TrainingSet { Weight = "40", Reps = "8" });
        var saved = _state.Data.ActiveWorkout;
        var saves = _repository.Saves;
        var page = _context.Render<RouteHarness>();
        page.Find("h1").TextContent.ShouldBe("Prêt à progresser ?");
        page.Find(".resume-workout").GetAttribute("href").ShouldBe("/seance");
        page.Find(".free-workout-card").HasAttribute("disabled").ShouldBeTrue();
        page.FindAll(".set-row").ShouldBeEmpty();
        Navigation.NavigateTo("/seance");
        page.WaitForAssertion(() => page.Find(".workout-elapsed").ShouldNotBeNull());
        page.Find("input[aria-label^='Charge série 1']").GetAttribute("value").ShouldBe("40");
        Navigation.NavigateTo("/");
        page.WaitForAssertion(() => page.Find(".dashboard").ShouldNotBeNull());
        _state.Data.ActiveWorkout.ShouldBeSameAs(saved);
        _repository.Saves.ShouldBe(saves);
    }

    [Fact]
    public void ProgramsRoute_ShowsEditorAndLibraryWithoutDashboard()
    {
        Navigation.NavigateTo("/programmes");
        var page = _context.Render<RouteHarness>();
        page.Find("h1").TextContent.ShouldBe("Choisis ta séance");
        page.Find(".library-heading").TextContent.ShouldContain("44 séances");
        page.Find(".week-plan").ShouldNotBeNull();
        page.Find(".program-library").ShouldNotBeNull();
        page.FindAll(".dashboard, .free-workout-card").ShouldBeEmpty();
    }

    [Fact]
    public void Home_Navigation_RoutesToSeparatePages()
    {
        var page = _context.Render<RouteHarness>();
        Navigation.NavigateTo("/programmes");
        page.WaitForAssertion(() => page.Find("h1").TextContent.ShouldBe("Choisis ta séance"));
        page.Find(".program-library").ShouldNotBeNull();
        page.FindAll(".dashboard").ShouldBeEmpty();
        Navigation.NavigateTo("/");
        page.WaitForAssertion(() => page.Find(".dashboard").ShouldNotBeNull());
        page.FindAll(".program-library").ShouldBeEmpty();
    }

    [Fact]
    public void Navigation_HasSeparateHomeAndProgramsLinksWithCorrectActiveState()
    {
        var page = _context.Render<MainLayout>();
        var links = page.FindAll(".bottom-nav a");
        links.Count.ShouldBe(4);
        page.Find(".bottom-nav button[aria-label=Plus]").ShouldNotBeNull();
        links.Single(link => link.TextContent.Contains("Accueil")).GetAttribute("href").ShouldBe("/");
        links.Single(link => link.TextContent.Contains("Programmes")).GetAttribute("href").ShouldBe("/programmes");
        page.Find(".bottom-nav a.active").TextContent.ShouldContain("Accueil");
        Navigation.NavigateTo("/programmes");
        page.Find(".bottom-nav a.active").TextContent.ShouldContain("Programmes");
    }

    [Fact]
    public async Task Programs_ActiveWorkout_CanEditWeekWithoutReplacingDraft()
    {
        var session = _catalog.Programs.First().Sessions.First(session => session.Id == "pa");
        await _state.StartAsync(session, "One Air");
        var saved = _state.Data.ActiveWorkout;
        Navigation.NavigateTo("/programmes");
        var page = _context.Render<RouteHarness>();

        page.Find("[data-session=pc] .add-week").Click();
        page.Find(".start-library").Click();

        _state.WeekSessions.Single().Id.ShouldBe("pc");
        _state.Data.ActiveWorkout.ShouldBeSameAs(saved);
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        page.Find(".notice").TextContent.ShouldContain("Une séance est déjà en cours");
        page.Find(".resume-workout").GetAttribute("href").ShouldBe("/seance");
    }

    [Fact]
    public async Task Workout_Finish_ReturnsToHomeAndUpdatesMetrics()
    {
        var session = _catalog.Programs.First().Sessions.First(session => session.Id == "pa");
        await _state.StartAsync(session, "One Air");
        await _state.UpdateSetAsync(session.Exercises[0].ExerciseId, 0, new TrainingSet { Weight = "100", Reps = "10" });
        Navigation.NavigateTo("/seance");
        var page = _context.Render<RouteHarness>();

        page.Find(".finish-workout[aria-label='Terminer la séance']").Click();
        page.Find("[role=dialog] .primary-button").Click();

        new Uri(Navigation.Uri).AbsolutePath.ShouldBe("/");
        page.Find(".weekly-metric strong").TextContent.ShouldContain("1");
        page.Find(".sets-metric strong").TextContent.ShouldContain("1");
        _state.Data.ActiveWorkout.ShouldBeNull();
        _state.Data.History.Count.ShouldBe(1);
    }

    [Fact]
    public void Home_FailedStart_RemainsOnConfirmationWithoutChangingRoute()
    {
        var page = _context.Render<RouteHarness>();
        page.Find(".free-workout-card").Click();
        _repository.FailWrites = true;

        page.Find("[role=dialog] .primary-button").Click();

        _state.Data.ActiveWorkout.ShouldBeNull();
        new Uri(Navigation.Uri).AbsolutePath.ShouldBe("/");
        page.Find("[role=dialog] .notice").TextContent.ShouldContain("L’enregistrement a échoué");
    }

    [Fact]
    public async Task Home_NextSelection_UsesLatestSavedGymAfterNavigation()
    {
        var page = _context.Render<RouteHarness>();
        await _state.SetGymAsync("Basic-Fit");
        page.Find(".free-workout-card").Click();
        page.Find("[role=dialog] select").GetAttribute("value").ShouldBe("Basic-Fit");
        page.Find("[role=dialog] .primary-button").Click();
        _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit");
    }

    private static TrainingSessionLog Log(string id, DateTimeOffset date, string weight, string reps) => new()
    {
        Id = id, SessionId = "free", Date = date,
        Exercises = [new ExercisePerformance { ExerciseId = "bench", Sets = [new TrainingSet { Weight = weight, Reps = reps }] }]
    };
    public void Dispose() => _context.Dispose();
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-02T12:00:00Z");
    }
    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public int Saves { get; private set; }
        public bool FailWrites { get; set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test write failure");
            _data = data;
            Saves++;
            return ValueTask.CompletedTask;
        }
    }
}
