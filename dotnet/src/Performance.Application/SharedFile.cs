using System.Text.Json;

namespace Performance.Application;

public abstract record SharedFile;

public sealed record SharedSession(SessionPackage Package) : SharedFile;

public sealed record SharedPrograms(ProgramPackage Package) : SharedFile;

public sealed class ReadSharedFileUseCase(IProgramPackageCodec programs, ISessionPackageCodec sessions)
{
    private const string Incompatible = "Ce fichier n'est pas un partage Performance compatible.";

    public SharedFile Execute(string json) => Kind(json) switch
    {
        SessionPackage.DocumentKind => new SharedSession(sessions.Decode(json)),
        ProgramPackage.DocumentKind => new SharedPrograms(programs.Decode(json)),
        _ => throw new FormatException(Incompatible)
    };

    private static string? Kind(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("kind", out var kind)) return null;
            return kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
