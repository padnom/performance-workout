using Bunit;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Components;
using Performance.Application;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Components;

[TestSubject(typeof(SessionHistory))]
public sealed class SessionFavoritesUiTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;

    public SessionFavoritesUiTests()
    {
        _state = new WorkoutStateService(new MemoryRepository(), new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
    }

    [Fact]
    public async Task HistoryStar_CatalogSession_FavoritesItAndLibraryFilterShowsIt()
    {
        var variant = ProgramFamilies.VisibleSessions(_state.Programs.Single(program => program.Id == "pull"))[0];
        await _state.ReplaceDataAsync(new PerformanceData { History = [Log("done", variant.Id, variant.Exercises[0].ExerciseId)] });
        var history = _context.Render<SessionHistory>();

        history.Find("[data-favorite-log=done]").Click();

        history.WaitForAssertion(() => _state.IsSessionFavorite(variant.Id).ShouldBeTrue());
        history.Find("[data-favorite-log=done]").GetAttribute("aria-pressed").ShouldBe("true");
        history.Find("[role=status]").TextContent.ShouldBe("Séance ajoutée aux favoris.");
        var library = _context.Render<ProgramLibrary>();
        library.Find("[data-filter=favorites]").Click();
        library.FindAll(".library-row").Select(row => row.GetAttribute("data-session")).ShouldBe([variant.Id]);
        history.Find("[data-favorite-log=done]").Click();
        history.WaitForAssertion(() => _state.FavoriteSessionIds.ShouldBeEmpty());
        history.Find("[role=status]").TextContent.ShouldBe("Retirée des favoris.");
    }

    [Fact]
    public async Task HistoryStar_FreeWorkout_SavesInMyWorkoutsAndFavoritesWithoutDuplicate()
    {
        var exercise = _state.AllExercises.First();
        await _state.ReplaceDataAsync(new PerformanceData { History = [Log("free-1", "free", exercise.Id)] });
        var history = _context.Render<SessionHistory>();

        history.Find("[data-favorite-log=free-1]").Click();

        history.WaitForAssertion(() => _state.IsSessionFavorite("history-session-free-1").ShouldBeTrue());
        var mine = _state.Data.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId);
        mine.Sessions.Single().Id.ShouldBe("history-session-free-1");
        mine.Sessions.Single().Name.ShouldStartWith("Séance libre · ");
        mine.Sessions.Single().Exercises.Single().ExerciseId.ShouldBe(exercise.Id);
        history.Find("[role=status]").TextContent.ShouldBe("Ajoutée à Mes séances et aux favoris.");
        history.Find("[data-favorite-log=free-1]").GetAttribute("aria-pressed").ShouldBe("true");
        history.Find("[data-favorite-log=free-1]").Click();
        history.WaitForAssertion(() => _state.FavoriteSessionIds.ShouldBeEmpty());
        _state.Data.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HistoryStar_EnglishLabelsAndMessage()
    {
        var exercise = _state.AllExercises.First();
        await _state.ReplaceDataAsync(new PerformanceData { History = [Log("free-1", "free", exercise.Id)] });
        using (DisplayLanguage.Use("en-US"))
        {
            var history = _context.Render<SessionHistory>();
            history.Find("[data-favorite-log=free-1]").GetAttribute("aria-label").ShouldBe("Add Free workout to favorites");
            history.Find("[data-favorite-log=free-1]").Click();
            history.WaitForAssertion(() => history.Find("[role=status]").TextContent.ShouldBe("Added to My workouts and to favorites."));
            history.Find("[data-favorite-log=free-1]").GetAttribute("aria-label").ShouldBe("Remove Free workout from favorites");
        }
    }

    [Fact]
    public async Task LibraryStar_TogglesSessionAndFavoritesFilterListsOnlyFavoriteSessions()
    {
        var sessions = ProgramFamilies.VisibleSessions(_state.Programs.Single(program => program.Id == "push"));
        var library = _context.Render<ProgramLibrary>();
        library.Find("[data-filter=favorites]").Click();
        library.Find(".library-empty").ShouldNotBeNull();
        library.Find("[data-filter=all]").Click();

        library.Find($"[data-group=push] [data-favorite-session='{sessions[1].Id}']").Click();

        library.WaitForAssertion(() => _state.IsSessionFavorite(sessions[1].Id).ShouldBeTrue());
        library.Find("[data-filter=favorites]").Click();
        library.FindAll(".library-group").Select(group => group.GetAttribute("data-group")).ShouldBe(["push"]);
        library.FindAll(".library-row").Select(row => row.GetAttribute("data-session")).ShouldBe([sessions[1].Id]);
        library.Find("[data-favorite-session]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    private static TrainingSessionLog Log(string id, string sessionId, string exerciseId) => new()
    {
        Id = id, SessionId = sessionId, DurationMinutes = 30, Date = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
        Exercises = [new ExercisePerformance { ExerciseId = exerciseId, Gym = "One Air", Sets = [new TrainingSet { Weight = "40", Reps = "8" }] }]
    };

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
}
