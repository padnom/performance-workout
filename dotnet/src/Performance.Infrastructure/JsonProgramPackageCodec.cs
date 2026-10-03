using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Performance.Application;

namespace Performance.Infrastructure;

public sealed class JsonProgramPackageCodec : IProgramPackageCodec
{
    public const int MaximumBytes = 1024 * 1024;

    public ProgramPackage Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new FormatException("Le fichier de programmes est vide ou dépasse 1 Mo.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("kind", out var kind)
                || kind.ValueKind != JsonValueKind.String || kind.GetString() != ProgramPackage.DocumentKind
                || !root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number) || number != 1)
                throw new FormatException("Ce fichier n'est pas un partage de programmes compatible.");
            var package = JsonSerializer.Deserialize(json, ProgramPackageJsonContext.Default.ProgramPackage)
                ?? throw new FormatException("Fichier de programmes invalide.");
            ProgramPackageValidation.Validate(package);
            return package;
        }
        catch (JsonException exception)
        {
            throw new FormatException("Fichier de programmes invalide. Aucune donnée n'a été modifiée.", exception);
        }
    }

    public string Encode(ProgramPackage package)
    {
        ProgramPackageValidation.Validate(package);
        var json = JsonSerializer.Serialize(package, ProgramPackageJsonContext.Default.ProgramPackage);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new FormatException("Le partage de programmes dépasse 1 Mo.");
        return json;
    }
}

[JsonSerializable(typeof(ProgramPackage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
internal sealed partial class ProgramPackageJsonContext : JsonSerializerContext;
