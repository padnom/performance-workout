using System.Diagnostics;
using Performance.Application;

namespace Performance.App.Services;

public sealed class DiagnosticPerformanceDataRepository(IPerformanceDataRepository inner, IAppDiagnostics diagnostics)
    : IPerformanceDataRepository
{
    public async ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try { var value = await inner.LoadAsync(cancellationToken); diagnostics.Record("data.load", Stopwatch.GetElapsedTime(started)); return value; }
        catch (Exception exception) { diagnostics.Record("data.load", Stopwatch.GetElapsedTime(started), exception); throw; }
    }
    public async ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try { await inner.SaveAsync(data, cancellationToken); diagnostics.Record("data.save", Stopwatch.GetElapsedTime(started)); }
        catch (Exception exception) { diagnostics.Record("data.save", Stopwatch.GetElapsedTime(started), exception); throw; }
    }
}
