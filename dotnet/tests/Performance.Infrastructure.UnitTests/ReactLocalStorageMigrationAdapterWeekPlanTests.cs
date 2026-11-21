using JetBrains.Annotations;
using Performance.Application;
using Performance.Domain;
using Shouldly;
using System.Text.Json;

namespace Performance.Infrastructure.UnitTests;

[TestSubject(typeof(ReactLocalStorageMigrationAdapter))]
public sealed class ReactLocalStorageMigrationAdapterWeekPlanTests
{
    [Fact]
    public async Task Export_WithModifiedImportedWeek_ShouldEmitCurrentOrderIncludingDuplicates()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var initial = adapter.Import(new Dictionary<string, string?> { [ReactLocalStorageMigrationAdapter.WeekPlanKey] = "[\"pa\",\"pa\",\"pua\"]", ["future-key"] = "opaque" });
        var service = Create(new MemoryRepository(initial));

        // Act
        await service.MoveWeekSessionAsync(2, -1);
        var output = adapter.Export(service.Data);
        var restored = adapter.Import(output.ToDictionary(pair => pair.Key, pair => (string?)pair.Value));

        // Assert
        JsonSerializer.Deserialize<string[]>(output[ReactLocalStorageMigrationAdapter.WeekPlanKey]).ShouldBe(["pa", "pua", "pa"]);
        restored.Programs.Single().Sessions.Select(session => session.Id).ShouldBe(["pa", "pua", "pa"]);
        restored.Programs[0].Sessions[0].Name.ShouldBe("Push · A · Barre & incliné");
        output["future-key"].ShouldBe("opaque");
    }

    [Fact]
    public async Task Export_WithWeekCleared_ShouldOverwriteOldRawWeekWithEmptyArray()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var initial = adapter.Import(new Dictionary<string, string?> { [ReactLocalStorageMigrationAdapter.WeekPlanKey] = "[\"pa\"]" });
        var service = Create(new MemoryRepository(initial));

        // Act
        await service.RemoveWeekSessionAsync(0);
        var output = adapter.Export(service.Data);

        // Assert
        output[ReactLocalStorageMigrationAdapter.WeekPlanKey].ShouldBe("[]");
        service.WeekSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Export_WithNewAndroidWeek_ShouldIncludeReactCompatibleIds()
    {
        // Arrange
        var adapter = new ReactLocalStorageMigrationAdapter();
        var service = Create(new MemoryRepository(new PerformanceData()));

        // Act
        await service.AddWeekSessionAsync("pa");
        await service.AddWeekSessionAsync("pa");
        var output = adapter.Export(service.Data);

        // Assert
        JsonSerializer.Deserialize<string[]>(output[ReactLocalStorageMigrationAdapter.WeekPlanKey]).ShouldBe(["pa", "pa"]);
    }

    private static WorkoutStateService Create(IPerformanceDataRepository repository) => new(repository, new DefaultTrainingCatalog(), TimeProvider.System);

    private sealed class MemoryRepository(PerformanceData data) : IPerformanceDataRepository
    {
        private PerformanceData _stored = data;
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_stored);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
        {
            _stored = data;
            return ValueTask.CompletedTask;
        }
    }
}
