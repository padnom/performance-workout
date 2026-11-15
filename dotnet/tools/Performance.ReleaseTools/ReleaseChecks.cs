using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Performance.ReleaseTools;

public sealed record ProjectVersion(string Display, int Code)
{
    public override string ToString() => $"{Display}/{Code}";
}

/// <summary>Release checks shared by the CI commands and their regression tests.</summary>
public static partial class ReleaseChecks
{
    public const string Project = "dotnet/src/Performance.App/Performance.App.csproj";
    public const string ApplicationId = "fr.padnom.performance";
    public static readonly IReadOnlySet<string> Architectures = new HashSet<string> { "arm64-v8a", "x86_64" };
    public static readonly IReadOnlyList<string> Assemblies = ["App", "Application", "Domain", "Infrastructure"];
    /// <summary>First bytes of every Android native library (0x7F followed by "ELF").</summary>
    public static ReadOnlySpan<byte> ElfMagic => [0x7F, 0x45, 0x4C, 0x46];
    public static readonly IReadOnlyList<string> WebPayload =
        ["assets/wwwroot/index.html", "assets/wwwroot/css/app.css", "assets/wwwroot/_framework/blazor.webview.js"];

    public static ProjectVersion ParseProjectVersion(string xml)
    {
        var root = XDocument.Parse(xml).Root!;
        string? Find(string name) => root.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
        var display = Find("ApplicationDisplayVersion");
        var code = Find("ApplicationVersion");
        if (display is null || !DisplayVersionPattern().IsMatch(display))
            throw new ReleaseCheckException("ApplicationDisplayVersion must be a numeric major.minor[.patch].");
        if (code is null || !code.All(char.IsAsciiDigit) || !long.TryParse(code, out var number) || number is <= 0 or > 2_100_000_000)
            throw new ReleaseCheckException("ApplicationVersion must be a valid positive Android version code.");
        return new ProjectVersion(display, (int)number);
    }

    public static void CheckVersion(ProjectVersion current, ProjectVersion previous, bool requireIncrease = false)
    {
        var comparison = CompareDisplay(current.Display, previous.Display);
        if (comparison < 0 || current.Code < previous.Code)
            throw new ReleaseCheckException("Release version cannot move backwards.");
        if (comparison != 0 && current.Code <= previous.Code)
            throw new ReleaseCheckException("Changing the display version requires an increased Android version code.");
        if (requireIncrease && current.Code <= previous.Code)
            throw new ReleaseCheckException("A distributable release requires an increased Android version code.");
    }

    private static int CompareDisplay(string left, string right)
    {
        static long[] Parts(string display) => [.. display.Split('.').Select(long.Parse).Concat(Enumerable.Repeat(0L, 3)).Take(3)];
        var (a, b) = (Parts(left), Parts(right));
        for (var i = 0; i < 3; i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        return 0;
    }

    public static void VerifyPayload(Stream apk)
    {
        using var package = OpenApk(apk);
        var entries = package.Entries.ToDictionary(entry => entry.FullName);
        var architectures = entries.Keys.Select(name => name.Split('/'))
            .Where(parts => parts[0] == "lib" && parts.Length >= 3).Select(parts => parts[1]).ToHashSet();
        if (!architectures.SetEquals(Architectures))
            throw new ReleaseCheckException($"Expected arm64-v8a and x86_64; found [{string.Join(", ", architectures.Order())}].");
        var required = new HashSet<string>(WebPayload);
        foreach (var arch in Architectures)
        {
            required.Add($"lib/{arch}/libassembly-store.so");
            foreach (var assembly in Assemblies) required.Add($"lib/{arch}/libaot-Performance.{assembly}.dll.so");
        }
        var missing = required.Where(entry => !entries.ContainsKey(entry)).Order().ToList();
        if (missing.Count > 0)
            throw new ReleaseCheckException("Release APK missing standalone/AOT payload: " + string.Join(", ", missing));
        foreach (var name in required)
        {
            if (entries[name].Length == 0) throw new ReleaseCheckException($"Empty APK payload: {name}");
            if (name.EndsWith(".so", StringComparison.Ordinal) && !StartsWithElf(entries[name]))
                throw new ReleaseCheckException($"Invalid Android native library: {name}");
        }
        VerifyIntegrity(package);
    }

    /// <summary>Checks a Debug build still bundles the app assemblies and the Blazor UI (no fast deployment).</summary>
    public static IReadOnlyList<string> VerifyDebugPayload(Stream apk)
    {
        using var package = OpenApk(apk);
        var entries = package.Entries.Select(entry => entry.FullName).ToHashSet();
        var architectures = entries.Where(name => name.StartsWith("lib/", StringComparison.Ordinal) && name.Split('/').Length >= 3)
            .Select(name => name.Split('/')[1]).Distinct().Order().ToList();
        if (architectures.Count == 0) throw new ReleaseCheckException("APK has no Android runtime libraries.");
        var required = new HashSet<string>(WebPayload);
        foreach (var arch in architectures)
            foreach (var assembly in Assemblies) required.Add($"lib/{arch}/lib_Performance.{assembly}.dll.so");
        var missing = required.Where(entry => !entries.Contains(entry)).Order().ToList();
        if (missing.Count > 0)
            throw new ReleaseCheckException("APK is not standalone; missing payload:\n" + string.Join('\n', missing));
        VerifyIntegrity(package);
        return architectures;
    }

    public static void VerifyManifest(string badging, ProjectVersion expected)
    {
        var line = badging.Split('\n').FirstOrDefault(text => text.StartsWith("package:", StringComparison.Ordinal)) ?? "";
        var fields = BadgingField().Matches(line).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        if (fields.GetValueOrDefault("name") != ApplicationId)
            throw new ReleaseCheckException("Unexpected APK application ID.");
        if (fields.GetValueOrDefault("versionName") != expected.Display || fields.GetValueOrDefault("versionCode") != expected.Code.ToString())
            throw new ReleaseCheckException("APK version differs from the project version.");
        if (badging.Contains("application-debuggable", StringComparison.Ordinal))
            throw new ReleaseCheckException("Release APK must not be debuggable.");
    }

    public static void VerifyCertificate(string signingOutput, string expectedCertificate)
    {
        if (!Sha256Pattern().IsMatch(expectedCertificate))
            throw new ReleaseCheckException("Expected signing certificate must be a SHA-256 digest.");
        var certificates = CertificateDigest().Matches(signingOutput).Select(match => match.Groups[1].Value.ToLowerInvariant()).ToList();
        if (certificates.Count != 1 || certificates[0] != expectedCertificate.ToLowerInvariant())
            throw new ReleaseCheckException("Signing certificate differs from the existing installation; update would be rejected.");
    }

    public static void VerifyPackage(string path, ProjectVersion expected, string apksigner, string aapt, string? expectedCertificate, IProcessRunner runner)
    {
        using (var apk = File.OpenRead(path)) VerifyPayload(apk);
        var signature = runner.RunChecked(apksigner, "verify", "--verbose", "--print-certs", path);
        if (expectedCertificate is not null) VerifyCertificate(signature.Output, expectedCertificate);
        VerifyManifest(runner.RunChecked(aapt, "dump", "badging", path).Output, expected);
    }

    private static ZipArchive OpenApk(Stream apk)
    {
        try { return new ZipArchive(apk, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException error) { throw new ReleaseCheckException($"Not a valid APK archive: {error.Message}"); }
    }

    private static bool StartsWithElf(ZipArchiveEntry entry)
    {
        using var library = entry.Open();
        Span<byte> header = stackalloc byte[4];
        return library.ReadAtLeast(header, 4, throwOnEndOfStream: false) == 4 && header.SequenceEqual(ElfMagic);
    }

    /// <summary>Reads every entry so damaged data or CRC mismatches are reported before distribution.</summary>
    private static void VerifyIntegrity(ZipArchive package)
    {
        foreach (var entry in package.Entries)
        {
            try
            {
                using var content = entry.Open();
                content.CopyTo(Stream.Null);
            }
            catch (InvalidDataException) { throw new ReleaseCheckException($"Damaged APK entry: {entry.FullName}"); }
        }
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+(\.[0-9]+)?\z")] private static partial Regex DisplayVersionPattern();
    [GeneratedRegex(@"(\w+)='([^']*)'")] private static partial Regex BadgingField();
    [GeneratedRegex(@"^[0-9a-fA-F]{64}\z")] private static partial Regex Sha256Pattern();
    [GeneratedRegex("certificate SHA-256 digest: ([0-9a-fA-F]{64})")] private static partial Regex CertificateDigest();
}
