using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class SessionFavoritesTests
{
    private const string Setting = "training-favorite-sessions";
    private const string LegacySetting = "training-favorite-programs";

    [Fact]
    public async Task ToggleSessionFavoriteAsync_CatalogSession_ShouldAddPersistAndRemove()
    {
        var repository = new Repository();
        var service = Create(repository);

        (await service.ToggleSessionFavoriteAsync("push-a")).IsSuccess.ShouldBeTrue();

        service.IsSessionFavorite("push-a").ShouldBeTrue();
        service.IsSessionFavorite("push-b").ShouldBeFalse();
        repository.Stored.Settings[Setting].ShouldBe("[\"push-a\"]");
        var reopened = Create(repository);
        await reopened.InitializeAsync();
        reopened.FavoriteSessionIds.ShouldBe(["push-a"]);
        (await reopened.ToggleSessionFavoriteAsync("push-a")).IsSuccess.ShouldBeTrue();
        reopened.FavoriteSessionIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToggleSessionFavoriteAsync_UnknownSession_ShouldFailWithoutWriting()
    {
        var repository = new Repository();
        var service = Create(repository);

        (await service.ToggleSessionFavoriteAsync("missing")).Error.ShouldBe("UnknownSession");

        repository.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task DeletedSession_ShouldDisappearFromFavorites()
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1"), Session("custom-2"))],
            Settings = new Dictionary<string, string> { [Setting] = "[\"custom-1\",\"custom-2\"]" } } };
        var service = Create(repository);
        await service.InitializeAsync();
        service.FavoriteSessionIds.Count.ShouldBe(2);

        await service.ReplaceDataAsync(service.Data with { Programs = [Mine(Session("custom-2"))] });

        service.FavoriteSessionIds.ShouldBe(["custom-2"]);
    }

    [Fact]
    public async Task FavoriteHistorySessionAsync_FreeWorkout_ShouldSaveOnceInMySessionsAndFavorite()
    {
        var repository = new Repository();
        var service = Create(repository);
        var session = Session("history-session-log-1");

        (await service.FavoriteHistorySessionAsync(session)).IsSuccess.ShouldBeTrue();
        (await service.FavoriteHistorySessionAsync(session)).IsSuccess.ShouldBeTrue();

        var mine = service.Data.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId);
        mine.Sessions.Select(item => item.Id).ShouldBe(["history-session-log-1"]);
        service.FavoriteSessionIds.ShouldBe(["history-session-log-1"]);
        (await service.ToggleSessionFavoriteAsync("history-session-log-1")).IsSuccess.ShouldBeTrue();
        service.FavoriteSessionIds.ShouldBeEmpty();
        service.Data.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FavoriteHistorySessionAsync_ExistingSession_ShouldOnlyFavorite()
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1"))] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.FavoriteHistorySessionAsync(Session("custom-1") with { Name = "Other" })).IsSuccess.ShouldBeTrue();

        service.Data.Programs.Single().Sessions.Single().Name.ShouldBe("Push");
        service.FavoriteSessionIds.ShouldBe(["custom-1"]);
    }

    [Fact]
    public async Task FavoriteHistorySessionAsync_InvalidOrFull_ShouldFailWithoutWriting()
    {
        var full = Mine(Enumerable.Range(0, 100).Select(index => Session($"custom-{index}")).ToArray());
        var repository = new Repository { Stored = new PerformanceData { Programs = [full] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.FavoriteHistorySessionAsync(Session("new"))).Error.ShouldBe("SessionsProgramFull");
        (await service.FavoriteHistorySessionAsync(Session("new") with { Exercises = [] })).Error.ShouldBe("InvalidProgramSession");

        repository.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task LegacyFavoritePrograms_ShouldBeReadAsVisibleSessionsAndReplacedOnFirstWrite()
    {
        var custom = new WorkoutProgram { Id = "custom-p", Name = "Perso", Gym = "Toutes", Sessions = [Session("c1"), Session("c2")] };
        var repository = new Repository { Stored = new PerformanceData { Programs = [custom], Settings = new Dictionary<string, string>
            { ["theme"] = "dark", [LegacySetting] = "[\"push\",\"custom-p\",\"stale\"]" } } };
        var service = Create(repository);
        await service.InitializeAsync();

        service.FavoriteSessionIds.OrderBy(id => id).ShouldBe(["c1", "c2", "push-a", "push-b"]);
        repository.Writes.ShouldBe(0);
        (await service.ToggleSessionFavoriteAsync("c1")).IsSuccess.ShouldBeTrue();

        repository.Stored.Settings.ContainsKey(LegacySetting).ShouldBeFalse();
        repository.Stored.Settings["theme"].ShouldBe("dark");
        service.FavoriteSessionIds.OrderBy(id => id).ShouldBe(["c2", "push-a", "push-b"]);
    }

    [Fact]
    public async Task SessionSetting_ShouldTakePrecedenceOverLegacyAndEmptyIsNotMigrated()
    {
        var repository = new Repository { Stored = new PerformanceData { Settings = new Dictionary<string, string>
            { [Setting] = "[]", [LegacySetting] = "[\"push\"]" } } };
        var service = Create(repository);
        await service.InitializeAsync();

        service.FavoriteSessionIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateProgramAsync_Favorite_ShouldFavoriteAllItsSessions()
    {
        var service = Create(new Repository());
        var draft = new WorkoutProgram { Id = "d", Name = "Perso", Gym = "One Air", Sessions = [Session("a"), Session("b")] };

        (await service.CreateProgramAsync(draft, favorite: true)).IsSuccess.ShouldBeTrue();

        service.FavoriteSessionIds.OrderBy(id => id).ShouldBe(service.Data.Programs.Single().Sessions.Select(session => session.Id).Order());
        service.FavoriteSessionIds.Count.ShouldBe(2);
    }

    [Fact]
    public void CanFavoriteSession_ShouldOnlyAcceptSessionsTheLibraryShows()
    {
        var service = Create(new Repository());

        service.CanFavoriteSession("push-a").ShouldBeTrue();
        service.CanFavoriteSession("push").ShouldBeFalse();
        service.CanFavoriteSession("missing").ShouldBeFalse();
    }

    private static WorkoutExercise Plan() => new() { ExerciseId = "press", Sets = 4, RepTarget = "10", Rest = "1:30" };
    private static WorkoutSession Session(string id) => new() { Id = id, Name = "Push", Exercises = [Plan()] };
    private static WorkoutProgram Mine(params WorkoutSession[] sessions) => new() { Id = WorkoutStateService.SessionsProgramId, Name = "Mes séances", Gym = "Toutes", Sessions = sessions };
    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);

    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press", Sets = 4, RepTarget = "10", Rest = "2:00", Variants = [new GymVariant { Gym = "One Air" }] }];
        public IReadOnlyList<WorkoutProgram> Programs =>
        [
            new WorkoutProgram { Id = "push", Name = "Push", Gym = "Toutes", Sessions = [Session("push"), Session("push-a"), Session("push-b")] }
        ];
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; set; } = new();
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            Stored = data;
            Writes++;
            return ValueTask.CompletedTask;
        }
    }
}
