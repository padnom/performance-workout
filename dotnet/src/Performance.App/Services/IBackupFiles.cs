namespace Performance.App.Services;

public interface IBackupFiles
{
    Task<string?> PickBackupAsync(CancellationToken cancellationToken = default);
    Task ShareBackupAsync(string json, CancellationToken cancellationToken = default);
}
