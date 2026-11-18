using JetBrains.Annotations;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Components;
using Performance.App.Pages;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Components;

[TestSubject(typeof(SessionHistory))]
public sealed class SessionHistoryTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;

    public SessionHistoryTests()
    {
        _state = new WorkoutStateService(new MemoryRepository(), new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
    }

    [Fact]
    public async Task MixedGyms_ShowNamesAndSeparateAllNonemptySets()
    {
        var exercise = _state.AllExercises.First();
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History = [new TrainingSessionLog
            {
                Id = "mixed", SessionId = "free", DurationMinutes = 35,
                Date = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
                Exercises =
                [
                    new ExercisePerformance { ExerciseId = exercise.Id, Gym = "One Air", Sets =
                        [new TrainingSet { Weight = "42,5", Reps = "8", Rir = "2" }, new TrainingSet(), new TrainingSet { Rir = "3" }] },
                    new ExercisePerformance { ExerciseId = exercise.Id, Gym = "Basic-Fit", Sets =
                        [new TrainingSet { Weight = "70", Reps = "10", Rir = "1" }] }
                ]
            }]
        });

        var component = _context.Render<SessionHistory>();

        component.Find("h2").TextContent.ShouldBe("Séance libre");
        component.Find(".count").TextContent.ShouldBe("35 min");
        component.Markup.ShouldNotContain("kg de volume");
        component.FindAll(".history-exercise").Count.ShouldBe(2);
        component.FindAll(".history-exercise h3").ShouldAllBe(heading => heading.TextContent.Contains(exercise.Name));
        component.FindAll(".history-exercise small").Select(label => label.TextContent.Trim()).ShouldBe(["· One Air", "· Basic-Fit"]);
        component.FindAll(".history-set").Count.ShouldBe(2);
        component.FindAll(".history-set")[0].QuerySelectorAll("span").Select(cell => cell.TextContent).ShouldBe(["42,5", "8"]);
        component.Markup.ShouldNotContain("RIR");
        _state.Data.History[0].Exercises[0].Sets[2].Rir.ShouldBe("3");
    }

    [Fact]
    public async Task MissingCatalogReferences_ShowReadableFallbacks()
    {
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History = [new TrainingSessionLog
            {
                Id = "old", SessionId = "removed-session", Exercises =
                [new ExercisePerformance { ExerciseId = "removed-exercise", Sets = [new TrainingSet { Reps = "8" }] }]
            }]
        });

        var component = _context.Render<SessionHistory>();

        component.Find("h2").TextContent.ShouldBe("Séance");
        component.Find(".history-exercise h3").TextContent.ShouldContain("Exercice inconnu");
        component.Find(".history-exercise small").TextContent.ShouldContain("One Air");
        component.Find(".history-set").QuerySelectorAll("span").Select(cell => cell.TextContent).ShouldBe(["—", "8"]);
    }

    [Fact]
    public async Task RecentLimit_ShowsNewestTenRecordsInDateOrder()
    {
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History = Enumerable.Range(0, 12).Select(index => new TrainingSessionLog
            {
                Id = $"record-{index}", SessionId = "free", DurationMinutes = index,
                Date = DateTimeOffset.Parse("2026-09-01T10:00:00Z").AddDays(index)
            }).Reverse().ToArray()
        });

        var component = _context.Render<SessionHistory>(parameters => parameters.Add(value => value.RecentLimit, 10));

        component.FindAll(".history-card").Count.ShouldBe(10);
        component.FindAll(".history-card .count").Select(count => count.TextContent).ShouldBe(
            Enumerable.Range(2, 10).Reverse().Select(index => $"{index} min"));
    }

    [Fact]
    public async Task GymFilter_KeepOnlyMatchingRecordsAndSets()
    {
        var session = _state.Programs.First().Sessions.First();
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History =
            [
                new TrainingSessionLog { Id = "other", SessionId = "free", Exercises = [new ExercisePerformance { ExerciseId = "bench", Gym = "One Air" }] },
                new TrainingSessionLog
                {
                    Id = "match", SessionId = session.Id, Exercises =
                    [
                        new ExercisePerformance { ExerciseId = "bench", Gym = "One Air", Sets = [new TrainingSet { Weight = "42", Reps = "8" }] },
                        new ExercisePerformance { ExerciseId = "bench", Gym = "Basic-Fit", Sets = [new TrainingSet { Weight = "70", Reps = "10" }] }
                    ]
                }
            ]
        });

        var component = _context.Render<SessionHistory>(parameters => parameters.Add(value => value.Gym, "Basic-Fit"));

        component.FindAll(".history-card").Count.ShouldBe(1);
        component.Find("h2").TextContent.ShouldBe(session.Name);
        component.FindAll(".history-exercise").Count.ShouldBe(1);
        component.Find(".history-exercise small").TextContent.ShouldContain("Basic-Fit");
        component.Find(".history-set").TextContent.ShouldContain("70");
        component.Markup.ShouldNotContain("42");
    }

    [Fact]
    public async Task Replay_WithoutCallback_HidesButton_AndStoredNameWins()
    {
        await _state.ReplaceDataAsync(new PerformanceData { History = [ReplayLog(_state.AllExercises.First().Id) with { Name = "Jambes du lundi" }] });

        var component = _context.Render<SessionHistory>();

        component.FindAll(".replay-session").ShouldBeEmpty();
        component.Find("h2").TextContent.ShouldBe("Jambes du lundi");
    }

    [Fact]
    public async Task Replay_Click_SendsSessionBuiltFromLoggedExercises()
    {
        var exercises = _state.AllExercises.Take(2).ToArray();
        var log = ReplayLog(exercises[1].Id) with
        {
            Exercises =
            [
                new ExercisePerformance { ExerciseId = exercises[1].Id, Gym = "Basic-Fit", Sets = [new TrainingSet { Weight = "50", Reps = "8" }, new TrainingSet { Weight = "50", Reps = "8" }] },
                new ExercisePerformance { ExerciseId = exercises[0].Id, Gym = "Basic-Fit", Sets = [new TrainingSet { Weight = "30", Reps = "12" }] }
            ]
        };
        await _state.ReplaceDataAsync(new PerformanceData { History = [log] });
        WorkoutSession? received = null;

        var component = _context.Render<SessionHistory>(parameters => parameters.Add(value => value.Replay, (WorkoutSession session) => received = session));
        component.Find(".replay-session").Click();

        received.ShouldNotBeNull();
        received.Name.ShouldBe("Séance libre");
        received.Exercises.Select(plan => (plan.ExerciseId, plan.Sets, plan.Gym)).ShouldBe(
            [(exercises[1].Id, (int?)2, "Basic-Fit"), (exercises[0].Id, (int?)1, "Basic-Fit")]);
    }

    [Fact]
    public async Task Replay_WithUnknownExercisesOnly_ShowsMessageWithoutCallback()
    {
        await _state.ReplaceDataAsync(new PerformanceData { History = [ReplayLog("removed-exercise")] });
        var called = false;

        var component = _context.Render<SessionHistory>(parameters => parameters.Add(value => value.Replay, (WorkoutSession _) => called = true));
        component.Find(".replay-session").Click();

        called.ShouldBeFalse();
        component.Find(".notice").TextContent.ShouldContain("ne peut pas être refaite");
    }

    [Fact]
    public async Task Replay_WhileWorkoutActive_IsDisabled()
    {
        var exercise = _state.AllExercises.First();
        await _state.ReplaceDataAsync(new PerformanceData { History = [ReplayLog(exercise.Id)] });
        (await _state.StartAsync(new WorkoutSession { Id = "free", Name = "Séance libre" }, "One Air")).IsSuccess.ShouldBeTrue();

        var component = _context.Render<SessionHistory>(parameters => parameters.Add(value => value.Replay, (WorkoutSession _) => { }));

        component.Find(".replay-session").HasAttribute("disabled").ShouldBeTrue();
        component.Find(".replay-blocked").TextContent.ShouldContain("séance en cours");
    }

    private static TrainingSessionLog ReplayLog(string exerciseId) => new()
    {
        Id = "replay", SessionId = "free", Date = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
        Exercises = [new ExercisePerformance { ExerciseId = exerciseId, Gym = "One Air", Sets = [new TrainingSet { Weight = "40", Reps = "10" }] }]
    };

    [Fact]
    public void EmptyHistory_ShowsAnExplicitEmptyState()
    {
        var component = _context.Render<SessionHistory>();

        component.FindAll(".history-card").ShouldBeEmpty();
        component.Find(".empty-state").TextContent.ShouldContain("Aucune séance enregistrée");
    }

    [Fact]
    public async Task HistoryPage_HidesRirEverywhereWhilePreservingLegacyValues()
    {
        await _state.ReplaceDataAsync(new PerformanceData { History = [new TrainingSessionLog
        {
            Id = "legacy", SessionId = "free", Exercises = [new ExercisePerformance
            { ExerciseId = "bench", Sets = [new TrainingSet { Weight = "40", Reps = "8", Rir = "2" }] }]
        }] });

        var page = _context.Render<History>();

        page.Markup.ShouldNotContain("RIR");
        page.FindAll(".history-set-header span").Select(cell => cell.TextContent).ShouldBe(new[] { "Série", "kg", "Reps" });
        page.Find(".history-set").QuerySelectorAll("span").Select(cell => cell.TextContent).ShouldBe(new[] { "40", "8" });
        _state.Data.History[0].Exercises[0].Sets[0].Rir.ShouldBe("2");
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
}
