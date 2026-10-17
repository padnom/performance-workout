namespace Performance.App.Services;

public interface IProgramFiles
{
    Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default);
    Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default);
    Task<string?> PickSessionAsync(CancellationToken cancellationToken = default);
    Task ShareSessionAsync(string json, string name, CancellationToken cancellationToken = default);
    Task ShareTextAsync(string text, string title, CancellationToken cancellationToken = default);
}
