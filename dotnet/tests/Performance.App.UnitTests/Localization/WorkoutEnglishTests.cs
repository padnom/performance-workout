using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Components;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Application;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Localization;

[Collection("DisplayLanguage")]
public sealed class WorkoutEnglishTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly WorkoutStateService _state;

    public WorkoutEnglishTests()
    {
        var clock = new Clock();
        _state = new(_repository, new DefaultTrainingCatalog(), clock);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton<TimeProvider>(clock);
    }

    [Fact]
    public async Task Progress_RendersEnglishTextsAndTags()
    {
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History =
            [
                new TrainingSessionLog
                {
                    Id = "a", SessionId = "free", Date = Now.AddDays(-1), DurationMinutes = 30,
                    Exercises = [new ExercisePerformance { ExerciseId = "bench", Gym = "One Air", Sets = [new TrainingSet { Weight = "40", Reps = "8" }] }]
                }
            ]
        });
        using (DisplayLanguage.Use("en-US"))
        {
            var page = _context.Render<Progress>();
            page.Find("h1").TextContent.ShouldBe("My progress");
            page.Find(".program-bars").TextContent.ShouldContain("Free workout");
            page.Find(".program-bars").TextContent.ShouldContain("1 workout");
            page.Find(".area-bars").TextContent.ShouldContain("Chest");
            page.Find(".area-bars").TextContent.ShouldNotContain("Pectoraux");
        }
    }

    [Fact]
    public void Exercises_RendersEnglishTextsAndTags()
    {
        using (DisplayLanguage.Use("en-US"))
        {
            var page = _context.Render<Exercises>();
            page.WaitForAssertion(() => page.FindAll(".catalog-card").Count.ShouldBeGreaterThan(0));
            page.Find("h1").TextContent.ShouldBe("Exercises");
            page.Markup.ShouldContain("Filter by gym");
            page.Markup.ShouldContain("exercise(s)");
            page.Find(".catalog-card .muted").TextContent.ShouldContain("sets");
            page.Markup.ShouldContain("#Chest");
            page.Find(".primary-button").TextContent.ShouldContain("Create");
        }
    }

    [Fact]
    public void TagPicker_ShowsTranslatedTagButtons()
    {
        using (DisplayLanguage.Use("en-US"))
        {
            var page = _context.Render<TagPicker>();
            page.Markup.ShouldContain("Equipment");
            page.FindAll("button").Select(button => button.TextContent).ShouldContain("Chest");
        }
    }

    public void Dispose() => _context.Dispose();
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}
