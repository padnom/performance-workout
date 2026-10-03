using JetBrains.Annotations;
using Microsoft.Extensions.Time.Testing;
using Performance.Domain;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(WorkoutStateService))]
public sealed class WorkoutPublicationTests
{
    [Fact]
    public async Task DataChanged_WhenObserverThrows_ShouldPersistAndNotifyRemainingObservers()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.InitializeAsync();
        PerformanceData? observed = null;
        service.DataChanged += _ => throw new InvalidOperationException("Observer failed");
        service.DataChanged += data => observed = data;

        // Act
        var result = await service.SetGymAsync("Basic-Fit");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        observed.ShouldBeSameAs(repository.Stored);
        observed.ShouldBeSameAs(service.Data);
    }

    [Fact]
    public async Task DataChanged_WhenLoadingAgainWithoutReload_ShouldNotifyOnlyFirstLoad()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        var notifications = 0;
        service.DataChanged += _ => notifications++;

        // Act
        await service.InitializeAsync();
        await service.InitializeAsync();

        // Assert
        notifications.ShouldBe(1);
    }

    [Fact]
    public async Task DataChanged_WhenReloading_ShouldPublishFreshRepositoryData()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.InitializeAsync();
        PerformanceData? observed = null;
        service.DataChanged += data => observed = data;
        repository.Stored = new PerformanceData { Settings = new Dictionary<string, string> { ["training-rest-notifications-enabled"] = "True" } };

        // Act
        await service.ReloadAsync();

        // Assert
        observed.ShouldBeSameAs(repository.Stored);
        service.RestNotificationsEnabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("False")]
    public async Task RestNotificationsEnabled_WithMissingOrFalseSetting_ShouldDefaultFalse(string? setting)
    {
        // Arrange
        var settings = new Dictionary<string, string>();
        if (setting is not null) settings["training-rest-notifications-enabled"] = setting;
        var service = Create(new Repository { Stored = new PerformanceData { Settings = settings } });

        // Act
        await service.InitializeAsync();

        // Assert
        service.RestNotificationsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task SetRestNotificationsEnabledAsync_WhenEnabledThenDisabled_ShouldPersistChoiceAcrossReopen()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.SetRestNotificationsEnabledAsync(true);
        var reopened = Create(repository);
        await reopened.InitializeAsync();
        reopened.RestNotificationsEnabled.ShouldBeTrue();

        // Act
        await reopened.SetRestNotificationsEnabledAsync(false);

        // Assert
        reopened.RestNotificationsEnabled.ShouldBeFalse();
        repository.Stored.Settings["training-rest-notifications-enabled"].ShouldBe("False");
    }

    [Fact]
    public async Task SetRestNotificationsEnabledAsync_WhenWriteFails_ShouldRetainChoice()
    {
        // Arrange
        var repository = new Repository();
        var service = Create(repository);
        await service.SetRestNotificationsEnabledAsync(true);
        repository.FailWrites = true;

        // Act
        var result = await service.SetRestNotificationsEnabledAsync(false);

        // Assert
        result.Error.ShouldBe("PersistenceFailed");
        service.RestNotificationsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task CompleteRestAsync_WithCapturedDeadline_ShouldRejectStoppedRest()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = Create(new Repository(), clock);
        await service.StartAsync(Session, "One Air");
        await service.StartRestAsync("press");
        var deadline = service.Data.ActiveWorkout!.RestEnd!.Value;
        await service.StopRestAsync();
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await service.CompleteRestAsync(deadline);

        // Assert
        result.Error.ShouldBe("StaleRest");
    }

    [Fact]
    public async Task CompleteRestAsync_WithCapturedDeadline_ShouldRejectExtendedRest()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var service = Create(new Repository(), clock);
        await service.StartAsync(Session, "One Air");
        await service.StartRestAsync("press");
        var deadline = service.Data.ActiveWorkout!.RestEnd!.Value;
        await service.ExtendRestAsync();
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await service.CompleteRestAsync(deadline);

        // Assert
        result.Error.ShouldBe("StaleRest");
        service.Data.ActiveWorkout.RestEnd.ShouldBe(deadline.AddSeconds(30));
    }

    [Fact]
    public async Task CompleteRestAsync_WithUnexpiredMatchingDeadline_ShouldRejectPrematureCallback()
    {
        // Arrange
        var service = Create(new Repository());
        await service.StartAsync(Session, "One Air");
        await service.StartRestAsync("press");

        // Act
        var result = await service.CompleteRestAsync(service.Data.ActiveWorkout!.RestEnd!.Value);

        // Assert
        result.Error.ShouldBe("StaleRest");
    }

    [Fact]
    public async Task CompleteRestAsync_WithConcurrentMatchingDeadline_ShouldClearExactlyOnce()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var repository = new Repository();
        var service = Create(repository, clock);
        await service.StartAsync(Session, "One Air");
        await service.StartRestAsync("press");
        var deadline = service.Data.ActiveWorkout!.RestEnd!.Value;
        clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var results = await Task.WhenAll(service.CompleteRestAsync(deadline), service.CompleteRestAsync(deadline));

        // Assert
        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => result.Error == "StaleRest").ShouldBe(1);
        repository.Writes.ShouldBe(3);
        service.Data.ActiveWorkout.RestEnd.ShouldBeNull();
    }

    private static readonly WorkoutSession Session = new() { Id = "upper", Name = "Upper", Exercises = [new WorkoutExercise { ExerciseId = "press" }] };
    private static WorkoutStateService Create(Repository repository, FakeTimeProvider? clock = null) => new(repository, new Catalog(), clock ?? new FakeTimeProvider());
    private sealed class Catalog : ITrainingCatalog
    {
        public IReadOnlyList<Exercise> Exercises => [new Exercise { Id = "press", Name = "Press" }];
        public IReadOnlyList<WorkoutProgram> Programs => [];
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Stored { get; set; } = new();
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            if (FailWrites) throw new IOException("Write failed");
            Stored = data;
            Writes++;
            return ValueTask.CompletedTask;
        }
    }
}
