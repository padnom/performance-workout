using Performance.Domain;
using Shouldly;

namespace Performance.Application.Tests;

public sealed class WorkoutWeekPlanTests
{
    [Fact]
    public async Task WeekSessions_WithImportedPlan_ShouldPreserveOrderDuplicatesAndMetadata()
    {
        var service = Create(new Repository(new PerformanceData { Programs = [Plan([Push, Push, Pull])] }));
        await service.InitializeAsync();
        service.WeekSessions.ShouldBe([Push, Push, Pull]);
    }

    [Fact]
    public async Task WeekSessions_WithoutPlan_ShouldNotInventDefaultWeek()
    {
        var service = Create(new Repository());
        await service.InitializeAsync();
        service.WeekSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddWeekSessionAsync_WithRepeatedSession_ShouldPersistAndRestoreDuplicates()
    {
        var repository = new Repository();
        var service = Create(repository);
        (await service.AddWeekSessionAsync("push")).IsSuccess.ShouldBeTrue();
        (await service.AddWeekSessionAsync("push")).IsSuccess.ShouldBeTrue();
        (await service.AddWeekSessionAsync("pull")).IsSuccess.ShouldBeTrue();
        var reopened = Create(repository);
        await reopened.InitializeAsync();
        reopened.WeekSessions.ShouldBe([Push, Push, Pull]);
        repository.Stored.Programs.ShouldHaveSingleItem().Id.ShouldBe("react-week-plan");
    }

    [Fact]
    public async Task MoveWeekSessionAsync_WithDuplicateAndCustomMetadata_ShouldMoveOccurrenceOnly()
    {
        var firstPush = Push with { Name = "First push" };
        var service = Create(new Repository(new PerformanceData { Programs = [Plan([firstPush, Pull, Push])] }));
        (await service.MoveWeekSessionAsync(2, -1)).IsSuccess.ShouldBeTrue();
        service.WeekSessions.ShouldBe([firstPush, Push, Pull]);
        (await service.MoveWeekSessionAsync(0, 1)).IsSuccess.ShouldBeTrue();
        service.WeekSessions.ShouldBe([Push, firstPush, Pull]);
    }

    [Fact]
    public async Task RemoveWeekSessionAsync_WithDuplicates_ShouldRemoveSelectedOccurrenceAndPersistEmptyWeek()
    {
        var repository = new Repository(new PerformanceData { Programs = [Plan([Push, Push])] });
        var service = Create(repository);
        (await service.RemoveWeekSessionAsync(1)).IsSuccess.ShouldBeTrue();
        service.WeekSessions.ShouldBe([Push]);
        (await service.RemoveWeekSessionAsync(0)).IsSuccess.ShouldBeTrue();
        var reopened = Create(repository);
        await reopened.InitializeAsync();
        reopened.WeekSessions.ShouldBeEmpty();
        repository.Stored.Programs.ShouldHaveSingleItem().Id.ShouldBe("react-week-plan");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task AddWeekSessionAsync_WithUnknownSession_ShouldRejectWithoutSaving(string? sessionId)
    {
        var repository = new Repository();
        var service = Create(repository);
        (await service.AddWeekSessionAsync(sessionId!)).Error.ShouldBe("UnknownSession");
        repository.Writes.ShouldBe(0);
        service.WeekSessions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1, 1, "InvalidWeekSessionIndex")]
    [InlineData(3, -1, "InvalidWeekSessionIndex")]
    [InlineData(0, -1, "InvalidWeekMove")]
    [InlineData(2, 1, "InvalidWeekMove")]
    [InlineData(0, 0, "InvalidWeekMove")]
    [InlineData(0, 2, "InvalidWeekMove")]
    [InlineData(1, int.MaxValue, "InvalidWeekMove")]
    public async Task MoveWeekSessionAsync_WithInvalidPosition_ShouldRejectWithoutSaving(int index, int delta, string error)
    {
        var repository = new Repository(new PerformanceData { Programs = [Plan([Push, Push, Pull])] });
        var service = Create(repository);
        (await service.MoveWeekSessionAsync(index, delta)).Error.ShouldBe(error);
        repository.Writes.ShouldBe(0);
        service.WeekSessions.ShouldBe([Push, Push, Pull]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task RemoveWeekSessionAsync_WithInvalidPosition_ShouldRejectWithoutSaving(int index)
    {
        var repository = new Repository(new PerformanceData { Programs = [Plan([Push, Pull])] });
        (await Create(repository).RemoveWeekSessionAsync(index)).Error.ShouldBe("InvalidWeekSessionIndex");
        repository.Writes.ShouldBe(0);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("move")]
    [InlineData("remove")]
    public async Task WeekMutation_WhenWriteFails_ShouldNotPublishChanges(string operation)
    {
        var original = new PerformanceData { Programs = [Plan([Push, Pull])] };
        var repository = new Repository(original) { FailWrites = true };
        var service = Create(repository);
        var result = operation switch
        {
            "add" => await service.AddWeekSessionAsync("push"),
            "move" => await service.MoveWeekSessionAsync(0, 1),
            _ => await service.RemoveWeekSessionAsync(0)
        };
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task WeekMutation_WithOtherData_ShouldPreserveProgramsHistoryDraftAndPlanId()
    {
        var custom = new WorkoutProgram { Id = "custom", Name = "Custom", Sessions = [Pull with { Id = "custom-pull" }] };
        var data = new PerformanceData { Programs = [custom, Plan([Push])], ActiveWorkout = new ActiveWorkout { Session = Push },
            History = [new TrainingSessionLog { Id = "log", SessionId = "push" }] };
        var service = Create(new Repository(data));
        (await service.AddWeekSessionAsync("custom-pull")).IsSuccess.ShouldBeTrue();
        service.WeekSessions.Select(session => session.Id).ShouldBe(["push", "custom-pull"]);
        service.Data.Programs[0].ShouldBeSameAs(custom);
        service.Data.Programs[1].Name.ShouldBe("My imported week");
        service.Data.ActiveWorkout.ShouldBeSameAs(data.ActiveWorkout);
        service.Data.History.ShouldBeSameAs(data.History);
    }

    [Fact]
    public async Task AddWeekSessionAsync_WithConcurrentAdds_ShouldKeepEveryOccurrence()
    {
        var repository = new Repository { YieldWrites = true };
        var service = Create(repository);
        var results = await Task.WhenAll(service.AddWeekSessionAsync("push"), service.AddWeekSessionAsync("push"), service.AddWeekSessionAsync("pull"));
        results.All(result => result.IsSuccess).ShouldBeTrue();
        service.WeekSessions.Count.ShouldBe(3);
        service.WeekSessions.Count(session => session.Id == "push").ShouldBe(2);
        service.WeekSessions.Count(session => session.Id == "pull").ShouldBe(1);
    }

    private static readonly WorkoutSession Push = new() { Id = "push", Name = "Push · A", Color = "red", Exercises = [new WorkoutExercise { ExerciseId = "press" }] };
    private static readonly WorkoutSession Pull = new() { Id = "pull", Name = "Pull · A", Color = "blue" };
    private static WorkoutProgram Plan(IReadOnlyList<WorkoutSession> sessions) => new() { Id = "react-week-plan", Name = "My imported week", Sessions = sessions };
    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), TimeProvider.System);

    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises { get; } = [new Exercise { Id = "press", Name = "Press" }];
        public IReadOnlyList<WorkoutProgram> Programs { get; } = [new WorkoutProgram { Id = "ppl", Name = "PPL", Sessions = [Push, Pull] }];
    }

    private sealed class Repository(PerformanceData? data = null) : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = data ?? new();
        public bool FailWrites { get; init; }
        public bool YieldWrites { get; init; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Write failure");
            if (YieldWrites) await Task.Yield();
            Stored = data;
            Writes++;
        }
    }
}
