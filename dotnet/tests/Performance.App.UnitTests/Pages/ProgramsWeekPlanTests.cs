using JetBrains.Annotations;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(Programs))]
public sealed class ProgramsWeekPlanTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly DefaultTrainingCatalog _catalog = new();
    private readonly Repository _repository = new();
    private readonly WorkoutStateService _state;

    public ProgramsWeekPlanTests()
    {
        _state = new(_repository, _catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/programmes");
    }

    [Fact]
    public async Task Home_ImportedWeek_DisplaysOrderedDuplicatesAndRequiresStartConfirmation()
    {
        // Arrange
        var push = Session("Push · A");
        var pull = Session("Pull · A");
        await SeedWeekAsync(push, push, pull);

        // Act
        var page = _context.Render<Programs>();

        // Assert
        page.Find(".week-plan").TextContent.ShouldContain("PROGRAMME ACTIF");
        page.FindAll(".weekly-session h3").Select(item => item.TextContent).ShouldBe(new[] { push.Name, push.Name, pull.Name });
        page.FindAll(".weekly-session .session-index").Select(item => item.TextContent).ShouldBe(new[] { "01", "02", "03" });
        page.FindAll(".week-move-up")[0].HasAttribute("disabled").ShouldBeTrue();
        page.FindAll(".week-move-down")[2].HasAttribute("disabled").ShouldBeTrue();
        page.FindAll(".week-remove").ShouldAllBe(button => button.TextContent == "×");
        page.FindAll(".weekly-session .weekly-start")[2].Click();
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find(".start-preview h2").TextContent.ShouldBe(pull.Name);
        page.Find(".start-preview .primary-button").Click();
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find("[role=dialog] .primary-button").Click();
        _state.Data.ActiveWorkout!.Session!.Id.ShouldBe(pull.Id);
        _state.Data.Programs.Single(program => program.Id == "react-week-plan").Sessions.Count.ShouldBe(3);
    }

    [Fact]
    public void Home_AddWeekSession_KeepsDuplicatesAndAllowsReorderAndSingleRemoval()
    {
        // Arrange
        var page = _context.Render<Programs>();
        var add = page.FindAll(".add-week").First();

        // Act
        add.Click();
        page.FindAll(".add-week").First().Click();
        page.FindAll(".add-week")[1].Click();

        // Assert
        page.FindAll(".weekly-session").Count.ShouldBe(3);
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.FindAll(".start-preview").ShouldBeEmpty();
        var initial = page.FindAll(".weekly-session h3").Select(item => item.TextContent).ToArray();
        initial[0].ShouldBe(initial[1]);
        initial[0].ShouldNotBe(initial[2]);

        // Act
        page.FindAll(".week-move-up")[2].Click();
        page.FindAll(".week-remove")[0].Click();

        // Assert
        page.FindAll(".weekly-session h3").Select(item => item.TextContent).ShouldBe(new[] { initial[2], initial[1] });
        page.Dispose();
        page = _context.Render<Programs>();
        page.FindAll(".weekly-session").Count.ShouldBe(2);
    }

    [Fact]
    public async Task Home_FailedWeekRemoval_ShowsErrorAndRetainsPlan()
    {
        // Arrange
        await SeedWeekAsync(Session("Push · A"));
        var page = _context.Render<Programs>();
        _repository.FailWrites = true;

        // Act
        page.Find(".week-remove").Click();

        // Assert
        page.FindAll(".weekly-session").Count.ShouldBe(1);
        page.Find(".notice").TextContent.ShouldContain("L’enregistrement a échoué");
    }

    [Fact]
    public void Home_Library_FiltersFamiliesAndResetsCategoryAndVariant()
    {
        var page = _context.Render<Programs>();
        page.FindAll(".category-filter").Select(button => button.TextContent).ShouldBe(new[] { "Push", "Pull", "Jambes", "Upper", "Bras + épaules", "Full Body" });
        page.FindAll(".variant-card").Count.ShouldBe(4);
        page.Find(".variant-detail h2").TextContent.ShouldBe("Push · A");

        page.Find("[data-family=targeted]").Click();

        page.FindAll(".category-filter").Count.ShouldBe(5);
        page.Find(".variant-detail h2").TextContent.ShouldBe("Pectoraux + biceps · A · Équilibré");
        page.Find("[data-category=hams-glutes]").Click();
        page.Find("[data-session=hgd] .session-option").Click();
        page.Find(".variant-detail h2").TextContent.ShouldBe("Ischios + fessiers · D · Machines");
        page.Find("[data-family=classic]").Click();
        page.Find(".variant-detail h2").TextContent.ShouldBe("Push · A");
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void Home_VariantSelection_DisplaysItsOwnExercisesWithoutStarting()
    {
        var page = _context.Render<Programs>();
        var expected = Session("Push · C");

        page.Find("[data-session=pc] .session-option").Click();

        page.Find(".variant-detail h2").TextContent.ShouldBe(expected.Name);
        page.FindAll(".program-exercise").Select(row => row.GetAttribute("data-exercise")).ShouldBe(expected.Exercises.OrderBy(plan => plan.Order).Select(plan => plan.ExerciseId));
        page.Find(".variant-detail").TextContent.ShouldContain("24 séries");
        page.Find("[data-session=pc] .session-option").GetAttribute("aria-pressed").ShouldBe("true");
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void Home_VariantPlus_AddsCorrectSessionWithoutChangingSelection()
    {
        var page = _context.Render<Programs>();
        page.Find("[data-session=pb] .session-option").Click();

        page.Find("[data-session=pc] .add-week").Click();

        _state.WeekSessions.Single().Id.ShouldBe("pc");
        page.Find(".variant-detail h2").TextContent.ShouldBe("Push · B");
        page.FindAll(".weekly-session").Count.ShouldBe(1);
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void Home_NextVariant_WrapsToFirstVariant()
    {
        var page = _context.Render<Programs>();
        page.Find("[data-session=pd] .session-option").Click();
        page.Find(".next-variant").Click();
        page.Find(".variant-detail h2").TextContent.ShouldBe("Push · A");
    }

    [Fact]
    public async Task Home_CustomProgram_RemainsAvailableWithoutAssumingFourVariants()
    {
        var custom = new WorkoutSession { Id = "custom-session", Name = "Ma séance" };
        await _state.ReplaceDataAsync(new PerformanceData { Programs = [new WorkoutProgram { Id = "custom", Name = "Mon programme", Sessions = [custom] }] });
        var page = _context.Render<Programs>();

        page.Find("[data-family=custom]").Click();

        page.FindAll(".variant-card").Count.ShouldBe(1);
        page.Find(".variant-detail h2").TextContent.ShouldBe(custom.Name);
        page.Find(".next-variant").HasAttribute("disabled").ShouldBeTrue();
        page.Find(".add-week").Click();
        _state.WeekSessions.Single().Id.ShouldBe(custom.Id);
    }

    [Fact]
    public void Home_FailedVariantAddition_RetainsWeekAndDisplaysError()
    {
        var page = _context.Render<Programs>();
        _repository.FailWrites = true;
        page.Find(".add-week").Click();
        _state.WeekSessions.ShouldBeEmpty();
        page.Find(".notice").TextContent.ShouldContain("L’enregistrement a échoué");
    }

    private WorkoutSession Session(string name) => _catalog.Programs.SelectMany(program => program.Sessions).First(session => session.Name == name);
    private Task<WorkoutResult> SeedWeekAsync(params WorkoutSession[] sessions) => _state.ReplaceDataAsync(new PerformanceData
    {
        Programs = [new WorkoutProgram { Id = "react-week-plan", Name = "Programme hebdomadaire", Sessions = sessions }]
    });

    public void Dispose() => _context.Dispose();

    private sealed class Feedback : IWorkoutFeedback
    {
        public void NotifyRestCompleted() { }
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public bool FailWrites { get; set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test write failure");
            _data = data;
            return ValueTask.CompletedTask;
        }
    }
}
