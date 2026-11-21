using Microsoft.Maui.ApplicationModel.DataTransfer;
using static Performance.App.Resources.Text;

namespace Performance.App.Services;

public sealed class MauiProgramFiles : IProgramFiles
{
    public async Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = T("Service.MauiProgramFiles.PickerTitle") });
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        try { return await BoundedTextReader.ReadAsync(stream, 4_000_000, cancellationToken); }
        catch (FormatException) { throw new FormatException(T("Service.MauiProgramFiles.TooLarge")); }
    }

    public async Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"performance-programs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = T("Service.MauiProgramFiles.ShareTitle"), File = new ShareFile(path) });
    }

    public async Task<string?> PickSessionAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = T("Service.MauiProgramFiles.SessionPickerTitle") });
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        return await BoundedTextReader.ReadAsync(stream, 1_100_000, cancellationToken);
    }

    public async Task ShareSessionAsync(string json, string name, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, ShareFileName.ForSession(name, DateTime.UtcNow));
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = T("Service.MauiProgramFiles.SessionShareTitle"), File = new ShareFile(path) });
    }

    public Task ShareTextAsync(string text, string title, CancellationToken cancellationToken = default) =>
        Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = text });
}
