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
public sealed class ProgramsTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly FakeRepository _repository = new();
    private readonly FakeBackupFiles _files = new();
    private readonly WorkoutStateService _state;

    public ProgramsTests()
    {
        var catalog = new DefaultTrainingCatalog();
        _state = new WorkoutStateService(_repository, catalog, TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton(TimeProvider.System);
        _context.Services.AddSingleton<IWorkoutFeedback>(new FakeWorkoutFeedback());
        _context.Services.AddSingleton<IBackupFiles>(_files);
        _context.Services.AddSingleton<IReactLocalStorageAdapter>(new ReactLocalStorageMigrationAdapter(catalog));
        _context.Services.AddTransient<ImportPerformanceBackupUseCase>();
        _context.Services.AddTransient<ExportPerformanceBackupUseCase>();
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/programmes");
    }

    [Fact]
    public void Programs_PlayButton_StartsOnlyAfterConfirmation()
    {
        // Arrange
        var page = _context.Render<Programs>();
        page.WaitForAssertion(() => page.FindAll(".library-row").Count.ShouldBeGreaterThan(0));

        // Act
        page.Find("[data-session=pc] .library-start").Click();

        // Assert
        _state.Data.ActiveWorkout.ShouldBeNull();
        page.Find("[role=dialog] h2").TextContent.ShouldContain("Démarrer");
        page.Find("[role=dialog] .primary-button").Click();
        page.WaitForAssertion(() => _state.Data.ActiveWorkout.ShouldNotBeNull());
        _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("pc");
        _repository.Saves.ShouldBe(1);
    }

    [Fact]
    public void Programs_RowTap_OpensPreviewWithExercisesBeforeStarting()
    {
        // Arrange
        var page = _context.Render<Programs>();

        // Act
        page.Find("[data-session=pc] .library-open").Click();

        // Assert
        var preview = page.Find(".start-preview");
        preview.QuerySelector("h2")!.TextContent.ShouldBe("Push · C");
        preview.QuerySelectorAll("ol li").Length.ShouldBeGreaterThan(0);
        _state.Data.ActiveWorkout.ShouldBeNull();

        // Act
        page.Find(".start-preview .primary-button").Click();
        page.Find("[role=dialog] .primary-button").Click();

        // Assert
        page.WaitForAssertion(() => _state.Data.ActiveWorkout!.Session!.Id.ShouldBe("pc"));
    }

    [Fact]
    public async Task Home_ReopenWithActiveDraft_DisplaysPersistedSet()
    {
        // Arrange
        var session = new DefaultTrainingCatalog().Programs.First().Sessions.First(session => session.Exercises.Count > 0);
        await _state.StartAsync(session, "One Air");
        var exerciseId = session.Exercises[0].ExerciseId;
        await _state.UpdateSetAsync(exerciseId, 0, new TrainingSet { Weight = "42,5", Reps = "8", Rir = "2" });
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/seance");

        // Act
        var page = _context.Render<Workout>();

        // Assert
        page.WaitForAssertion(() => page.Markup.ShouldContain("Séance en cours"));
        page.FindAll("input").ShouldContain(input => input.GetAttribute("value") == "42,5");
    }

    [Theory]
    [InlineData("42,5", "8", "340")]
    [InlineData("42.5", "8", "340")]
    [InlineData("+25", "8", "200")]
    public void ProgressValues_AcceptedWeightFormats_CalculateVolume(string weight, string reps, string expected)
    {
        // Arrange
        TrainingSet[] sets = [new() { Weight = weight, Reps = reps }];

        // Act
        var volume = Performance.App.Components.WorkoutUi.Volume(sets);

        // Assert
        volume.ShouldBe(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("2:30", 150)]
    [InlineData("2–3 min", 180)]
    [InlineData("1 min 30–2 min", 150)]
    [InlineData("2 min", 120)]
    [InlineData("1 min 30", 90)]
    [InlineData("1–1 min 30", 90)]
    [InlineData("1 min", 60)]
    [InlineData(null, 60)]
    public void RestTimer_CatalogFormats_PreserveWebDurations(string? rest, int expected)
    {
        // Act
        var seconds = Performance.App.Components.WorkoutUi.RestSeconds(rest);

        // Assert
        seconds.ShouldBe(expected);
    }

    [Fact]
    public void Settings_FilePickerCancelled_PreservesExistingData()
    {
        // Arrange
        var page = _context.Render<Settings>();

        // Act
        page.Find(".settings-panel .primary-button").Click();

        // Assert
        _repository.Saves.ShouldBe(0);
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void Settings_InvalidBackup_PreservesExistingData()
    {
        // Arrange
        _files.PickedJson = "{}";
        var page = _context.Render<Settings>();

        // Act
        page.Find(".settings-panel .primary-button").Click();

        // Assert
        _repository.Saves.ShouldBe(0);
        page.Find("[role=status]").TextContent.ShouldContain("Aucune donnée n’a été remplacée");
    }

    [Fact]
    public void Settings_ValidBackup_ReplacesOnlyAfterConfirmation()
    {
        // Arrange
        _files.PickedJson = """{"schemaVersion":2,"logs":[{"id":"imported","sessionId":"push","date":"2026-09-25T10:00:00Z","duration":35,"entries":{}}]}""";
        var page = _context.Render<Settings>();

        // Act
        page.Find(".settings-panel .primary-button").Click();

        // Assert
        _repository.Saves.ShouldBe(0);
        page.Find("[role=dialog] h2").TextContent.ShouldContain("Remplacer");
        page.Find("[role=dialog] .primary-button").Click();
        page.WaitForAssertion(() => _state.Data.History.Single().Id.ShouldBe("imported"));
        _repository.Saves.ShouldBe(1);
    }

    [Fact]
    public void Settings_ExportBackup_CanBeReimportedWithoutLosingState()
    {
        // Arrange
        var page = _context.Render<Settings>();

        // Act
        page.Find(".settings-panel .secondary-button").Click();

        // Assert
        _files.SharedJson.ShouldNotBeNull();
        new ReactLocalStorageMigrationAdapter().ImportBackup(_files.SharedJson).SchemaVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Progress_GymFilter_DoesNotMixRecordsOrMuscleSets()
    {
        // Arrange
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History = [new TrainingSessionLog
            {
                Id = "mixed", SessionId = "push", Date = DateTimeOffset.UtcNow, Exercises =
                [
                    new ExercisePerformance { ExerciseId = "bench", Gym = "One Air", Sets = [new TrainingSet { Weight = "42,5", Reps = "8" }] },
                    new ExercisePerformance { ExerciseId = "bench", Gym = "Basic-Fit", Sets = [new TrainingSet { Weight = "70", Reps = "10" }] }
                ]
            }]
        });
        var page = _context.Render<Progress>();

        // Act
        page.Find(".gym-field select").Change("Basic-Fit");

        // Assert
        page.Find(".record-card .record-metrics strong").TextContent.ShouldBe("70 kg");
        page.Find(".area-bars").TextContent.ShouldContain("1 séries");
        page.Markup.ShouldNotContain("Volume cumulé");
        page.FindAll(".record-card").Count.ShouldBe(1);
        page.Find(".record-card .eyebrow").TextContent.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task History_GymFilter_ShowsOnlySelectedGymSets()
    {
        // Arrange
        await _state.ReplaceDataAsync(new PerformanceData
        {
            History = [new TrainingSessionLog
            {
                Id = "mixed", SessionId = "push", Exercises =
                [
                    new ExercisePerformance { ExerciseId = "bench", Gym = "One Air", Sets = [new TrainingSet { Weight = "42,5", Reps = "8" }] },
                    new ExercisePerformance { ExerciseId = "bench", Gym = "Basic-Fit", Sets = [new TrainingSet { Weight = "70", Reps = "10" }] }
                ]
            }]
        });
        var page = _context.Render<History>();

        // Act
        page.Find("select").Change("Basic-Fit");

        // Assert
        page.FindAll(".history-exercise").Count.ShouldBe(1);
        page.Find(".history-exercise small").TextContent.ShouldContain("Basic-Fit");
        page.Find(".history-set").TextContent.ShouldContain("70");
        page.FindAll(".history-set").ShouldNotContain(row => row.TextContent.Contains("42,5"));
    }

    public void Dispose() => _context.Dispose();

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        public PerformanceData Data { get; private set; } = new();
        public int Saves { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            Data = data;
            Saves++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWorkoutFeedback : IWorkoutFeedback
    {
        public void NotifyRestCompleted() { }
    }

    private sealed class FakeBackupFiles : IBackupFiles
    {
        public string? PickedJson { get; set; }
        public string? SharedJson { get; private set; }
        public Task<string?> PickBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult(PickedJson);
        public Task ShareBackupAsync(string json, CancellationToken cancellationToken = default)
        {
            SharedJson = json;
            return Task.CompletedTask;
        }
    }
}
