using System.Text.Json;
using System.Text.Json.Serialization;
using Performance.Application;

namespace Performance.Infrastructure;

public static class PersistenceJson
{
    public static JsonSerializerOptions Options() => new(System.Text.Json.JsonSerializerDefaults.Web)
    { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNameCaseInsensitive = true };
}

/// <summary>Atomic JSON-file document adapter. Pass a path under the platform app-data directory.</summary>
public sealed class JsonFileDocumentStore(string filePath) : IJsonDocumentStore
{
    public string FilePath { get; } = Path.GetFullPath(filePath ?? throw new ArgumentNullException(nameof(filePath)));
    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) =>
        File.Exists(FilePath) ? await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false) : null;

    public async ValueTask WriteAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, json, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, FilePath, overwrite: true);
    }
}

public sealed class JsonPerformanceDataRepository(IJsonDocumentStore store) : IPerformanceDataRepository
{
    public async ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return new PerformanceData();
        var value = JsonSerializer.Deserialize<PerformanceData>(json, PersistenceJson.Options());
        return value is null ? new PerformanceData() : value with { SchemaVersion = 1 };
    }

    public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) =>
        store.WriteAsync(JsonSerializer.Serialize(data with { SchemaVersion = 1, ExportedAt = DateTimeOffset.UtcNow }, PersistenceJson.Options()), cancellationToken);
}
