using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace Performance.App.Services;

public sealed class MauiAppDiagnostics : IAppDiagnostics
{
    private readonly DiagnosticJournal _journal;
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "performance-diagnostics.json");
    public string Version => $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public MauiAppDiagnostics(TimeProvider clock)
    {
        _journal = new(clock);
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length <= 128_000)
                _journal.Restore(JsonSerializer.Deserialize<DiagnosticEntry[]>(File.ReadAllText(_path)) ?? []);
        }
        catch (Exception exception) { _journal.Record("diagnostics.load", TimeSpan.Zero, exception); }
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Record("runtime.unhandled", TimeSpan.Zero, args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => Record("runtime.unobserved", TimeSpan.Zero, args.Exception);
    }

    public void Record(string operation, TimeSpan elapsed, Exception? exception = null)
    {
        _journal.Record(operation, elapsed, exception);
        if (exception is not null) Flush();
    }

    public void Flush()
    {
        lock (_gate)
        {
            try { File.WriteAllText(_path, JsonSerializer.Serialize(_journal.Snapshot())); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public async Task ShareAsync(CancellationToken cancellationToken = default)
    {
        Flush();
        var json = JsonSerializer.Serialize(new
        {
            ApplicationVersion = Version,
            Platform = DeviceInfo.Current.Platform.ToString(),
            OsVersion = DeviceInfo.Current.VersionString,
            Entries = _journal.Snapshot()
        }, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(FileSystem.CacheDirectory, "performance-diagnostics.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Diagnostics Performance", File = new ShareFile(path) });
    }
}
