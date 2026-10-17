using Bunit;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Components;
using Performance.App.Layout;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Domain;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Pages;

[TestSubject(typeof(ProgramSharing))]
public sealed class SessionImportUiTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly Files _files = new();
    private readonly IncomingShareService _incoming = new();
    private readonly JsonSessionPackageCodec _sessionCodec = new();
    private readonly JsonProgramPackageCodec _programCodec = new();
    private readonly WorkoutStateService _state;

    public SessionImportUiTests()
    {
        _state = new(_repository, new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton(new AppNavigationService());
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
        _context.Services.AddSingleton<IProgramFiles>(_files);
        _context.Services.AddSingleton<ISessionPackageCodec>(_sessionCodec);
        _context.Services.AddSingleton<IProgramPackageCodec>(_programCodec);
        _context.Services.AddSingleton(_incoming);
        _context.Services.AddTransient<ImportWorkoutSessionUseCase>();
        _context.Services.AddTransient<ExportWorkoutSessionUseCase>();
        _context.Services.AddTransient<ImportWorkoutProgramsUseCase>();
        _context.Services.AddTransient<ExportWorkoutProgramsUseCase>();
        _context.Services.AddTransient<ReadSharedFileUseCase>();
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void IncomingShare_ReceivedOutsideWorkout_NavigatesToPrograms()
    {
        // Arrange
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        _context.Render<MainLayout>();

        // Act
        _incoming.Offer(new IncomingShare(SessionJson("Séance reçue"), null));

        // Assert
        navigation.Uri.ShouldEndWith("/programmes");
        _incoming.HasPending.ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void IncomingShare_ReceivedDuringWorkout_StaysOnWorkoutAndKeepsSharePending()
    {
        // Arrange
        var navigation = _context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/seance");
        _context.Render<MainLayout>();

        // Act
        _incoming.Offer(new IncomingShare(SessionJson("Séance reçue"), null));

        // Assert
        navigation.Uri.ShouldEndWith("/seance");
        _incoming.HasPending.ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_ConfirmedPreview_AddsSessionToMySessions()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.PickedSession = SessionJson("Séance reçue");
        var page = _context.Render<Programs>();

        // Act
        page.Find(".import-session").Click();

        // Assert
        var preview = page.Find(".session-import-preview");
        preview.GetAttribute("role").ShouldBe("dialog");
        preview.TextContent.ShouldContain("Ajouter cette séance ?");
        preview.TextContent.ShouldContain("Séance reçue");
        preview.TextContent.ShouldContain("2 exercice(s)");
        preview.TextContent.ShouldContain("Ajoutée dans Mes séances.");
        _repository.Saves.ShouldBe(0);

        // Act
        page.Find(".confirm-session-import").Click();

        // Assert
        page.WaitForAssertion(() => _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId)
            .Sessions.Single().Name.ShouldBe("Séance reçue"));
        page.FindAll(".session-import-preview").ShouldBeEmpty();
        page.Find(".program-sharing .notice").TextContent.ShouldBe("La séance a été ajoutée à Mes séances.");
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_Cancelled_ChangesNothing()
    {
        // Arrange
        await _state.InitializeAsync();
        var savesBefore = _repository.Saves;
        _files.PickedSession = SessionJson("Séance reçue");
        var page = _context.Render<ProgramSharing>();
        page.Find(".import-session").Click();

        // Act
        page.Find(".cancel-session-import").Click();

        // Assert
        page.FindAll(".session-import-preview").ShouldBeEmpty();
        _state.Programs.ShouldNotContain(program => program.Id == WorkoutStateService.SessionsProgramId);
        _repository.Saves.ShouldBe(savesBefore);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_InvalidFile_ShowsMessageAndChangesNothing()
    {
        // Arrange
        await _state.InitializeAsync();
        var savesBefore = _repository.Saves;
        _files.PickedSession = "{ not json";
        var page = _context.Render<ProgramSharing>();

        // Act
        page.Find(".import-session").Click();

        // Assert
        page.FindAll(".session-import-preview").ShouldBeEmpty();
        page.Find(".program-sharing .notice").TextContent.ShouldNotBeEmpty();
        _repository.Saves.ShouldBe(savesBefore);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_ProgramFileChosen_ShowsMessageWithoutPreview()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.PickedSession = ProgramJson();
        var page = _context.Render<ProgramSharing>();

        // Act
        page.Find(".import-session").Click();

        // Assert
        page.FindAll(".session-import-preview").ShouldBeEmpty();
        page.Find(".program-sharing .notice").TextContent.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_FullMySessions_KeepsPreviewWithErrorMessage()
    {
        // Arrange
        await _state.InitializeAsync();
        var full = Enumerable.Range(0, 100).Select(index => new WorkoutSession { Id = $"s{index}", Name = $"S{index}", Exercises = Plans(1) }).ToArray();
        await _state.ReplaceDataAsync(_state.Data with { Programs = [new WorkoutProgram { Id = WorkoutStateService.SessionsProgramId, Name = "Mes séances", Sessions = full }] });
        _files.PickedSession = SessionJson("Une de trop");
        var page = _context.Render<ProgramSharing>();
        page.Find(".import-session").Click();

        // Act
        page.Find(".confirm-session-import").Click();

        // Assert
        page.Find(".session-import-preview [role=alert]").TextContent.ShouldBe(WorkoutUi.Error("SessionsProgramFull"));
        _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Count.ShouldBe(100);
    }

    [Fact]
    [Trait("AC", "AC-43")]
    public async Task ImportSession_BackWhileOpen_ClosesPreview()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.PickedSession = SessionJson("Séance reçue");
        var navigation = _context.Services.GetRequiredService<AppNavigationService>();
        var page = _context.Render<ProgramSharing>();
        page.Find(".import-session").Click();

        // Act
        await page.InvokeAsync(() => navigation.RequestBackAsync());

        // Assert
        page.FindAll(".session-import-preview").ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_PendingSession_OpensSessionPreviewOnRender()
    {
        // Arrange
        await _state.InitializeAsync();
        _incoming.Offer(new IncomingShare(SessionJson("Séance partagée"), null));
        var savesBefore = _repository.Saves;

        // Act
        var page = _context.Render<ProgramSharing>();

        // Assert
        page.Find(".session-import-preview").TextContent.ShouldContain("Séance partagée");
        _incoming.HasPending.ShouldBeFalse();
        _repository.Saves.ShouldBe(savesBefore);
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_PendingPrograms_OpensProgramPreview()
    {
        // Arrange
        await _state.InitializeAsync();
        _incoming.Offer(new IncomingShare(ProgramJson(), null));

        // Act
        var page = _context.Render<ProgramSharing>();

        // Assert
        page.Find(".program-import-preview").TextContent.ShouldContain("Ajouter ces programmes ?");
        page.FindAll(".session-import-preview").ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_PendingError_ShowsErrorMessage()
    {
        // Arrange
        await _state.InitializeAsync();
        _incoming.Offer(new IncomingShare(null, "Fichier trop volumineux."));

        // Act
        var page = _context.Render<ProgramSharing>();

        // Assert
        page.Find(".program-sharing .notice").TextContent.ShouldBe("Fichier trop volumineux.");
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_UnknownJson_ShowsFormatMessage()
    {
        // Arrange
        await _state.InitializeAsync();
        _incoming.Offer(new IncomingShare("{\"kind\":\"other\"}", null));

        // Act
        var page = _context.Render<ProgramSharing>();

        // Assert
        page.Find(".program-sharing .notice").TextContent.ShouldBe("Ce fichier n'est pas un partage Performance compatible.");
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_ReceivedAfterRender_OpensSessionPreview()
    {
        // Arrange
        await _state.InitializeAsync();
        var page = _context.Render<ProgramSharing>();

        // Act
        await page.InvokeAsync(() => _incoming.Offer(new IncomingShare(SessionJson("Séance tardive"), null)));

        // Assert
        page.WaitForAssertion(() => page.Find(".session-import-preview").TextContent.ShouldContain("Séance tardive"));
    }

    [Fact]
    [Trait("AC", "AC-44")]
    public async Task IncomingShare_AfterDispose_NoLongerConsumes()
    {
        // Arrange
        await _state.InitializeAsync();
        var page = _context.Render<ProgramSharing>();
        await _context.DisposeComponentsAsync();

        // Act
        _incoming.Offer(new IncomingShare(SessionJson("Séance tardive"), null));

        // Assert
        _incoming.HasPending.ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSession_FilledEditor_SavesSessionInMySessions()
    {
        // Arrange
        await _state.InitializeAsync();
        var page = _context.Render<Programs>();

        // Act
        page.Find(".create-session").Click();
        page.Find(".draft-session-name").Change("Ma séance maison");
        var exerciseId = page.Find("[data-add-exercise]").GetAttribute("data-add-exercise");
        page.Find("[data-add-exercise]").Click();
        page.Find(".save-program").Click();

        // Assert
        page.WaitForAssertion(() => _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId)
            .Sessions.Single().Exercises.Single().ExerciseId.ShouldBe(exerciseId));
        _state.Programs.Single(program => program.Id == WorkoutStateService.SessionsProgramId).Sessions.Single().Name.ShouldBe("Ma séance maison");
        page.FindAll(".program-editor").ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSession_Editor_HidesProgramControlsAndLabelsSessionSave()
    {
        // Arrange
        await _state.InitializeAsync();
        var page = _context.Render<Programs>();

        // Act
        page.Find(".create-session").Click();

        // Assert
        page.Find("#program-editor-title").TextContent.ShouldBe("Créer une séance");
        page.Find(".save-program").TextContent.ShouldBe("Enregistrer la séance");
        page.FindAll(".program-name").ShouldBeEmpty();
        page.FindAll(".program-description").ShouldBeEmpty();
        page.FindAll(".add-draft-session").ShouldBeEmpty();
        page.FindAll(".remove-draft-session").ShouldBeEmpty();
        page.FindAll(".move-session-up").ShouldBeEmpty();
        page.FindAll(".draft-favorite").ShouldBeEmpty();
        page.FindAll(".program-gym").Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSession_WithoutExercise_KeepsDraftAndWritesNothing()
    {
        // Arrange
        await _state.InitializeAsync();
        var savesBefore = _repository.Saves;
        var page = _context.Render<Programs>();
        page.Find(".create-session").Click();

        // Act
        page.Find(".save-program").Click();

        // Assert
        page.Find(".program-editor [role=alert]").TextContent.ShouldBe("Ajoute de 1 à 80 exercices dans chaque séance.");
        _repository.Saves.ShouldBe(savesBefore);
    }

    [Fact]
    [Trait("AC", "AC-45")]
    public async Task CreateSession_Cancelled_PublishesNothing()
    {
        // Arrange
        await _state.InitializeAsync();
        var savesBefore = _repository.Saves;
        var page = _context.Render<Programs>();
        page.Find(".create-session").Click();
        page.Find("[data-add-exercise]").Click();

        // Act
        page.Find(".cancel-program").Click();

        // Assert
        page.FindAll(".program-editor").ShouldBeEmpty();
        _state.Programs.ShouldNotContain(program => program.Id == WorkoutStateService.SessionsProgramId);
        _repository.Saves.ShouldBe(savesBefore);
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void Error_SessionSharingTokens_AreTranslated()
    {
        // Assert
        WorkoutUi.Error("InvalidSessionPackage").ShouldBe("Fichier de séance invalide. Tes données sont conservées.");
        WorkoutUi.Error("SessionsProgramFull").ShouldBe("Mes séances contient déjà 100 séances. Crée un programme pour les suivantes.");
    }

    private WorkoutExercise[] Plans(int count) => _state.AllExercises.Take(count)
        .Select((exercise, index) => new WorkoutExercise { ExerciseId = exercise.Id, Order = index, Sets = 3, RepTarget = "10", Rest = "1:30", Gym = "One Air" }).ToArray();

    private string SessionJson(string name) => new ExportWorkoutSessionUseCase(_sessionCodec).Execute(
        new WorkoutSession { Id = "shared", Name = name, Exercises = Plans(2) }, _state.AllExercises);

    private string ProgramJson() => new ExportWorkoutProgramsUseCase(_programCodec).Execute(
        [new WorkoutProgram { Id = "p", Name = "Programme reçu", Gym = "One Air", Sessions = [new WorkoutSession { Id = "ps", Name = "S1", Exercises = Plans(1) }] }], _state.AllExercises);

    public void Dispose() => _context.Dispose();

    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }

    private sealed class Files : IProgramFiles
    {
        public string? PickedSession { get; set; }
        public Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> PickSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(PickedSession);
        public Task ShareSessionAsync(string json, string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ShareTextAsync(string text, string title, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public int Saves { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; Saves++; return ValueTask.CompletedTask; }
    }
}
