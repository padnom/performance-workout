namespace Performance.Application;

public interface IJsonDocumentStore
{
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string json, CancellationToken cancellationToken = default);
}

public interface IPerformanceDataRepository
{
    ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default);
    ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default);
}

public interface IReactLocalStorageAdapter
{
    PerformanceData Import(IReadOnlyDictionary<string, string?> localStorage);
    IReadOnlyDictionary<string, string> Export(PerformanceData data);
    PerformanceData ImportBackup(string json);
    string ExportBackup(PerformanceData data);
}

public sealed class LoadPerformanceDataUseCase(IPerformanceDataRepository repository)
{
    public ValueTask<PerformanceData> ExecuteAsync(CancellationToken cancellationToken = default) => repository.LoadAsync(cancellationToken);
}

public sealed class SavePerformanceDataUseCase(IPerformanceDataRepository repository)
{
    public ValueTask ExecuteAsync(PerformanceData data, CancellationToken cancellationToken = default) => repository.SaveAsync(data, cancellationToken);
}

public sealed class ImportReactLocalStorageUseCase(IReactLocalStorageAdapter adapter)
{
    public PerformanceData Execute(IReadOnlyDictionary<string, string?> localStorage) => adapter.Import(localStorage);
}

public sealed class ExportReactLocalStorageUseCase(IReactLocalStorageAdapter adapter)
{
    public IReadOnlyDictionary<string, string> Execute(PerformanceData data) => adapter.Export(data);
}

public sealed class ImportPerformanceBackupUseCase(IReactLocalStorageAdapter adapter)
{
    public PerformanceData Execute(string json) => adapter.ImportBackup(json);
}

public sealed class ExportPerformanceBackupUseCase(IReactLocalStorageAdapter adapter)
{
    public string Execute(PerformanceData data) => adapter.ExportBackup(data);
}
