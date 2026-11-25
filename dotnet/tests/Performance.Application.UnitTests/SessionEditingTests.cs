using JetBrains.Annotations;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class SessionEditingTests
{
    [Fact]
    public async Task RenameSessionAsync_MySession_ShouldPersistKeepIdAndFavorite()
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1", "Ancien"), Session("custom-2", "Autre"))] } };
        var service = Create(repository);
        await service.InitializeAsync();
        await service.ToggleSessionFavoriteAsync("custom-1");
        var writes = repository.Writes;

        var result = await service.RenameSessionAsync("custom-1", "  Nouveau nom  ");

        result.IsSuccess.ShouldBeTrue();
        repository.Writes.ShouldBe(writes + 1);
        var sessions = repository.Stored.Programs.Single().Sessions;
        sessions.Select(session => session.Id).ShouldBe(["custom-1", "custom-2"]);
        sessions.Select(session => session.Name).ShouldBe(["Nouveau nom", "Autre"]);
        service.IsSessionFavorite("custom-1").ShouldBeTrue();
        service.CanRenameSession("custom-1").ShouldBeTrue();
    }

    [Fact]
    public async Task RenameSessionAsync_CatalogOrMissingSession_ShouldFailWithoutWriting()
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1", "Ancien"))] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.RenameSessionAsync("push-a", "Nom")).Error.ShouldBe("UnknownSession");
        (await service.RenameSessionAsync("missing", "Nom")).Error.ShouldBe("UnknownSession");

        service.CanRenameSession("push-a").ShouldBeFalse();
        repository.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task RenameSessionAsync_WeekPlanSession_ShouldFail()
    {
        var week = new WorkoutProgram { Id = "react-week-plan", Name = "Semaine", Sessions = [Session("week-1", "Lundi")] };
        var repository = new Repository { Stored = new PerformanceData { Programs = [week] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.RenameSessionAsync("week-1", "Nom")).Error.ShouldBe("UnknownSession");
        repository.Writes.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameSessionAsync_EmptyName_ShouldFail(string name)
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1", "Ancien"))] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.RenameSessionAsync("custom-1", name)).Error.ShouldBe("InvalidSessionName");

        repository.Writes.ShouldBe(0);
        service.Data.Programs.Single().Sessions.Single().Name.ShouldBe("Ancien");
    }

    [Fact]
    public async Task RenameSessionAsync_NameLength_ShouldAcceptLimitAndRejectBeyond()
    {
        var repository = new Repository { Stored = new PerformanceData { Programs = [Mine(Session("custom-1", "Ancien"))] } };
        var service = Create(repository);
        await service.InitializeAsync();

        (await service.RenameSessionAsync("custom-1", new string('a', 161))).Error.ShouldBe("InvalidSessionName");
        (await service.RenameSessionAsync("custom-1", $" {new string('a', 160)} ")).IsSuccess.ShouldBeTrue();

        service.Data.Programs.Single().Sessions.Single().Name.Length.ShouldBe(160);
    }

    private static WorkoutProgram Mine(params WorkoutSession[] sessions) =>
        new() { Id = WorkoutStateService.SessionsProgramId, Name = "Mes séances", Gym = "Toutes", Sessions = sessions };
    private static WorkoutSession Session(string id, string name) =>
        new() { Id = id, Name = name, Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 4, RepTarget = "10", Rest = "1:30" }] };

    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);

    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press", Sets = 4, RepTarget = "10", Rest = "2:00", Variants = [new GymVariant { Gym = "One Air" }] }];
        public IReadOnlyList<WorkoutProgram> Programs =>
        [
            new WorkoutProgram { Id = "push", Name = "Push", Gym = "Toutes", Sessions = [Session("push", "Push"), Session("push-a", "Push A"), Session("push-b", "Push B")] }
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
