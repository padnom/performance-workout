using Bunit;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Components;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(Programs))]
public sealed class ProgramAuthoringUiTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly AppNavigationService _navigation = new();
    private readonly Files _files = new();
    private readonly WorkoutStateService _state;

    public ProgramAuthoringUiTests()
    {
        _state = new(_repository, new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton(_navigation);
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
        _context.Services.AddSingleton<IProgramFiles>(_files);
        _context.Services.AddSingleton<IProgramPackageCodec>(new JsonProgramPackageCodec());
        _context.Services.AddTransient<ImportWorkoutProgramsUseCase>();
        _context.Services.AddTransient<ExportWorkoutProgramsUseCase>();
    }

    [Fact]
    public void CreateProgram_MultipleSessionsAndPrescriptions_SaveOneAtomicFavoriteProgram()
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-name").Change("Ma progression");
        page.Find(".program-description").Change("Deux séances personnelles");
        var firstExerciseId = AddFirstExercise(page, 0);
        var secondExerciseId = AddFirstExercise(page, 0);
        page.FindAll(".draft-exercise")[0].QuerySelector(".draft-sets")!.Change("3");
        page.FindAll(".draft-exercise")[0].QuerySelector(".draft-repetitions")!.Change("8–12");
        page.FindAll(".draft-exercise")[0].QuerySelector(".draft-rest")!.Change("2:00");
        page.FindAll(".move-exercise-down")[0].Click();
        page.Find(".draft-session-name").Change("Haut du corps");
        page.Find(".add-draft-session").Click();
        page.FindAll(".draft-session-name")[1].Change("Jambes");
        var legExerciseId = AddFirstExercise(page, 1);
        page.FindAll(".move-session-up")[1].Click();
        page.Find(".draft-favorite").Change(true);

        // Act
        page.Find(".save-program").Click();

        // Assert
        page.WaitForAssertion(() => _state.Data.Programs.Count.ShouldBe(1));
        _repository.Saves.ShouldBe(1);
        var saved = _state.Data.Programs.Single();
        saved.Name.ShouldBe("Ma progression");
        saved.Description.ShouldBe("Deux séances personnelles");
        saved.Sessions.Select(session => session.Name).ShouldBe(["Jambes", "Haut du corps"]);
        saved.Sessions[0].Exercises.Single().ExerciseId.ShouldBe(legExerciseId);
        saved.Sessions[1].Exercises.Select(plan => plan.ExerciseId).ShouldBe([secondExerciseId, firstExerciseId]);
        saved.Sessions[1].Exercises[1].Sets.ShouldBe(3);
        saved.Sessions[1].Exercises[1].RepTarget.ShouldBe("8–12");
        saved.Sessions[1].Exercises[1].Rest.ShouldBe("2:00");
        _state.IsProgramFavorite(saved.Id).ShouldBeTrue();
        page.FindAll(".program-editor").ShouldBeEmpty();
        page.Find(".my-programs").TextContent.ShouldContain("Ma progression");
        page.Find(".favorite-programs").TextContent.ShouldContain("Ma progression");
        _state.WeekSessions.ShouldBeEmpty();
        _state.Data.ActiveWorkout.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateProgram_CancelOrAndroidBack_DiscardsDraftWithoutSaving(bool back)
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-name").Change("Brouillon");
        AddFirstExercise(page, 0);
        page.Find(".add-draft-session").Click();

        // Act
        if (back) await page.InvokeAsync(() => _navigation.RequestBackAsync());
        if (!back) page.Find(".cancel-program").Click();

        // Assert
        page.FindAll(".program-editor").ShouldBeEmpty();
        _repository.Saves.ShouldBe(0);
        _state.Data.Programs.ShouldBeEmpty();
        page.Find(".create-program").Click();
        page.Find(".program-name").GetAttribute("value").ShouldBe("");
        page.FindAll(".draft-session").Count.ShouldBe(1);
        page.FindAll(".draft-exercise").ShouldBeEmpty();
        page.Find(".draft-favorite").HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public void CreateProgram_InvalidNameEmptySessionAndRest_KeepDraftAndExplainCorrection()
    {
        // Arrange
        var page = OpenEditor();

        // Act
        page.Find(".save-program").Click();

        // Assert
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("nom de programme");
        page.Find(".program-name").Change("Valide");
        page.Find(".save-program").Click();
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("exercices dans chaque séance");
        AddFirstExercise(page, 0);
        page.Find(".draft-rest").Change("1:99");
        page.Find(".save-program").Click();
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("M:ss");
        page.Find(".draft-rest").Change(new string('0', 101) + ":15");
        page.Find(".save-program").Click();
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("100 caractères maximum");
        page.Find(".program-name").GetAttribute("value").ShouldBe("Valide");
        _repository.Saves.ShouldBe(0);
        _state.Data.Programs.ShouldBeEmpty();
    }

    [Fact]
    public void CreateProgram_FailedSave_RetainsDraftAndCanRetryOnce()
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-name").Change("À conserver");
        AddFirstExercise(page, 0);
        page.Find(".draft-favorite").Change(true);
        _repository.Fail = true;

        // Act
        page.Find(".save-program").Click();

        // Assert
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("L’enregistrement a échoué");
        page.Find(".program-name").GetAttribute("value").ShouldBe("À conserver");
        page.FindAll(".draft-exercise").Count.ShouldBe(1);
        _state.Data.Programs.ShouldBeEmpty();
        _state.FavoritePrograms.ShouldBeEmpty();
        _repository.Fail = false;
        page.Find(".save-program").Click();
        page.WaitForAssertion(() => _state.Data.Programs.Count.ShouldBe(1));
        _repository.Saves.ShouldBe(1);
        _state.FavoritePrograms.Single().Name.ShouldBe("À conserver");
    }

    [Fact]
    public async Task CreateProgram_PendingSave_DisablesControlsAndPreventsDuplicateSubmissionOrBack()
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-name").Change("Une seule copie");
        AddFirstExercise(page, 0);
        _repository.PendingSave = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var save = page.Find(".save-program").ClickAsync(new());
        page.WaitForAssertion(() => page.Find(".save-program").HasAttribute("disabled").ShouldBeTrue());
        await page.Find(".save-program").ClickAsync(new());
        await page.InvokeAsync(() => _navigation.RequestBackAsync());

        // Assert
        page.Find(".program-editor").GetAttribute("aria-busy").ShouldBe("true");
        page.Find(".cancel-program").HasAttribute("disabled").ShouldBeTrue();
        page.Find(".program-editor-fields").HasAttribute("disabled").ShouldBeTrue();
        _state.Data.Programs.ShouldBeEmpty();
        _repository.PendingSave.SetResult();
        await save;
        page.WaitForAssertion(() => _state.Data.Programs.Count.ShouldBe(1));
        _repository.Saves.ShouldBe(1);
    }

    [Fact]
    public void CreateProgram_RemoveExercisesAndSession_UpdatesOnlyLocalDraft()
    {
        // Arrange
        var page = OpenEditor();
        var id = AddFirstExercise(page, 0);
        page.Find(".add-draft-session").Click();
        AddFirstExercise(page, 1);

        // Act
        page.FindAll(".remove-draft-exercise")[0].Click();
        page.FindAll(".remove-draft-session")[1].Click();

        // Assert
        page.FindAll(".draft-session").Count.ShouldBe(1);
        page.FindAll(".draft-exercise").ShouldBeEmpty();
        page.Find($"[data-add-exercise='{id}']").ShouldNotBeNull();
        page.Find(".remove-draft-session").HasAttribute("disabled").ShouldBeTrue();
        _state.Data.Programs.ShouldBeEmpty();
        _repository.Saves.ShouldBe(0);
    }

    [Fact]
    public void CreateProgram_GymFilterSearchAndDuplicatePrevention_PreserveChoicesAndBoundResults()
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-gym").Change("Toutes");
        var candidates = page.FindAll("[data-add-exercise]");

        // Assert
        candidates.Count.ShouldBeLessThanOrEqualTo(12);

        // Act
        var exercise = _state.Exercises.First(item => item.Variants.Any(variant => variant.Gym == "One Air")
            && !ProgramDraft.Compatible(item, "Basic-Fit"));
        page.Find(".draft-exercise-search").Input(exercise.Name);
        page.Find($"[data-add-exercise='{exercise.Id}']").Click();
        page.FindAll($"[data-add-exercise='{exercise.Id}']").ShouldBeEmpty();
        page.Find(".program-name").Change("One Air spécifique");
        page.Find(".program-gym").Change("Basic-Fit");
        page.Find(".save-program").Click();

        // Assert
        page.FindAll(".draft-exercise").Count.ShouldBe(1);
        page.Find(".program-editor [role=alert]").TextContent.ShouldContain("ne correspond pas");
        _repository.Saves.ShouldBe(0);
        page.Find(".program-gym").Change("Toutes");
        page.Find(".save-program").Click();
        page.WaitForAssertion(() => _state.Data.Programs.Single().Gym.ShouldBe("Toutes"));
        _state.FavoritePrograms.ShouldBeEmpty();
    }

    [Fact]
    public void Favorites_BuiltInToggle_UpdatesSectionAndKeepsLibrarySelection()
    {
        // Arrange
        var page = _context.Render<Programs>();
        page.Find("[data-category=pull]").Click();
        page.FindAll(".variant-card .session-option")[1].Click();
        var selected = page.Find(".variant-detail h2").TextContent;

        // Act
        page.Find(".library-favorite-heading [data-favorite-program=pull]").Click();

        // Assert
        page.WaitForAssertion(() => _state.IsProgramFavorite("pull").ShouldBeTrue());
        page.Find(".favorite-programs [data-personal-program=pull]").ShouldNotBeNull();
        page.Find(".variant-detail h2").TextContent.ShouldBe(selected);
        page.Find("[data-category=pull]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find(".library-favorite-heading .program-favorite").GetAttribute("aria-pressed").ShouldBe("true");

        // Act
        page.Find(".favorite-programs [data-favorite-program=pull]").Click();

        // Assert
        page.WaitForAssertion(() => _state.FavoritePrograms.ShouldBeEmpty());
        page.FindAll(".favorite-programs [data-personal-program=pull]").ShouldBeEmpty();
        page.Find(".variant-detail h2").TextContent.ShouldBe(selected);
    }

    [Fact]
    public void CreateProgram_NewFavoriteImmediatelyAppearsInSharing_ExportsWithoutPersonalFavorites()
    {
        // Arrange
        var page = OpenEditor();
        page.Find(".program-name").Change("Mon programme partageable");
        AddFirstExercise(page, 0);
        page.Find(".draft-favorite").Change(true);
        page.Find(".save-program").Click();
        page.WaitForAssertion(() => _state.Data.Programs.Count.ShouldBe(1));
        var program = _state.Data.Programs.Single();

        // Act
        page.Find($"[data-share-program='{program.Id}']").Change(true);
        page.Find(".export-programs").Click();

        // Assert
        page.WaitForAssertion(() => _files.Shared.ShouldNotBeNull());
        var package = new JsonProgramPackageCodec().Decode(_files.Shared!);
        package.Programs.Single().Name.ShouldBe("Mon programme partageable");
        package.Exercises.Count.ShouldBe(1);
        _files.Shared!.ShouldNotContain("favorite");
        _files.Shared!.ShouldNotContain("settings");
        _repository.Saves.ShouldBe(1);
    }

    [Fact]
    public async Task Favorites_CustomProgramRemoval_StaysInMyProgramsWithPreviewWeekAndStart()
    {
        // Arrange
        await _state.InitializeAsync();
        var exercise = _state.Exercises.First(item => ProgramDraft.Compatible(item, "One Air"));
        var draft = new ProgramDraft { Name = "Mon programme" };
        draft.Sessions[0].Exercises.Add(new() { ExerciseId = exercise.Id });
        (await _state.CreateProgramAsync(draft.ToProgram(), true)).IsSuccess.ShouldBeTrue();
        var program = _state.Data.Programs.Single();
        var page = _context.Render<Programs>();

        // Act
        page.Find($".favorite-programs [data-favorite-program='{program.Id}']").Click();

        // Assert
        page.FindAll(".favorite-programs [data-personal-program]").ShouldBeEmpty();
        page.Find($".my-programs [data-personal-program='{program.Id}']").ShouldNotBeNull();
        page.Find(".my-programs .add-week").Click();
        page.WaitForAssertion(() => _state.WeekSessions.Single().Id.ShouldBe(program.Sessions[0].Id));
        page.Find(".my-programs .personal-session-preview").Click();
        page.Find(".start-preview h2").TextContent.ShouldBe(program.Sessions[0].Name);
        page.Find(".start-preview").TextContent.ShouldContain(exercise.Name);
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find(".start-preview .primary-button").Click();
        page.Find("[role=dialog] .primary-button").Click();
        page.WaitForAssertion(() => _state.Data.ActiveWorkout!.Session!.Id.ShouldBe(program.Sessions[0].Id));
        _context.Services.GetRequiredService<NavigationManager>().Uri.ShouldEndWith("/seance");
    }

    [Fact]
    public void Favorites_FailedToggle_KeepsPreviousFavoriteStateAndReportsFailure()
    {
        // Arrange
        var page = _context.Render<Programs>();
        _repository.Fail = true;

        // Act
        page.Find(".gym-program [data-favorite-program=gym-one-air]").Click();

        // Assert
        page.Find(".gym-program .favorite-error").TextContent.ShouldContain("L’enregistrement a échoué");
        _state.IsProgramFavorite("gym-one-air").ShouldBeFalse();
        page.Find(".gym-program .program-favorite").GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll(".favorite-programs [data-personal-program]").ShouldBeEmpty();
    }

    private IRenderedComponent<Programs> OpenEditor()
    {
        var page = _context.Render<Programs>();
        page.Find(".create-program").Click();
        return page;
    }

    private static string AddFirstExercise(IRenderedComponent<Programs> page, int sessionIndex)
    {
        var button = page.FindAll(".draft-session")[sessionIndex].QuerySelector("[data-add-exercise]")!;
        var id = button.GetAttribute("data-add-exercise")!;
        button.Click();
        return id;
    }

    public void Dispose() => _context.Dispose();
    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }
    private sealed class Files : IProgramFiles
    {
        public string? Shared { get; private set; }
        public Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default) { Shared = json; return Task.CompletedTask; }
        public string? PickedSession { get; set; }
        public string? SharedSessionJson { get; private set; }
        public string? SharedSessionName { get; private set; }
        public string? SharedText { get; private set; }
        public string? SharedTextTitle { get; private set; }
        public Task<string?> PickSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(PickedSession);
        public Task ShareSessionAsync(string json, string name, CancellationToken cancellationToken = default) { SharedSessionJson = json; SharedSessionName = name; return Task.CompletedTask; }
        public Task ShareTextAsync(string text, string title, CancellationToken cancellationToken = default) { SharedText = text; SharedTextTitle = title; return Task.CompletedTask; }
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public int Saves { get; private set; }
        public bool Fail { get; set; }
        public TaskCompletionSource? PendingSave { get; set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("test failure");
            if (PendingSave is { } pending) await pending.Task;
            _data = data; Saves++;
        }
    }
}
