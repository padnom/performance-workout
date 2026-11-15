using Bunit;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Components;
using Performance.App.Layout;
using Performance.Application;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Components;

[TestSubject(typeof(GymTheme))]
public sealed class GymThemeLayoutTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly DefaultTrainingCatalog _catalog = new();
    private readonly WorkoutStateService _state;

    public GymThemeLayoutTests()
    {
        _state = new(new Repository(), _catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
    }

    [Theory]
    [InlineData("Basic-Fit", "basic-fit")]
    [InlineData("One Air", "one-air")]
    [InlineData("Toutes", "all")]
    [InlineData(null, "all")]
    public void For_MapsGymToTheme(string? gym, string theme) => GymTheme.For(gym).ShouldBe(theme);

    [Fact]
    public async Task Layout_FollowsSelectedGymLive()
    {
        var page = _context.Render<MainLayout>();
        page.Find(".app-shell").GetAttribute("data-gym").ShouldBe("one-air");

        await _state.SetGymAsync("Basic-Fit");

        page.WaitForAssertion(() =>
        {
            page.Find(".app-shell").GetAttribute("data-gym").ShouldBe("basic-fit");
            page.Find(".offline-pill").TextContent.ShouldContain("BASIC-FIT");
        });
    }

    [Fact]
    public async Task Layout_ActiveWorkoutGymWinsOverSettings()
    {
        await _state.SetGymAsync("One Air");
        await _state.StartAsync(_catalog.Programs.First().Sessions.First(), "Basic-Fit");

        var page = _context.Render<MainLayout>();

        page.Find(".app-shell").GetAttribute("data-gym").ShouldBe("basic-fit");
    }

    public void Dispose() => _context.Dispose();

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}
