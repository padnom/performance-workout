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
public sealed class TrainingProgramsUiTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly Repository _repository = new();
    private readonly Files _files = new();
    private readonly AppNavigationService _navigation = new();
    private readonly JsonProgramPackageCodec _codec = new();
    private readonly WorkoutStateService _state;

    public TrainingProgramsUiTests()
    {
        _state = new(_repository, new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton<IProgramFiles>(_files);
        _context.Services.AddSingleton<IProgramPackageCodec>(_codec);
        _context.Services.AddTransient<ImportWorkoutProgramsUseCase>();
        _context.Services.AddTransient<ExportWorkoutProgramsUseCase>();
        _context.Services.AddSingleton(_navigation);
        _context.Services.AddSingleton<IWorkoutFeedback>(new Feedback());
    }

    [Fact]
    public void Programs_GymSelection_ShowsFourSessionsAndKeepsLegacyLibraryFolded()
    {
        // Act
        var page = _context.Render<Programs>();

        // Assert
        page.FindAll(".gym-session").Count.ShouldBe(4);
        page.Find(".gym-program h2").TextContent.ShouldBe("Ton programme — One Air");
        page.Find(".other-programs").HasAttribute("open").ShouldBeFalse();
        page.FindAll(".category-filter").Count.ShouldBe(6);

        // Act
        page.Find("[aria-label='Salle du programme']").Change("Basic-Fit");

        // Assert
        page.FindAll(".gym-session").ShouldAllBe(card => card.GetAttribute("data-gym-session")!.StartsWith("basic-fit-"));
        _repository.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task Programs_ApplyWeek_RequiresConfirmationAndPreservesActiveDraft()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.AddWeekSessionAsync("pa");
        await _state.StartAsync(_state.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        var active = _state.Data.ActiveWorkout;
        var page = _context.Render<Programs>();

        // Act
        page.Find("[aria-label='Salle du programme']").Change("Basic-Fit");
        page.Find(".use-gym-week").Click();

        // Assert
        _state.WeekSessions.Single().Id.ShouldBe("pa");

        // Act
        page.Find(".confirm-gym-week").Click();

        // Assert
        page.WaitForAssertion(() => _state.WeekSessions.Count.ShouldBe(4));
        _state.WeekSessions.ShouldAllBe(session => session.Id.StartsWith("basic-fit-"));
        _state.Data.ActiveWorkout.ShouldBeSameAs(active);
    }

    [Fact]
    public async Task Programs_WeekConfirmation_BackCancelsWithoutWrite()
    {
        // Arrange
        var page = _context.Render<Programs>();
        page.Find(".use-gym-week").Click();

        // Act
        await page.InvokeAsync(() => _navigation.RequestBackAsync());

        // Assert
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        _repository.Saves.ShouldBe(0);
    }

    [Fact]
    public void Programs_Start_GymChangeMapsPreviewAndActualSession()
    {
        // Arrange
        var page = _context.Render<Programs>();
        page.Find(".gym-start").Click();

        // Act
        page.Find(".start-preview select").Change("Basic-Fit");

        // Assert
        page.Find(".start-preview").TextContent.ShouldNotContain("Life Fitness Chest Press");
        _state.Data.ActiveWorkout.ShouldBeNull();

        // Act
        page.Find(".start-preview .primary-button").Click();
        page.Find("[role=dialog] .primary-button").Click();

        // Assert
        page.WaitForAssertion(() => _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("basic-fit-push"));
        _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task Workout_OptionalAbs_AreNotCountedUntilManualAddition()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.StartAsync(_state.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        var page = _context.Render<Workout>();

        // Assert
        page.Find(".workout-completion").TextContent.ShouldContain("0 / 20");
        page.Find(".optional-abs").TextContent.ShouldContain("10–15");
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(5);

        // Act
        page.Find(".add-optional-abs").Click();

        // Assert
        page.WaitForAssertion(() => page.FindAll(".optional-abs").ShouldBeEmpty());
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(7);
        page.Find(".workout-completion").TextContent.ShouldContain("0 / 24");
    }

    [Fact]
    public void Programs_BasicFitCard_OpensWithItsGymEvenWhenSettingsRemainOneAir()
    {
        // Arrange
        var page = _context.Render<Programs>();
        page.Find("[aria-label='Salle du programme']").Change("Basic-Fit");

        // Act
        page.Find(".gym-start").Click();

        // Assert
        page.Find(".start-preview select").GetAttribute("value").ShouldBe("Basic-Fit");
        _state.CurrentGym.ShouldBe("One Air");
    }

    [Fact]
    public async Task Programs_ImportedBasicFitProgram_StartsInItsOwnGymIncludingStandardEquipment()
    {
        // Arrange
        await _state.InitializeAsync();
        var original = _state.GetRecommendedProgram("Basic-Fit")!;
        var shareable = original with { Sessions = original.Sessions.Select(session => session with
        {
            Exercises = session.Exercises.Select(plan => plan with { Gym = null }).ToArray(),
            OptionalExercises = session.OptionalExercises.Select(plan => plan with { Gym = null }).ToArray()
        }).ToArray() };
        var json = new ExportWorkoutProgramsUseCase(_codec).Execute([shareable], _state.AllExercises);
        (await _state.ImportProgramsAsync(new ImportWorkoutProgramsUseCase(_codec).Execute(json))).IsSuccess.ShouldBeTrue();
        var imported = _state.Data.Programs.Single();
        await _state.AddWeekSessionAsync(imported.Sessions[0].Id);
        var page = _context.Render<Programs>();
        page.Find("[data-family=custom]").Click();

        // Act
        page.Find(".start-library").Click();

        // Assert
        page.Find("[role=dialog] select").GetAttribute("value").ShouldBe("Basic-Fit");
        _state.CurrentGym.ShouldBe("One Air");

        // Act
        page.Find("[role=dialog] .primary-button").Click();

        // Assert
        page.WaitForAssertion(() => _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit"));
        _state.Data.ActiveWorkout!.ExerciseGyms.Values.ShouldAllBe(gym => gym == "Basic-Fit");
        _state.Data.ActiveWorkout.Session!.Id.ShouldBe(imported.Sessions[0].Id);
    }

    [Fact]
    public async Task Workout_FailedOptionalAddition_RetainsOfferAndCoreCounts()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.StartAsync(_state.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        var page = _context.Render<Workout>();
        _repository.Fail = true;

        // Act
        page.Find(".add-optional-abs").Click();

        // Assert
        page.Find(".optional-abs").ShouldNotBeNull();
        page.Find(".workout-completion").TextContent.ShouldContain("0 / 20");
        _state.Data.ActiveWorkout!.Session!.Exercises.Count.ShouldBe(5);
        page.Find(".notice").TextContent.ShouldContain("L’enregistrement a échoué");
    }

    [Fact]
    public async Task Workout_ExplicitSessionGym_ChangesDefaultWithoutChangingExistingMachineOrSettings()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.StartAsync(_state.GetRecommendedProgram("One Air")!.Sessions[0], "One Air");
        var firstId = _state.Data.ActiveWorkout!.Session!.Exercises[0].ExerciseId;
        await _state.UpdateSetAsync(firstId, 0, new TrainingSet { Weight = "40", Reps = "10" });
        var page = _context.Render<Workout>();

        // Act
        page.Find(".workout-page > .gym-field select").Change("Basic-Fit");

        // Assert
        _state.Data.ActiveWorkout!.DefaultGym.ShouldBe("Basic-Fit");
        _state.Data.ActiveWorkout.ExerciseGyms[firstId].ShouldBe("One Air");
        _state.CurrentGym.ShouldBe("One Air");
        _state.GetSets(firstId)[0].Weight.ShouldBe("40");
    }

    [Fact]
    public async Task ProgramSharing_ExportWeek_ContainsOnlyTrainingDefinitions()
    {
        // Arrange
        await _state.InitializeAsync();
        await _state.UseGymProgramWeekAsync("One Air");
        var page = _context.Render<ProgramSharing>();

        // Act
        page.Find("input[type=checkbox]").Change(true);
        page.Find(".export-programs").Click();

        // Assert
        page.WaitForAssertion(() => _files.Shared.ShouldNotBeNull());
        var package = _codec.Decode(_files.Shared!);
        package.Programs.Single().Name.ShouldBe("Ma semaine");
        package.Programs.Single().Sessions.Count.ShouldBe(4);
        _files.Shared!.ShouldNotContain("activeWorkout");
        _files.Shared!.ShouldNotContain("history");
        _files.Shared!.ShouldNotContain("weight");
    }

    [Fact]
    public async Task ProgramSharing_ImportPreview_OnlyAddsAfterConfirmation()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.Picked = new ExportWorkoutProgramsUseCase(_codec).Execute([_state.GetRecommendedProgram("One Air")!], _state.AllExercises);
        var page = _context.Render<ProgramSharing>();
        var existing = _state.Data.Programs.Count;

        // Act
        page.Find(".import-programs").Click();

        // Assert
        page.Find(".program-import-preview").TextContent.ShouldContain("1 programme(s)");
        _state.Data.Programs.Count.ShouldBe(existing);

        // Act
        page.Find(".confirm-program-import").Click();

        // Assert
        page.WaitForAssertion(() => _state.Data.Programs.Count.ShouldBe(existing + 1));
        _state.WeekSessions.ShouldBeEmpty();
        page.FindAll(".program-import-preview").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public void ProgramSharing_CancelledOrInvalidFile_DoesNotWrite(string? json)
    {
        // Arrange
        _files.Picked = json;
        _context.Render<Programs>();
        var page = _context.Render<ProgramSharing>();

        // Act
        page.Find(".import-programs").Click();

        // Assert
        _repository.Saves.ShouldBe(0);
        page.FindAll(".program-import-preview").ShouldBeEmpty();
    }

    [Fact]
    public async Task ProgramSharing_FailedImport_LeavesPreviewOpenAndDataIntact()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.Picked = new ExportWorkoutProgramsUseCase(_codec).Execute([_state.GetRecommendedProgram("One Air")!], _state.AllExercises);
        var page = _context.Render<ProgramSharing>();
        page.Find(".import-programs").Click();
        _repository.Fail = true;

        // Act
        page.Find(".confirm-program-import").Click();

        // Assert
        page.Find(".program-import-preview [role=alert]").TextContent.ShouldContain("L’enregistrement a échoué");
        _state.Data.Programs.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProgramSharing_PreviewBack_CancelsWithoutWriting()
    {
        // Arrange
        await _state.InitializeAsync();
        _files.Picked = new ExportWorkoutProgramsUseCase(_codec).Execute([_state.GetRecommendedProgram("One Air")!], _state.AllExercises);
        var page = _context.Render<ProgramSharing>();
        page.Find(".import-programs").Click();

        // Act
        await page.InvokeAsync(() => _navigation.RequestBackAsync());

        // Assert
        page.FindAll(".program-import-preview").ShouldBeEmpty();
        _repository.Saves.ShouldBe(0);
    }

    [Fact]
    public async Task ProgramSharing_MultiplePrograms_ExportsBothGymProfiles()
    {
        // Arrange
        await _state.InitializeAsync();
        var page = _context.Render<ProgramSharing>();

        // Act
        page.Find("[data-share-program=gym-one-air]").Change(true);
        page.Find("[data-share-program=gym-basic-fit]").Change(true);
        page.Find(".export-programs").Click();

        // Assert
        var package = _codec.Decode(_files.Shared!);
        package.Programs.Select(program => program.Gym).ShouldBe(new[] { "One Air", "Basic-Fit" });
        package.Programs.Sum(program => program.Sessions.Count).ShouldBe(8);
    }

    public void Dispose() => _context.Dispose();
    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }
    private sealed class Files : IProgramFiles
    {
        public string? Picked { get; set; }
        public string? Shared { get; private set; }
        public Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Picked);
        public Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default) { Shared = json; return Task.CompletedTask; }
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public int Saves { get; private set; }
        public bool Fail { get; set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        { if (Fail) throw new IOException("test failure"); _data = data; Saves++; return ValueTask.CompletedTask; }
    }
}
