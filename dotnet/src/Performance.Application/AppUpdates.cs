using System.Text.Json;

namespace Performance.Application;

/// <summary>A published Android build that can replace the installed application.</summary>
public sealed record AppRelease(Version Version, Uri ApkUri, string? Sha256, long Size, string Notes);

/// <summary>Reads the GitHub "latest release" document published by the Android distribution workflow.</summary>
public static class AppReleaseParser
{
    public const long MaxApkBytes = 250_000_000;
    private static readonly string[] TrustedHosts = ["github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    public static AppRelease? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Flag(root, "draft") || Flag(root, "prerelease")) return null;
            var version = ParseVersion(Text(root, "tag_name"));
            var asset = FindApk(root);
            if (version is null || asset is null) return null;
            var notes = Text(root, "body") ?? "";
            return asset.Value.Size is <= 0 or > MaxApkBytes ? null
                : new AppRelease(version, asset.Value.Uri, asset.Value.Sha256, asset.Value.Size, notes.Length > 2000 ? notes[..2000] : notes);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Accepts tags such as <c>android-v1.4.0</c>, <c>v1.4.0</c> or <c>1.4</c>.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim();
        if (text.StartsWith("android-", StringComparison.OrdinalIgnoreCase)) text = text["android-".Length..];
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        if (!Version.TryParse(text, out var version) || version.Revision > 0) return null;
        return Normalize(version);
    }

    public static bool IsNewer(AppRelease release, string? installedVersion)
    {
        var installed = ParseVersion(installedVersion);
        return installed is null || release.Version > installed;
    }

    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    private static (Uri Uri, string? Sha256, long Size)? FindApk(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = Text(asset, "name");
            if (name is null || !name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Uri.TryCreate(Text(asset, "browser_download_url"), UriKind.Absolute, out var uri) || !IsTrusted(uri)) continue;
            var size = asset.TryGetProperty("size", out var value) && value.TryGetInt64(out var bytes) ? bytes : 0;
            return (uri, Sha256(Text(asset, "digest")), size);
        }
        return null;
    }

    private static bool IsTrusted(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && TrustedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    private static string? Sha256(string? digest)
    {
        const string prefix = "sha256:";
        if (digest is null || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var hex = digest[prefix.Length..];
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex.ToLowerInvariant() : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
