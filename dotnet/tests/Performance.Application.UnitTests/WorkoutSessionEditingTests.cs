using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutSessionEditingTests
{
    [Fact]
    public async Task ReplaceExerciseAsync_WithHistory_ShouldKeepPositionAndPrefillSelectedGym()
    {
        // Arrange
        var repository = new Repository(new PerformanceData { History = [Log("row", "Basic-Fit", "72,5", "12")] });
        var service = Create(repository);
        await service.StartAsync(Session("press", "other"), "Basic-Fit");

        // Act
        var result = await service.ReplaceExerciseAsync("press", "row");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout!.Session!.Exercises.Select(item => item.ExerciseId).ShouldBe(["row", "other"]);
        service.GetExerciseGym("row").ShouldBe("Basic-Fit");
        service.GetSets("row")[0].ShouldBe(new TrainingSet { Weight = "72,5" });
    }

    [Fact]
    public async Task ReplaceExerciseAsync_WithUnavailableGym_ShouldSelectCompatibleGymHistory()
    {
        // Arrange
        var service = Create(new Repository(new PerformanceData { History = [Log("restricted", "Basic-Fit", "60", "8")] }));
        await service.StartAsync(Session("press"), "One Air");

        // Act
        await service.ReplaceExerciseAsync("press", "restricted");

        // Assert
        service.GetExerciseGym("restricted").ShouldBe("Basic-Fit");
        service.GetSets("restricted")[0].Weight.ShouldBe("60");
    }

    [Theory]
    [InlineData("press")]
    [InlineData("other")]
    public async Task ReplaceExerciseAsync_WithExistingExercise_ShouldRejectWithoutSaving(string replacement)
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.StartAsync(Session("press", "other"), "One Air");
        var previous = service.Data;

        // Act
        var result = await service.ReplaceExerciseAsync("press", replacement);

        // Assert
        result.Error.ShouldBe("DuplicateExercise");
        service.Data.ShouldBeSameAs(previous);
        repository.Writes.ShouldBe(1);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    public async Task ExerciseRemoval_WithDraftsInSeveralGyms_ShouldCleanOnlyAffectedExercise(string operation)
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));
        await service.InitializeAsync();
        var otherSets = service.GetSets("other");

        // Act
        var result = operation == "remove" ? await service.RemoveExerciseAsync("press") : await service.ReplaceExerciseAsync("press", "row");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.ActiveWorkout!.Draft.Keys.ShouldNotContain(key => key == "press" || key.StartsWith("press@@", StringComparison.Ordinal));
        service.Data.ActiveWorkout.ExerciseGyms.ContainsKey("press").ShouldBeFalse();
        service.Data.ActiveWorkout.RestDurations.ContainsKey("press").ShouldBeFalse();
        service.Data.ActiveWorkout.RestEnd.ShouldBeNull();
        service.GetSets("other").ShouldBeSameAs(otherSets);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    public async Task ExerciseRemoval_WithAnotherExerciseResting_ShouldPreserveThatRest(string operation)
    {
        // Arrange
        var initial = ActiveData();
        initial = initial with { ActiveWorkout = initial.ActiveWorkout! with { RestExerciseId = "other" } };
        var service = Create(new Repository(initial));

        // Act
        await (operation == "remove" ? service.RemoveExerciseAsync("press") : service.ReplaceExerciseAsync("press", "row"));

        // Assert
        service.Data.ActiveWorkout!.RestEnd.ShouldBe(initial.ActiveWorkout.RestEnd);
        service.Data.ActiveWorkout.RestExerciseId.ShouldBe("other");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task UpdateActiveExerciseAsync_WithNewSetCount_ShouldResizeAllGymDraftsPreservingRir(int count)
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));

        // Act
        var result = await service.UpdateActiveExerciseAsync(Press with { Name = "Personal press", Sets = count, Rest = "1:45", RepTarget = "8–10" });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.GetSets("press", "One Air").Count.ShouldBe(count);
        service.GetSets("press", "Basic-Fit").Count.ShouldBe(count);
        service.GetSets("press", "One Air")[0].ShouldBe(new TrainingSet { Weight = "40", Reps = "8", Rir = "2" });
        service.GetSets("press", "Basic-Fit")[0].ShouldBe(new TrainingSet { Weight = "50", Reps = "10" });
        service.Data.ActiveWorkout!.Draft.ContainsKey("press").ShouldBeFalse();
        service.Data.ActiveWorkout.Session!.Exercises[0].Sets.ShouldBe(count);
        service.GetRestSeconds("press").ShouldBe(105);
        service.AllExercises.Single(item => item.Id == "press").Name.ShouldBe("Personal press");
    }

    [Fact]
    public async Task UpdateActiveExerciseAsync_WhenGrowingSets_ShouldAppendEmptySets()
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));

        // Act
        await service.UpdateActiveExerciseAsync(Press with { Sets = 4 });

        // Assert
        service.GetSets("press", "One Air").Skip(2).ShouldBe([new TrainingSet(), new TrainingSet()]);
    }

    [Fact]
    public async Task UpdateActiveExerciseAsync_WhenGymsChange_ShouldRetainOldDraftAndChooseAvailableGym()
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));

        // Act
        await service.UpdateActiveExerciseAsync(Press with { Variants = [new GymVariant { Gym = "Basic-Fit" }] });

        // Assert
        service.GetExerciseGym("press").ShouldBe("Basic-Fit");
        service.GetSets("press", "One Air")[0].Reps.ShouldBe("8");
    }

    [Fact]
    public async Task CreateAndAddExerciseAsync_WithNewDefinition_ShouldPersistCatalogAndActiveSessionTogether()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.StartAsync(Session("press"), "One Air");

        // Act
        var result = await service.CreateAndAddExerciseAsync(new Exercise { Id = "", Name = "Cable curl", Sets = 3, Rest = "1:15", Variants = [new GymVariant { Gym = "Toutes" }] });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var exercise = service.Data.Exercises.ShouldHaveSingleItem();
        exercise.Id.ShouldNotBeNullOrWhiteSpace();
        exercise.IsCustom.ShouldBeTrue();
        service.Data.ActiveWorkout!.Session!.Exercises[1].ExerciseId.ShouldBe(exercise.Id);
        service.GetSets(exercise.Id).ShouldBe([new TrainingSet(), new TrainingSet(), new TrainingSet()]);
        repository.Writes.ShouldBe(2);
    }

    [Theory]
    [InlineData("press", "Brand new")]
    [InlineData("new", "PRÉSS")]
    [InlineData("new", "  Press  ")]
    public async Task CreateAndAddExerciseAsync_WithIdOrNameCollision_ShouldRejectWithoutSaving(string id, string name)
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.StartAsync(Session("press"), "One Air");

        // Act
        var result = await service.CreateAndAddExerciseAsync(Press with { Id = id, Name = name });

        // Assert
        result.Error.ShouldBe("ExerciseAlreadyExists");
        repository.Writes.ShouldBe(1);
        service.Data.Exercises.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAndAddExerciseAsync_WithConcurrentSameName_ShouldCreateOnlyOnce()
    {
        // Arrange
        var repository = new Repository { YieldWrites = true };
        var service = Create(repository);
        await service.StartAsync(Session("press"), "One Air");

        // Act
        var results = await Task.WhenAll(service.CreateAndAddExerciseAsync(Press with { Id = "first", Name = "Curl" }),
            service.CreateAndAddExerciseAsync(Press with { Id = "second", Name = "Curl" }));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => result.Error == "ExerciseAlreadyExists").ShouldBe(1);
        repository.Writes.ShouldBe(2);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("edit")]
    [InlineData("create")]
    public async Task SessionMutation_WhenPersistenceFails_ShouldNotPublishAnyChange(string operation)
    {
        // Arrange
        var original = ActiveData();
        var repository = new Repository(original) { FailWrites = true };
        var service = Create(repository);
        await service.InitializeAsync();
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        // Act
        var result = await Mutate(service, operation);

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
        notifications.ShouldBe(0);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("edit")]
    [InlineData("create")]
    public async Task SessionMutation_WithoutWorkout_ShouldReject(string operation)
    {
        // Arrange
        var service = Create(new Repository());

        // Act
        var result = await Mutate(service, operation);

        // Assert
        result.Error.ShouldBe("NoActiveWorkout");
    }

    [Theory]
    [InlineData(0, "", "Name")]
    [InlineData(21, "", "Name")]
    [InlineData(2, "javascript:alert(1)", "Name")]
    [InlineData(2, "", " ")]
    public async Task UpdateActiveExerciseAsync_WithInvalidDefinition_ShouldReject(int sets, string link, string name)
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));

        // Act
        var result = await service.UpdateActiveExerciseAsync(Press with { Sets = sets, Link = link, Name = name });

        // Assert
        result.Error.ShouldBe("InvalidExercise");
    }

    [Theory]
    [InlineData("edit", "0:00")]
    [InlineData("edit", "0:14")]
    [InlineData("edit", "10:01")]
    [InlineData("edit", "0:75")]
    [InlineData("edit", "999999999:00")]
    [InlineData("edit", "")]
    [InlineData("edit", "invalid")]
    [InlineData("create", "0:00")]
    [InlineData("create", "0:14")]
    [InlineData("create", "10:01")]
    [InlineData("create", "0:75")]
    [InlineData("create", "999999999:00")]
    [InlineData("create", "")]
    [InlineData("create", "invalid")]
    public async Task EditableMutation_WithInvalidRest_ShouldRejectWithoutSaving(string operation, string rest)
    {
        // Arrange
        var initial = ActiveData();
        var repository = new Repository(initial);
        var service = Create(repository);
        var candidate = Press with { Rest = rest };

        // Act
        var result = await EditOrCreate(service, operation, candidate);

        // Assert
        result.Error.ShouldBe("InvalidExercise");
        service.Data.ShouldBeSameAs(initial);
        repository.Writes.ShouldBe(0);
    }

    [Theory]
    [InlineData("edit", "0:15", 15)]
    [InlineData("edit", "10:00", 600)]
    [InlineData("create", "0:15", 15)]
    [InlineData("create", "10:00", 600)]
    public async Task EditableMutation_WithBoundaryRest_ShouldPreserveExactDuration(string operation, string rest, int expected)
    {
        // Arrange
        var service = Create(new Repository(ActiveData()));

        // Act
        var result = await EditOrCreate(service, operation, Press with { Rest = rest });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var exerciseId = operation == "edit" ? "press" : "curl";
        service.GetRestSeconds(exerciseId).ShouldBe(expected);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("create")]
    public async Task EditableMutation_WithNoGym_ShouldRejectWithoutSaving(string operation)
    {
        // Arrange
        var repository = new Repository(ActiveData());
        var service = Create(repository);

        // Act
        var result = await EditOrCreate(service, operation, Press with { Variants = [] });

        // Assert
        result.Error.ShouldBe("InvalidExercise");
        repository.Writes.ShouldBe(0);
    }

    [Theory]
    [InlineData("edit", "")]
    [InlineData("edit", " ")]
    [InlineData("create", "")]
    [InlineData("create", " ")]
    public async Task EditableMutation_WithBlankGym_ShouldRejectWithoutSaving(string operation, string gym)
    {
        // Arrange
        var repository = new Repository(ActiveData());
        var service = Create(repository);

        // Act
        var result = await EditOrCreate(service, operation, Press with { Variants = [new GymVariant { Gym = gym }] });

        // Assert
        result.Error.ShouldBe("InvalidExercise");
        repository.Writes.ShouldBe(0);
    }

    [Fact]
    public async Task SaveExerciseAsync_WithLegacyRestLabelAndImplicitGym_ShouldRemainCompatible()
    {
        // Arrange
        var service = Create(new Repository());

        // Act
        var result = await service.SaveExerciseAsync(Press with { Rest = "2–3 min", Variants = [] });

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Data.Exercises.ShouldHaveSingleItem().Rest.ShouldBe("2–3 min");
    }

    private static Task<WorkoutResult> EditOrCreate(WorkoutStateService service, string operation, Exercise exercise) =>
        operation == "edit" ? service.UpdateActiveExerciseAsync(exercise)
            : service.CreateAndAddExerciseAsync(exercise with { Id = "curl", Name = "Curl" });

    private static Task<WorkoutResult> Mutate(WorkoutStateService service, string operation) => operation switch
    {
        "remove" => service.RemoveExerciseAsync("press"),
        "replace" => service.ReplaceExerciseAsync("press", "row"),
        "edit" => service.UpdateActiveExerciseAsync(Press with { Sets = 3 }),
        _ => service.CreateAndAddExerciseAsync(Press with { Id = "curl", Name = "Curl" })
    };

    private static readonly Exercise Press = new() { Id = "press", Name = "Press", Sets = 2, Rest = "1:30", Variants = [new GymVariant { Gym = "Toutes" }] };
    private static WorkoutSession Session(params string[] ids) => new()
    {
        Id = "upper", Name = "Upper", Exercises = ids.Select((id, order) => new WorkoutExercise { ExerciseId = id, Order = order }).ToArray()
    };
    private static TrainingSessionLog Log(string id, string gym, string weight, string reps) => new()
    {
        Id = "history", SessionId = "upper", Date = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero),
        Exercises = [new ExercisePerformance { ExerciseId = id, Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = reps, Rir = "3" }] }]
    };
    private static PerformanceData ActiveData() => new()
    {
        ActiveWorkout = new ActiveWorkout
        {
            Session = Session("press", "other"), DefaultGym = "One Air", StartedAt = DateTimeOffset.UnixEpoch,
            Draft = new Dictionary<string, IReadOnlyList<TrainingSet>>
            {
                ["press"] = [new TrainingSet { Weight = "40", Reps = "8", Rir = "2" }, new TrainingSet { Weight = "42", Reps = "9" }],
                ["press@@Basic-Fit"] = [new TrainingSet { Weight = "50", Reps = "10" }, new TrainingSet()],
                ["other@@One Air"] = [new TrainingSet { Weight = "10", Reps = "12" }]
            },
            ExerciseGyms = new Dictionary<string, string> { ["press"] = "One Air", ["other"] = "One Air" },
            RestDurations = new Dictionary<string, int> { ["press"] = 120, ["other"] = 60 },
            RestEnd = DateTimeOffset.MaxValue, RestExerciseId = "press"
        }
    };
    private static WorkoutStateService Create(Repository repository) => new(repository, new Catalog(), new FakeTimeProvider());
    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [Press, new Exercise { Id = "row", Name = "Row", Sets = 3 },
            new Exercise { Id = "other", Name = "Other" }, new Exercise { Id = "restricted", Name = "Restricted", Variants = [new GymVariant { Gym = "Basic-Fit" }] }];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }
    private sealed class Repository(PerformanceData? initial = null) : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = initial ?? new();
        public bool FailWrites { get; init; }
        public bool YieldWrites { get; init; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Simulated write failure");
            if (YieldWrites) await Task.Yield();
            Stored = data;
            Writes++;
        }
    }
}
