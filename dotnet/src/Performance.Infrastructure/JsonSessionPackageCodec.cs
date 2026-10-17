using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Performance.Application;

namespace Performance.Infrastructure;

public sealed class JsonSessionPackageCodec : ISessionPackageCodec
{
    public const int MaximumBytes = 1024 * 1024;

    public SessionPackage Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new FormatException("Le fichier de séance est vide ou dépasse 1 Mo.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("kind", out var kind)
                || kind.ValueKind != JsonValueKind.String || kind.GetString() != SessionPackage.DocumentKind
                || !root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number) || number != 1)
                throw new FormatException("Ce fichier n'est pas un partage de séance compatible.");
            var package = JsonSerializer.Deserialize(json, SessionPackageJsonContext.Default.SessionPackage)
                ?? throw new FormatException("Fichier de séance invalide.");
            SessionPackageValidation.Validate(package);
            return package;
        }
        catch (JsonException exception)
        {
            throw new FormatException("Fichier de séance invalide. Aucune donnée n'a été modifiée.", exception);
        }
    }

    public string Encode(SessionPackage package)
    {
        SessionPackageValidation.Validate(package);
        var json = JsonSerializer.Serialize(package, SessionPackageJsonContext.Default.SessionPackage);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new FormatException("Le partage de séance dépasse 1 Mo.");
        return json;
    }
}

[JsonSerializable(typeof(SessionPackage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
internal sealed partial class SessionPackageJsonContext : JsonSerializerContext;
