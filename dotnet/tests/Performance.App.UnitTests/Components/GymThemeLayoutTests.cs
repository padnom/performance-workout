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
    public void Logo_OnlyForKnownGyms()
    {
        GymTheme.Logo("One Air").ShouldBe("img/gyms/one-air.png");
        GymTheme.Logo("Toutes").ShouldBeNull();
    }

    [Fact]
    public void GymName_ShowsOnAirButKeepsStoredValue()
    {
        WorkoutUi.GymName("One Air").ShouldBe("On Air");
        WorkoutUi.GymName("Basic-Fit").ShouldBe("Basic-Fit");
        WorkoutUi.Gyms.ShouldContain("One Air");
    }

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
            page.Find(".offline-pill .gym-logo").GetAttribute("src").ShouldBe("img/gyms/basic-fit.svg");
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

    [Fact]
    public async Task HeaderSwitch_ChangesSettingsGymAndTheme()
    {
        await _state.SetGymAsync("One Air");
        var page = _context.Render<MainLayout>();

        page.Find(".gym-switch").Click();
        page.FindAll(".gym-menu [role=menuitemradio]").Select(item => item.GetAttribute("data-gym")).ShouldBe(["One Air", "Basic-Fit", "Toutes"]);
        page.Find(".gym-menu [aria-checked=true]").GetAttribute("data-gym").ShouldBe("One Air");
        page.Find(".gym-menu [data-gym='Basic-Fit']").Click();

        page.WaitForAssertion(() =>
        {
            page.FindAll(".gym-menu").ShouldBeEmpty();
            page.Find(".app-shell").GetAttribute("data-gym").ShouldBe("basic-fit");
        });
        _state.CurrentGym.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task HeaderSwitch_DuringWorkout_ChangesWorkoutGymOnly()
    {
        await _state.SetGymAsync("One Air");
        await _state.StartAsync(new Performance.Domain.WorkoutSession { Id = "free", Name = "Séance libre" }, "One Air");
        var page = _context.Render<MainLayout>();

        page.Find(".gym-switch").Click();
        page.Find(".gym-menu .eyebrow").TextContent.ShouldContain("SÉANCE EN COURS");
        page.Find(".gym-menu [data-gym='Toutes']").Click();

        page.WaitForAssertion(() => page.Find(".app-shell").GetAttribute("data-gym").ShouldBe("all"));
        _state.Data.ActiveWorkout.ShouldNotBeNull().DefaultGym.ShouldBe("Toutes");
        _state.CurrentGym.ShouldBe("One Air");
    }

    [Fact]
    public void HeaderSwitch_BackdropCloses()
    {
        var page = _context.Render<MainLayout>();

        page.Find(".gym-switch").Click();
        page.Find(".plus-menu-backdrop").Click();

        page.FindAll(".gym-menu").ShouldBeEmpty();
    }

    public void Dispose() => _context.Dispose();

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}
