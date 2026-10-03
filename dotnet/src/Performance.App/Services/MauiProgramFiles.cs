using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Performance.App.Services;

public sealed class MauiProgramFiles : IProgramFiles
{
    public async Task<string?> PickProgramsAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choisir des programmes Performance (.json)" });
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        const int maximumCharacters = 4_000_000;
        var buffer = new char[8192];
        var content = new System.Text.StringBuilder();
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) is var count && count > 0)
        {
            if (content.Length + count > maximumCharacters) throw new FormatException("Ce fichier est trop volumineux pour des programmes.");
            content.Append(buffer, 0, count);
        }
        return content.ToString();
    }

    public async Task ShareProgramsAsync(string json, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"performance-programs-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = "Partager mes programmes Performance", File = new ShareFile(path) });
    }
}
