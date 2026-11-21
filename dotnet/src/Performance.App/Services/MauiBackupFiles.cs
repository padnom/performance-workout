using Microsoft.Maui.ApplicationModel.DataTransfer;
using static Performance.App.Resources.Text;

namespace Performance.App.Services;

public sealed class MauiBackupFiles : IBackupFiles
{
    public async Task<string?> PickBackupAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = T("Service.MauiBackupFiles.PickerTitle") });
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task ShareBackupAsync(string json, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"performance-backup-{DateTime.UtcNow:yyyy-MM-dd}.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = T("Service.MauiBackupFiles.ShareTitle"),
            File = new ShareFile(path)
        });
    }
}
