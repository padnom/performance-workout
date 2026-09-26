using Performance.Application;

namespace Performance.Application.Tests;

public sealed class UseCaseTests
{
    [Fact]
    public async Task Save_use_case_delegates_the_aggregate_to_repository()
    {
        var repository = new FakeRepository();
        var data = new PerformanceData();
        await new SavePerformanceDataUseCase(repository).ExecuteAsync(data);
        Assert.Same(data, repository.Saved);
    }

    private sealed class FakeRepository : IPerformanceDataRepository
    {
        public PerformanceData? Saved { get; private set; }
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new PerformanceData());
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { Saved = data; return ValueTask.CompletedTask; }
    }
}
