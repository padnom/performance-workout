namespace Performance.App.Services;

public interface IAppDiagnostics
{
    string Version { get; }
    void Record(string operation, TimeSpan elapsed, Exception? exception = null);
    Task ShareAsync(CancellationToken cancellationToken = default);
}
