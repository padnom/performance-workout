namespace Performance.App.Services;

public interface IProgramFiles
{
    Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default);
    Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default);
}
