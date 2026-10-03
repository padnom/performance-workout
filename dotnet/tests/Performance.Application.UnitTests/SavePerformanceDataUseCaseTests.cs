using JetBrains.Annotations;
using Performance.Application;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(SavePerformanceDataUseCase))]
public sealed class SavePerformanceDataUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithAggregate_ShouldDelegateSameInstanceToRepository()
    {
        // Arrange
        var repository = new FakeRepository();
        var data = new PerformanceData();

        // Act
        await new SavePerformanceDataUseCase(repository).ExecuteAsync(data);

        // Assert
        repository.Saved.ShouldBeSameAs(data);
    }

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        public PerformanceData? Saved { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new PerformanceData());
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { Saved = data; return ValueTask.CompletedTask; }
    }
}
