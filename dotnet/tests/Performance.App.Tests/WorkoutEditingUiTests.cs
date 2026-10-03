using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Layout;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.Tests;

public sealed class WorkoutEditingUiTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly DefaultTrainingCatalog _catalog = new();
    private readonly WorkoutStateService _state;
    private readonly AppNavigationService _navigation = new();
    private NavigationManager Navigation => _context.Services.GetRequiredService<NavigationManager>();
    private Exercise First => _catalog.Exercises.First();
    private Exercise Other => _catalog.Exercises.First(exercise => exercise.Name != First.Name);

    public WorkoutEditingUiTests()
    {
        _state = new(_repository, _catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton(_navigation);
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
        Navigation.NavigateTo("/seance");
    }

    [Fact]
    public async Task Replace_FromSearch_PreservesPositionAndLoadsLastWeightWithEmptyReps()
    {
        await _state.ReplaceDataAsync(new PerformanceData { History = [new TrainingSessionLog
        {
            Id = "previous", SessionId = "free", Date = DateTimeOffset.UtcNow.AddDays(-1),
            Exercises = [new ExercisePerformance { ExerciseId = Other.Id, Gym = "One Air", Sets = [new TrainingSet { Weight = "52,5", Reps = "10" }] }]
        }] });
        await StartAsync();
        var page = _context.Render<Workout>();

        page.Find(".replace-exercise").Click();
        page.Find(".search-input").Input(Other.Name);
        page.Find(".picker-item").Click();

        _state.Data.ActiveWorkout!.Session!.Exercises.Single().ExerciseId.ShouldBe(Other.Id);
        _state.Data.ActiveWorkout.Session.Exercises.Single().Order.ShouldBe(0);
        page.Find("input[aria-label^='Charge série 1']").GetAttribute("value").ShouldBe("52,5");
        page.Find("input[aria-label^='Répétitions série 1']").GetAttribute("value").ShouldBe("");
        page.FindAll(".exercise-picker").ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_Cancel_DoesNotChangeDefinitionOrDraft()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        var saves = _repository.Saves;
        page.Find(".edit-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input("Nom annulé");
        page.FindAll(".editor-dialog button").Single(button => button.TextContent == "Annuler").Click();

        _repository.Saves.ShouldBe(saves);
        page.Find(".exercise-card h2").TextContent.ShouldBe(First.Name);
        page.FindAll(".editor-dialog").ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_ChangesSeriesAndDefinition_PreservesEnteredValues()
    {
        await StartAsync();
        await _state.UpdateSetAsync(First.Id, 0, new TrainingSet { Weight = "40", Reps = "8", Rir = "2" });
        var page = _context.Render<Workout>();
        page.Find(".edit-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input("Mon exercice personnalisé");
        page.Find("input[aria-label='Nombre de séries']").Change("5");
        page.Find("input[aria-label='Cible de répétitions']").Change("10–15");
        page.Find("input[aria-label='Repos en secondes']").Change("150");
        page.Find(".editor-dialog form").Submit();

        page.WaitForAssertion(() => page.FindAll(".set-row").Count.ShouldBe(5));
        _state.AllExercises.First(exercise => exercise.Id == First.Id).Name.ShouldBe("Mon exercice personnalisé");
        _state.Data.ActiveWorkout!.Session!.Exercises.Single().RepTarget.ShouldBe("10–15");
        _state.GetSets(First.Id)[0].Weight.ShouldBe("40");
        _state.GetSets(First.Id)[0].Rir.ShouldBe("2");
        _state.GetRestSeconds(First.Id).ShouldBe(150);
    }

    [Fact]
    public async Task Edit_FailedSave_KeepsDialogAndPreviousDefinition()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find(".edit-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input("Nom non enregistré");
        _repository.FailWrites = true;
        page.Find(".editor-dialog form").Submit();

        page.Find(".editor-dialog [role=alert]").TextContent.ShouldContain("L’enregistrement a échoué");
        _state.AllExercises.First(exercise => exercise.Id == First.Id).Name.ShouldBe(First.Name);
        page.Find(".exercise-card h2").TextContent.ShouldBe(First.Name);
    }

    [Fact]
    public async Task Remove_RequiresConfirmationAndClearsRestWithoutEndingWorkout()
    {
        await StartAsync();
        await _state.StartRestAsync(First.Id);
        var page = _context.Render<Workout>();
        page.Find(".remove-exercise").Click();
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(1);

        page.Find("[role=dialog] .primary-button").Click();

        _state.Data.ActiveWorkout.ShouldNotBeNull();
        _state.Data.ActiveWorkout.Session!.Exercises.ShouldBeEmpty();
        _state.Data.ActiveWorkout.RestEnd.ShouldBeNull();
        page.FindAll(".exercise-card, .rest-banner").ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_FailedSave_KeepsExerciseAndConfirmation()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find(".remove-exercise").Click();
        _repository.FailWrites = true;
        page.Find("[role=dialog] .primary-button").Click();

        page.Find("[role=dialog] [role=alert]").TextContent.ShouldContain("L’enregistrement a échoué");
        page.FindAll(".exercise-card").Count.ShouldBe(1);
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Search_Create_AddsOnlyAfterSavingAndCannotDuplicateExistingExercise()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find(".add-workout-exercise").Click();
        page.FindAll(".picker-item").ShouldNotContain(button => button.TextContent.Contains(First.Name));
        page.Find(".create-workout-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input(First.Name);
        page.Find("input[aria-label='Muscle principal']").Change("Pectoraux");
        page.Find(".editor-dialog form").Submit();
        page.Find(".editor-dialog [role=alert]").TextContent.ShouldContain("existe déjà");
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(1);
        page.Find("input[aria-label='Nom de l’exercice']").Input("Ma nouvelle machine");
        page.Find(".editor-dialog form").Submit();

        page.WaitForAssertion(() => page.FindAll(".exercise-card").Count.ShouldBe(2));
        _state.Data.ActiveWorkout.Session.Exercises.Last().ExerciseId.ShouldStartWith("custom-");
        page.FindAll(".editor-dialog, .exercise-picker").ShouldBeEmpty();
    }

    [Fact]
    public async Task Collapse_IsManualAndDoesNotDiscardSets()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        await page.Find("input[aria-label^='Charge série 1']").ChangeAsync(new ChangeEventArgs { Value = "0" });
        await page.Find("input[aria-label^='Répétitions série 1']").InputAsync("8");
        page.WaitForAssertion(() =>
        {
            page.FindAll(".set-row").Count.ShouldBe(4);
            page.Find(".set-completed").TextContent.ShouldBe("✓");
            page.Find(".collapse-exercise").HasAttribute("disabled").ShouldBeFalse();
        });
        await page.Find(".collapse-exercise").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        page.WaitForAssertion(() =>
        {
            page.FindAll(".set-row").ShouldBeEmpty();
            page.Find(".exercise-completion").TextContent.ShouldContain("1 / 4");
            page.Find(".workout-completion").TextContent.ShouldContain("1 / 4");
        });
        await page.Find(".collapse-exercise").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        page.WaitForAssertion(() =>
        {
            page.FindAll(".set-row").Count.ShouldBe(4);
            page.Find("input[aria-label^='Répétitions série 1']").GetAttribute("value").ShouldBe("8");
            page.Find("input[aria-label^='Charge série 1']").GetAttribute("value").ShouldBe("0");
        });
    }

    [Theory]
    [InlineData("", "○", "0 / 4")]
    [InlineData("0", "✓", "1 / 4")]
    [InlineData("42,5", "✓", "1 / 4")]
    public async Task Completion_RequiresWeightAndRepsButAcceptsZeroWeight(string weight, string indicator, string summary)
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find("input[aria-label^='Charge série 1']").Change(weight);
        page.Find("input[aria-label^='Répétitions série 1']").Input("8");

        page.Find(".set-completed").TextContent.ShouldBe(indicator);
        page.Find(".workout-completion").TextContent.ShouldContain(summary);
        page.Find(".exercise-completion").TextContent.ShouldContain(summary);
        _state.Data.ActiveWorkout!.RestEnd.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_PredefinedTagsToggleAndMuscleIsRequired()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find(".add-workout-exercise").Click();
        page.Find(".create-workout-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input("Mon mouvement inédit");
        page.Find(".editor-dialog form").Submit();
        page.Find(".editor-dialog [role=alert]").TextContent.ShouldContain("muscle principal");
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(1);
        page.Find("input[aria-label='Muscle principal']").Change("Dos");
        page.FindAll(".tag-category button").Single(button => button.TextContent == "Machine").Click();
        page.FindAll(".tag-category button").Single(button => button.TextContent == "Machine").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("input[aria-label='Tags séparés par des virgules']").GetAttribute("value").ShouldBe("Machine");
        page.FindAll(".tag-category button").Single(button => button.TextContent == "Machine").Click();
        page.FindAll(".tag-category button").Single(button => button.TextContent == "Machine").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll(".tag-category button").Single(button => button.TextContent == "Poulie").Click();
        page.Find("input[aria-label='Tags séparés par des virgules']").Change("Poulie, prise neutre");
        page.Find(".editor-dialog form").Submit();

        var created = _state.Data.Exercises.Single();
        created.Tags.ShouldBe(new[] { "Poulie", "prise neutre" });
        created.MuscleGroup.ShouldBe("Dos");
    }

    [Fact]
    public async Task Create_SimilarExerciseSuggestion_UsesExistingWithoutChangingCatalog()
    {
        await StartAsync();
        var page = _context.Render<Workout>();
        page.Find(".add-workout-exercise").Click();
        page.Find(".create-workout-exercise").Click();
        page.Find("input[aria-label='Nom de l’exercice']").Input(Other.Name.ToUpperInvariant());
        var saves = _repository.Saves;

        page.FindAll(".use-existing-exercise").First(button => button.TextContent.Contains(Other.Name)).Click();

        _state.Data.Exercises.ShouldBeEmpty();
        _state.Data.ActiveWorkout!.Session!.Exercises.Last().ExerciseId.ShouldBe(Other.Id);
        _repository.Saves.ShouldBe(saves + 1);
        page.FindAll(".editor-dialog, .exercise-picker").ShouldBeEmpty();
    }

    [Fact]
    public async Task AndroidBack_ClosesNestedEditorThenPickerThenReturnsHomeWithDraft()
    {
        await StartAsync();
        var page = _context.Render<RouteHarness>();
        var saved = _state.Data.ActiveWorkout;
        page.Find(".add-workout-exercise").Click();
        page.Find(".create-workout-exercise").Click();

        await _navigation.RequestBackAsync();
        page.FindAll(".editor-dialog").ShouldBeEmpty();
        page.Find(".exercise-picker").ShouldNotBeNull();
        await _navigation.RequestBackAsync();
        page.FindAll(".exercise-picker").ShouldBeEmpty();
        await _navigation.RequestBackAsync();
        page.Find(".dashboard").ShouldNotBeNull();
        _state.Data.ActiveWorkout.ShouldBeSameAs(saved);
    }

    [Fact]
    public async Task PlusMenu_ProvidesSecondaryScreensAndBackClosesIt()
    {
        var page = _context.Render<MainLayout>();
        page.Find("button[aria-label=Plus]").Click();
        page.FindAll(".plus-menu a").Select(link => link.GetAttribute("href")).ShouldBe(new[] { "/exercices", "/reglages" });
        await _navigation.RequestBackAsync();
        page.FindAll(".plus-menu").ShouldBeEmpty();
    }

    [Fact]
    public void FreeSession_OneConfirmationChoosesGymAndOpensSearch()
    {
        Navigation.NavigateTo("/");
        var page = _context.Render<RouteHarness>();
        page.Find(".free-workout-card").Click();
        page.FindAll("[role=dialog]").Count.ShouldBe(1);
        page.FindAll(".start-preview").ShouldBeEmpty();
        page.Find("[role=dialog] select").Change("Basic-Fit");
        page.Find("[role=dialog] .primary-button").Click();

        _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit");
        page.Find(".exercise-picker").ShouldNotBeNull();
        page.Find(".workout-elapsed").ShouldNotBeNull();
    }

    private Task<WorkoutResult> StartAsync() => _state.StartAsync(new WorkoutSession
    {
        Id = "free", Name = "Séance libre", Exercises = [new WorkoutExercise { ExerciseId = First.Id, Sets = 4, Order = 0 }]
    }, "One Air");
    public void Dispose() => _context.Dispose();
    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public bool FailWrites { get; set; }
        public int Saves { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test write failure");
            _data = data; Saves++;
            return ValueTask.CompletedTask;
        }
    }
}
