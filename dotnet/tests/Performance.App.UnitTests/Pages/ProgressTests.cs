using JetBrains.Annotations;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Pages;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(Progress))]
public sealed class ProgressTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-02T12:00:00Z");
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly WorkoutStateService _state;

    public ProgressTests()
    {
        var clock = new Clock();
        _state = new(_repository, new DefaultTrainingCatalog(), clock);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton<TimeProvider>(clock);
    }

    [Fact]
    public async Task Overview_Uses28DaysAndGroupsProgramVariants()
    {
        await Seed(Log("a", "pa", -2, 30, "bench", "One Air", "40", "8"),
            Log("b", "pb", -1, 40, "bench", "One Air", "45", "10"),
            Log("old", "pull", -29, 60, "lat", "One Air", "50", "12"),
            Log("future", "legs", 1, 99, "squat", "One Air", "60", "10"));
        var page = _context.Render<Progress>();

        page.Find("h1").TextContent.ShouldBe("Mes progrès");
        page.FindAll(".progress-summary strong").Select(element => element.TextContent).ShouldBe(["2", "2", "35"]);
        page.Find(".program-bars").TextContent.ShouldContain("Push");
        page.Find(".program-bars").TextContent.ShouldContain("2 séances");
        page.Find(".program-bars").TextContent.ShouldNotContain("Pull");
        page.FindAll(".area-bars .progress-bar-row").Count.ShouldBe(7);
        page.Find(".progress-insight").TextContent.ShouldContain("Certaines zones sont absentes");
    }

    [Fact]
    public async Task BarScale_AlignsVisibleWidthAndAccessibleMaximum()
    {
        await Seed(Log("a", "pa", -3, 30, "bench", "One Air", "40", "8"),
            Log("b", "pb", -2, 30, "bench", "One Air", "45", "8"),
            Log("c", "pull", -1, 30, "pulldown", "One Air", "50", "8"));
        var page = _context.Render<Progress>();
        var program = page.Find(".program-bars [aria-label='Push']");
        program.GetAttribute("aria-valuenow").ShouldBe("2");
        program.GetAttribute("aria-valuemax").ShouldBe("2");
        program.GetAttribute("aria-valuetext").ShouldBe("2 séance(s) sur 3");
        program.QuerySelector("span")!.GetAttribute("style").ShouldBe("width: 100%");
        page.Find(".area-bars [aria-label='Pectoraux']").GetAttribute("aria-valuemax").ShouldBe("2");
        page.Find(".area-bars [aria-label='Dos'] span").GetAttribute("style").ShouldBe("width: 50%");
    }

    [Fact]
    public async Task Curve_UsesFullChronologicalHistoryAndKeepsGymsSeparate()
    {
        await Seed(Log("last", "pa", -1, 30, "bench", "One Air", "55", "8"),
            Log("first", "pa", -60, 30, "bench", "One Air", "40", "8"),
            Log("other", "pa", -1, 30, "bench", "Basic-Fit", "99", "8"),
            Log("middle", "pa", -30, 30, "bench", "One Air", "42,5", "8"));
        var page = _context.Render<Progress>();

        page.Find(".progress-chart .chart-latest").TextContent.ShouldContain("55");
        page.Find(".chart-start").TextContent.ShouldContain("40 kg");
        page.Find(".chart-change").TextContent.ShouldContain("+15 kg");
        page.Find(".chart-record").TextContent.ShouldContain("55 kg");
        page.FindAll(".progress-chart svg .chart-point").Count.ShouldBe(3);
        page.Find(".chart-values tbody").TextContent.ShouldContain("42,5");
        page.Find(".chart-values tbody").TextContent.ShouldNotContain("99");
        page.Find(".chart-values tbody tr:first-child").TextContent.ShouldContain("03/08/2026");
        page.FindAll(".chart-target")[0].Click();
        page.Find(".chart-point-detail").TextContent.ShouldContain("03/08/2026 : 40 kg");
        page.FindAll(".chart-target")[1].KeyDown("Enter");
        page.Find(".chart-point-detail").TextContent.ShouldContain("42,5 kg");
    }

    [Fact]
    public async Task GymAndExerciseSelection_RebuildsCurveAndHandlesMissingHistory()
    {
        await Seed(Log("a", "pa", -2, 30, "bench", "One Air", "40", "8"),
            Log("b", "pb", -1, 30, "bench", "Basic-Fit", "70", "8"),
            Log("c", "pb", -2, 30, "bench", "Basic-Fit", "60", "8"));
        var page = _context.Render<Progress>();
        page.FindAll(".progress-chart svg").Count.ShouldBe(0);
        page.Find(".exercise-progress-gym select").Change("Basic-Fit");
        page.Find(".chart-change").TextContent.ShouldContain("+10 kg");
        page.Find(".progress-chart svg").ShouldNotBeNull();
        page.Find(".progress-chart svg title").TextContent.ShouldContain("Basic-Fit");
        (page.Find(".progress-chart").GetAttribute("aria-label") ?? "").ShouldContain("Basic-Fit");
        page.Find(".chart-values caption").TextContent.ShouldContain("Basic-Fit");
        page.Markup.ShouldNotContain("_exerciseGym");
        page.Find(".progress-exercise select").Change("curl");
        page.Find(".chart-empty").TextContent.ShouldContain("Aucune séance enregistrée");
        page.FindAll(".progress-chart svg").Count.ShouldBe(0);
    }

    [Fact]
    public void EmptyHistory_ShowsGuidanceAndNoInventedCurve()
    {
        var page = _context.Render<Progress>();
        page.Find(".progress-insight").TextContent.ShouldContain("Pas encore assez de données");
        page.Find(".program-bars").TextContent.ShouldContain("Aucune séance sur cette période");
        page.FindAll(".progress-chart svg").Count.ShouldBe(0);
        page.Markup.ShouldNotContain("RIR");
        page.Markup.ShouldNotContain("Volume total");
        page.Markup.ShouldNotContain("Volume cumulé");
        page.Markup.ShouldNotContain("Minutes de séance");
    }

    [Fact]
    public async Task SavedHistoryChange_RefreshesWithoutReopeningPage()
    {
        var page = _context.Render<Progress>();
        await Seed(Log("a", "pa", -2, 30, "bench", "One Air", "40", "8"),
            Log("b", "pa", -1, 30, "bench", "One Air", "50", "8"));
        page.WaitForAssertion(() => page.FindAll(".progress-summary strong")[0].TextContent.ShouldBe("2"));
        page.Find(".chart-change").TextContent.ShouldContain("+10 kg");
    }

    [Theory]
    [InlineData("40")]
    [InlineData("0")]
    public async Task FlatCurve_HasFiniteCoordinatesAndAccessibleValues(string weight)
    {
        await Seed(Log("a", "free", -2, 30, "bench", "One Air", weight, "8"),
            Log("b", "free", -1, 30, "bench", "One Air", weight, "10"));
        var page = _context.Render<Progress>();
        var points = page.Find(".progress-chart polyline").GetAttribute("points")!;
        points.ShouldNotContain("NaN");
        points.ShouldNotContain("Infinity");
        page.Find(".progress-chart svg").GetAttribute("role").ShouldBe("img");
        page.FindAll(".chart-values tbody tr").Count.ShouldBe(2);
        page.Find(".chart-change").TextContent.ShouldContain("+0 kg");
    }

    private Task Seed(params TrainingSessionLog[] logs) => _state.ReplaceDataAsync(new PerformanceData { History = logs });
    private static TrainingSessionLog Log(string id, string session, int days, int minutes, string exercise, string gym, string weight, string reps) => new()
    {
        Id = id, SessionId = session, Date = Now.AddDays(days), DurationMinutes = minutes,
        Exercises = [new ExercisePerformance { ExerciseId = exercise, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = reps }] }]
    };
    public void Dispose() => _context.Dispose();
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}
