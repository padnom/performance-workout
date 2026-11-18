using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutStateServiceTests
{
    [Theory]
    [InlineData("One Air", "40")]
    [InlineData("Basic Fit", "70")]
    public async Task StartAsync_WithGymHistory_ShouldPrefillOnlyMatchingWeights(string gym, string expectedWeight)
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData { History = [Log("older", "One Air", "40"), Log("newer", "Basic Fit", "70")] });
        var service = CreateService(repository);

        // Act
        await service.StartAsync(Session(), gym);

        // Assert
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = expectedWeight });
    }

    [Theory]
    [InlineData("One Air", "25")]
    [InlineData("Basic Fit", "")]
    public async Task StartAsync_WithLegacyHistory_ShouldTreatMissingGymAsOneAir(string gym, string expectedWeight)
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData { History = [Log("legacy", null, "25")] }));

        // Act
        await service.StartAsync(Session(), gym);

        // Assert
        service.GetSets("press")[0].Weight.ShouldBe(expectedWeight);
    }

    [Fact]
    public async Task StartAsync_WithActiveWorkout_ShouldPreserveExistingSession()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(), "One Air");
        var original = service.Data.ActiveWorkout;

        // Act
        var result = await service.StartAsync(Session() with { Id = "replacement" }, "Basic Fit");

        // Assert
        result.Error.ShouldBe("ActiveWorkoutExists");
        service.Data.ActiveWorkout.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task FinishAsync_WithOnlyPrefilledWeights_ShouldRejectEmptyWorkout()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData { History = [Log("old", "One Air", "40")] }));
        await service.StartAsync(Session(), "One Air");

        // Act
        var result = await service.FinishAsync();

        // Assert
        result.Error.ShouldBe("EmptyWorkout");
        service.Data.ActiveWorkout.ShouldNotBeNull();
    }

    [Fact]
    public async Task FinishAsync_AfterRestart_ShouldPersistSetsGymAndElapsedDuration()
    {
        // Arrange
        var repository = new FakeRepository();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
        var service = CreateService(repository, clock);
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "70", Reps = "10", Rir = "2" }, "Basic Fit");
        clock.Advance(TimeSpan.FromMinutes(42));
        var resumed = CreateService(repository, clock);
        await resumed.InitializeAsync();

        // Act
        var result = await resumed.FinishAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        repository.Stored.ActiveWorkout.ShouldBeNull();
        var log = repository.Stored.History.ShouldHaveSingleItem();
        log.DurationMinutes.ShouldBe(42);
        log.Date.ShouldBe(clock.GetUtcNow());
        log.Name.ShouldBe("Upper");
        log.Exercises.ShouldHaveSingleItem().ShouldBe(new ExercisePerformance
        {
            ExerciseId = "press", Gym = "Basic Fit", Sets = log.Exercises[0].Sets
        });
        log.Exercises[0].Sets.ShouldHaveSingleItem().ShouldBe(new TrainingSet { Weight = "70", Reps = "10", Rir = "2" });
    }

    [Fact]
    public async Task FinishAsync_WithImportedGymDraft_ShouldRetainUnrelatedMigrationData()
    {
        // Arrange
        var storage = new Dictionary<string, string> { ["legacy-key"] = "raw payload" };
        var repository = new FakeRepository(new PerformanceData
        {
            History = [Log("existing", "Basic Fit", "20")], ReactLocalStorage = storage,
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), StartedAt = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero), DefaultGym = "One Air",
                ExerciseGyms = new Dictionary<string, string> { ["press"] = "Basic Fit" },
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press@@Basic Fit"] = [new TrainingSet { Weight = "75", Reps = "8" }] }
            }
        });
        var service = CreateService(repository);

        // Act
        await service.FinishAsync();

        // Assert
        repository.Stored.History.Count.ShouldBe(2);
        repository.Stored.History[1].Exercises[0].Gym.ShouldBe("Basic Fit");
        repository.Stored.History[1].Exercises[0].Sets[0].Weight.ShouldBe("75");
        repository.Stored.ReactLocalStorage.ShouldBeSameAs(storage);
    }

    [Fact]
    public async Task InitializeAsync_WithBareImportedDraft_ShouldRespectExerciseGym()
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                ExerciseGyms = new Dictionary<string, string> { ["press"] = "Basic Fit" },
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press"] = [new TrainingSet { Weight = "80", Reps = "6" }] }
            }
        });
        var service = CreateService(repository);

        // Act
        await service.InitializeAsync();

        // Assert
        service.GetExerciseGym("press").ShouldBe("Basic Fit");
        service.GetSets("press")[0].Weight.ShouldBe("80");
        service.GetSets("press", "One Air")[0].Weight.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetExerciseGymAsync_AfterEdits_ShouldRestoreSeparateGymDraft()
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "40", Reps = "12" });
        await service.SetExerciseGymAsync("press", "Basic Fit");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "65", Reps = "8" });

        // Act
        await service.SetExerciseGymAsync("press", "One Air");

        // Assert
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = "40", Reps = "12" });
        service.GetSets("press", "Basic Fit")[0].Weight.ShouldBe("65");
    }

    [Fact]
    public async Task SetExerciseGymAsync_WithBareImportedDraft_ShouldPreserveOriginalGymSets()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press"] = [new TrainingSet { Weight = "40", Reps = "12" }] }
            }
        }));
        await service.SetExerciseGymAsync("press", "Basic Fit");

        // Act
        await service.SetExerciseGymAsync("press", "One Air");

        // Assert
        service.GetSets("press")[0].ShouldBe(new TrainingSet { Weight = "40", Reps = "12" });
    }

    [Fact]
    public async Task StartAsync_WithEquivalentExerciseName_ShouldUseAliasHistory()
    {
        // Arrange
        var oldLog = Log("old", "One Air", "50");
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            Exercises = [new Exercise { Id = "legacy-press", Name = "PRÉSS" }],
            History = [oldLog with { Exercises = [oldLog.Exercises[0] with { ExerciseId = "legacy-press" }] }]
        }));

        // Act
        await service.StartAsync(Session(), "One Air");

        // Assert
        service.GetSets("press")[0].Weight.ShouldBe("50");
    }

    [Fact]
    public async Task CancelAsync_WithActiveWorkout_ShouldPersistDraftRemoval()
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData { History = [Log("old", "One Air", "40")] });
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");

        // Act
        await service.CancelAsync();

        // Assert
        repository.Stored.ActiveWorkout.ShouldBeNull();
        repository.Stored.History.ShouldHaveSingleItem().Id.ShouldBe("old");
    }

    [Fact]
    public async Task FinishAsync_WhenPersistenceFails_ShouldPreserveActiveDraftAndHistory()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Reps = "10" });
        var original = service.Data;
        repository.FailWrites = true;

        // Act
        var result = await service.FinishAsync();

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(original);
        repository.Stored.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task UpdateSetAsync_WhenPersistenceFails_ShouldPreservePreviousSet()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        repository.FailWrites = true;

        // Act
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "50", Reps = "8" });

        // Assert
        service.GetSets("press")[0].ShouldBe(new TrainingSet());
    }

    [Fact]
    public async Task UpdateSetAsync_WithConcurrentEdits_ShouldPersistBothSets()
    {
        // Arrange
        var repository = new FakeRepository { YieldWrites = true };
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");

        // Act
        await Task.WhenAll(service.UpdateSetAsync("press", 0, new TrainingSet { Reps = "10" }),
            service.UpdateSetAsync("press", 1, new TrainingSet { Reps = "8" }));

        // Assert
        repository.Stored.ActiveWorkout!.Draft["press@@One Air"].Select(set => set.Reps).ShouldBe(["10", "8"]);
    }

    [Fact]
    public async Task SaveExerciseAsync_WithNewExercise_ShouldAssignUniqueId()
    {
        // Arrange
        var service = CreateService(new FakeRepository());

        // Act
        await service.SaveExerciseAsync(new Exercise { Id = "", Name = "Row", Sets = 3 });

        // Assert
        var exercise = service.Data.Exercises.ShouldHaveSingleItem();
        Guid.TryParse(exercise.Id, out _).ShouldBeTrue();
        exercise.IsCustom.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveExerciseAsync_WithExistingExercise_ShouldPreserveHistory()
    {
        // Arrange
        var history = new[] { Log("existing", "One Air", "40") };
        var service = CreateService(new FakeRepository(new PerformanceData { History = history }));

        // Act
        await service.SaveExerciseAsync(new Exercise { Id = "press", Name = "Updated press", Sets = 3 });

        // Assert
        service.Data.History.ShouldBeSameAs(history);
        service.Exercises.ShouldHaveSingleItem().Name.ShouldBe("Updated press");
    }

    [Fact]
    public async Task InitializeAsync_WithImportedGymSetting_ShouldUseOriginalSettingKey()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            Settings = new Dictionary<string, string> { ["training-workout-gym"] = "Basic-Fit" }
        }));

        // Act
        await service.InitializeAsync();

        // Assert
        service.CurrentGym.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task SetGymAsync_WithSelectedGym_ShouldUpdateMigrationSettingKey()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);

        // Act
        await service.SetGymAsync("Basic-Fit");

        // Assert
        repository.Stored.Settings.GetValueOrDefault("training-workout-gym").ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task SetGymAsync_WithImportedBareDraft_ShouldKeepExistingExerciseGym()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press"] = [new TrainingSet { Reps = "10" }] }
            }
        }));

        // Act
        await service.SetGymAsync("Basic-Fit");

        // Assert
        service.GetExerciseGym("press").ShouldBe("One Air");
        service.GetSets("press")[0].Reps.ShouldBe("10");
        service.CurrentGym.ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task StartAsync_WithAllGymCatalogExercise_ShouldSelectCurrentGym()
    {
        // Arrange
        var catalog = new FakeCatalog([new Exercise { Id = "press", Name = "Press", Variants = [new GymVariant { Gym = "Toutes" }] }]);
        var service = CreateService(new FakeRepository(), catalog: catalog);

        // Act
        await service.StartAsync(Session() with { Exercises = [new WorkoutExercise { ExerciseId = "press", Gym = "Toutes" }] }, "Basic-Fit");

        // Assert
        service.GetExerciseGym("press").ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task StartAsync_WithExerciseAvailableInOneGym_ShouldSelectAvailableGym()
    {
        // Arrange
        var catalog = new FakeCatalog([new Exercise { Id = "press", Name = "Press", Variants = [new GymVariant { Gym = "One Air" }] }]);
        var service = CreateService(new FakeRepository(), catalog: catalog);

        // Act
        await service.StartAsync(Session(), "Basic-Fit");

        // Assert
        service.GetExerciseGym("press").ShouldBe("One Air");
    }

    [Fact]
    public async Task AddExerciseAsync_WithExerciseAvailableInOneGym_ShouldSelectAvailableGym()
    {
        // Arrange
        var catalog = new FakeCatalog([new Exercise { Id = "press", Name = "Press", Variants = [new GymVariant { Gym = "One Air" }] }]);
        var service = CreateService(new FakeRepository(), catalog: catalog);
        await service.StartAsync(Session() with { Exercises = [] }, "Basic-Fit");

        // Act
        await service.AddExerciseAsync("press");

        // Assert
        service.GetExerciseGym("press").ShouldBe("One Air");
    }

    [Fact]
    public async Task StartAsync_WithHiddenCatalogExercise_ShouldKeepPlannedTrainingUsable()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData { HiddenExerciseIds = ["press"] }));

        // Act
        var result = await service.StartAsync(Session(), "One Air");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.Exercises.ShouldBeEmpty();
        service.Data.ActiveWorkout!.Session!.Exercises.ShouldHaveSingleItem().ExerciseId.ShouldBe("press");
    }

    [Fact]
    public async Task ReplaceDataAsync_WhenPersistenceFails_ShouldRetainPreviousAggregate()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        var previous = service.Data;
        repository.FailWrites = true;

        // Act
        var result = await service.ReplaceDataAsync(new PerformanceData { History = [Log("import", "Basic-Fit", "70")] });

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.Data.ShouldBeSameAs(previous);
        repository.Stored.ShouldBeSameAs(previous);
    }

    [Fact]
    public async Task ReplaceDataAsync_WithImportedAggregate_ShouldPublishPersistedData()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var imported = new PerformanceData { History = [Log("import", "Basic-Fit", "70")] };

        // Act
        await service.ReplaceDataAsync(imported);

        // Assert
        service.Data.ShouldBeSameAs(imported);
        repository.Stored.ShouldBeSameAs(imported);
    }

    [Theory]
    [InlineData("-1", "8", "2")]
    [InlineData("abc", "8", "2")]
    [InlineData("25,5,2", "8", "2")]
    [InlineData("25", "abc", "2")]
    [InlineData("25", "-1", "2")]
    [InlineData("25", "1.5", "2")]
    [InlineData("25", "2147483648", "2")]
    [InlineData("25", "8", "-0,5")]
    [InlineData("25", "8", "10,1")]
    [InlineData("25", "8", "abc")]
    public async Task UpdateSetAsync_WithInvalidNumbers_ShouldRejectWithoutPersistence(string weight, string repetitions, string reserve)
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        var previous = service.Data;

        // Act
        var result = await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = weight, Reps = repetitions, Rir = reserve });

        // Assert
        result.Error.ShouldBe("InvalidSet");
        repository.SuccessfulWrites.ShouldBe(1);
        service.Data.ShouldBeSameAs(previous);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("0", "0", "0")]
    [InlineData("25.5", "8", "2.5")]
    [InlineData("25,5", "8", "2,5")]
    [InlineData("", "10", "10")]
    public async Task UpdateSetAsync_WithValidNumbers_ShouldPersistOriginalValues(string weight, string repetitions, string reserve)
    {
        // Arrange
        var service = CreateService(new FakeRepository());
        await service.StartAsync(Session(), "One Air");
        var entered = new TrainingSet { Weight = weight, Reps = repetitions, Rir = reserve };

        // Act
        var result = await service.UpdateSetAsync("press", 0, entered);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        service.GetSets("press")[0].ShouldBe(entered);
    }

    [Theory]
    [InlineData("abc", "InvalidSet")]
    [InlineData("0", "EmptyWorkout")]
    [InlineData("-1", "InvalidSet")]
    [InlineData("1.5", "InvalidSet")]
    [InlineData(" ", "EmptyWorkout")]
    public async Task FinishAsync_WithInvalidImportedRepetitions_ShouldPreserveActiveWorkout(string repetitions, string expectedError)
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press"] = [new TrainingSet { Weight = "40", Reps = repetitions }] }
            }
        });
        var service = CreateService(repository);
        await service.InitializeAsync();
        var previous = service.Data;

        // Act
        var result = await service.FinishAsync();

        // Assert
        result.Error.ShouldBe(expectedError);
        service.Data.ShouldBeSameAs(previous);
        repository.SuccessfulWrites.ShouldBe(0);
    }

    [Fact]
    public async Task FinishAsync_WithEnteredSetsInTwoGyms_ShouldPreserveBothGymPerformances()
    {
        // Arrange
        var opaqueStorage = new Dictionary<string, string> { ["opaque"] = "exact legacy value" };
        var repository = new FakeRepository(new PerformanceData { ReactLocalStorage = opaqueStorage });
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "40", Reps = "12" });
        await service.SetExerciseGymAsync("press", "Basic-Fit");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "65", Reps = "8" });

        // Act
        await service.FinishAsync();

        // Assert
        var performances = repository.Stored.History.ShouldHaveSingleItem().Exercises;
        performances.Count.ShouldBe(2);
        performances.Single(item => item.Gym == "One Air").Sets.ShouldHaveSingleItem().ShouldBe(new TrainingSet { Weight = "40", Reps = "12" });
        performances.Single(item => item.Gym == "Basic-Fit").Sets.ShouldHaveSingleItem().ShouldBe(new TrainingSet { Weight = "65", Reps = "8" });
        repository.Stored.ReactLocalStorage.ShouldBeSameAs(opaqueStorage);
    }

    [Fact]
    public async Task FinishAsync_WithMixedValidAndInvalidImportedSets_ShouldPreserveEntireDraft()
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>>
                {
                    ["press"] = [new TrainingSet { Weight = "40", Reps = "8" }, new TrainingSet { Weight = "42", Reps = "abc" }]
                }
            }
        });
        var service = CreateService(repository);
        await service.InitializeAsync();
        var original = service.Data;

        // Act
        var result = await service.FinishAsync();

        // Assert
        result.Error.ShouldBe("InvalidSet");
        service.Data.ShouldBeSameAs(original);
        repository.SuccessfulWrites.ShouldBe(0);
    }

    [Fact]
    public async Task FinishAsync_WithBareDraftAndOtherGymDraft_ShouldPreserveBoth()
    {
        // Arrange
        var repository = new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session(), DefaultGym = "One Air",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>>
                {
                    ["press"] = [new TrainingSet { Weight = "40", Reps = "12" }],
                    ["press@@Basic-Fit"] = [new TrainingSet { Weight = "65", Reps = "8" }]
                }
            }
        });
        var service = CreateService(repository);

        // Act
        await service.FinishAsync();

        // Assert
        repository.Stored.History.ShouldHaveSingleItem().Exercises.Select(item => item.Gym).Order().ShouldBe(["Basic-Fit", "One Air"]);
    }

    [Fact]
    public async Task FinishAsync_WithSelectedGymOnlyPrefilled_ShouldPreserveOtherGymCompletedSets()
    {
        // Arrange
        var repository = new FakeRepository();
        var service = CreateService(repository);
        await service.StartAsync(Session(), "One Air");
        await service.UpdateSetAsync("press", 0, new TrainingSet { Weight = "40", Reps = "12" });
        await service.SetExerciseGymAsync("press", "Basic-Fit");

        // Act
        var result = await service.FinishAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        repository.Stored.History.ShouldHaveSingleItem().Exercises.ShouldHaveSingleItem().Gym.ShouldBe("One Air");
    }

    [Fact]
    public async Task InitializeAsync_WithImportedAllGymSentinel_ShouldUseWorkoutDefaultGym()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session() with { Exercises = [new WorkoutExercise { ExerciseId = "press", Gym = "Toutes" }] },
                DefaultGym = "Basic-Fit",
                Draft = new Dictionary<string, IReadOnlyList<TrainingSet>> { ["press"] = [new TrainingSet { Weight = "65", Reps = "8" }] }
            }
        }));

        // Act
        await service.InitializeAsync();

        // Assert
        service.GetExerciseGym("press").ShouldBe("Basic-Fit");
    }

    [Fact]
    public async Task InitializeAsync_WithExplicitGymOverridingSentinel_ShouldKeepExplicitSelection()
    {
        // Arrange
        var service = CreateService(new FakeRepository(new PerformanceData
        {
            ActiveWorkout = new ActiveWorkout
            {
                Session = Session() with { Exercises = [new WorkoutExercise { ExerciseId = "press", Gym = "Toutes" }] },
                DefaultGym = "Basic-Fit", ExerciseGyms = new Dictionary<string, string> { ["press"] = "One Air" }
            }
        }));

        // Act
        await service.InitializeAsync();

        // Assert
        service.GetExerciseGym("press").ShouldBe("One Air");
    }

    private static WorkoutStateService CreateService(FakeRepository repository, FakeTimeProvider? clock = null, FakeCatalog? catalog = null) =>
        new(repository, catalog ?? new FakeCatalog(), clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero)));

    private static WorkoutSession Session() => new()
    {
        Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press", Sets = 2 }]
    };

    private static TrainingSessionLog Log(string id, string? gym, string weight) => new()
    {
        Id = id, SessionId = "upper", Date = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
        Exercises = [new ExercisePerformance { ExerciseId = "press", Gym = gym, Sets = [new TrainingSet { Weight = weight, Reps = "12", Rir = "2" }] }]
    };

    private sealed class FakeCatalog(IReadOnlyList<Exercise>? exercises = null) : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises { get; } = exercises ?? [new Exercise { Id = "press", Name = "Press", Sets = 2 }];
        public IReadOnlyList<WorkoutProgram> Programs { get; } = [];
    }

    private sealed class FakeRepository(PerformanceData? data = null) : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; private set; } = data ?? new();
        public bool FailWrites { get; set; }
        public bool YieldWrites { get; set; }
        public int SuccessfulWrites { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Test write failure");
            if (YieldWrites) await Task.Yield();
            Stored = data;
            SuccessfulWrites++;
        }
    }
}
