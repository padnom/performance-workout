using System.Text;
using static Performance.App.Resources.Text;

namespace Performance.App.Services;

public static class BoundedTextReader
{
    public static async Task<string> ReadAsync(Stream stream, int maximumCharacters, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var content = new StringBuilder();
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) is var count && count > 0)
        {
            if (content.Length + count > maximumCharacters) throw new FormatException(T("Service.BoundedTextReader.TooLarge"));
            content.Append(buffer, 0, count);
        }
        return content.ToString();
    }
}
