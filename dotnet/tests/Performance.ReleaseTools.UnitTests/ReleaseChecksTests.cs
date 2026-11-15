using System.IO.Compression;
using Performance.ReleaseTools;
using Shouldly;
using Xunit;

namespace Performance.ReleaseTools.UnitTests;

public sealed class ReleaseChecksTests
{
    private static readonly ProjectVersion V110 = new("1.1.0", 2);
    private static readonly byte[] ElfLibrary = [0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00];

    private static MemoryStream Apk(string? omit = null, string? invalidLibrary = null, string? extraArchitecture = null)
    {
        var stream = new MemoryStream();
        using (var package = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Write(string name, byte[] content)
            {
                using var entry = package.CreateEntry(name).Open();
                entry.Write(content);
            }
            foreach (var entry in ReleaseChecks.WebPayload.Where(entry => entry != omit)) Write(entry, "<html>offline</html>"u8.ToArray());
            foreach (var arch in ReleaseChecks.Architectures)
            {
                var libraries = ReleaseChecks.Assemblies.Select(assembly => $"lib/{arch}/libaot-Performance.{assembly}.dll.so").Prepend($"lib/{arch}/libassembly-store.so");
                foreach (var entry in libraries.Where(entry => entry != omit))
                    Write(entry, entry == invalidLibrary ? "broken"u8.ToArray() : ElfLibrary);
            }
            if (extraArchitecture is not null) Write($"lib/{extraArchitecture}/libassembly-store.so", ElfLibrary);
        }
        stream.Position = 0;
        return stream;
    }

    public static TheoryData<string> AotLibraries() =>
        [.. ReleaseChecks.Architectures.SelectMany(arch => ReleaseChecks.Assemblies.Select(assembly => $"lib/{arch}/libaot-Performance.{assembly}.dll.so"))];

    [Fact]
    public void Complete_release_payload_is_accepted() => ReleaseChecks.VerifyPayload(Apk());

    [Theory, MemberData(nameof(AotLibraries))]
    public void Both_architectures_and_all_aot_assemblies_are_required(string library) =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPayload(Apk(omit: library)));

    [Theory]
    [InlineData("assets/wwwroot/index.html")]
    [InlineData("assets/wwwroot/css/app.css")]
    [InlineData("assets/wwwroot/_framework/blazor.webview.js")]
    [InlineData("lib/x86_64/libassembly-store.so")]
    public void Missing_webview_or_store_is_not_a_standalone_release(string entry) =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPayload(Apk(omit: entry)));

    [Fact]
    public void Named_but_invalid_native_payload_is_rejected() =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPayload(Apk(invalidLibrary: "lib/arm64-v8a/libassembly-store.so")))
            .Message.ShouldContain("Invalid Android native");

    [Fact]
    public void Wrong_or_extra_abi_is_rejected() =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPayload(Apk(extraArchitecture: "armeabi-v7a")))
            .Message.ShouldContain("Expected arm64");

    [Fact]
    public void Non_zip_file_is_rejected() =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPayload(new MemoryStream("not an apk"u8.ToArray())));

    [Fact]
    public void Unsigned_apk_is_rejected_by_external_signature_check()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, Apk().ToArray());
            var runner = new FakeRunner(_ => new ProcessResult(1, "", "DOES NOT VERIFY"));
            Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyPackage(path, V110, "apksigner", "aapt", null, runner))
                .Message.ShouldContain("apksigner");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Signed_package_with_expected_certificate_and_manifest_is_accepted()
    {
        var path = Path.GetTempFileName();
        var digest = new string('a', 64);
        try
        {
            File.WriteAllBytes(path, Apk().ToArray());
            var runner = new FakeRunner(call => call.FileName == "apksigner"
                ? new ProcessResult(0, $"Signer #1 certificate SHA-256 digest: {digest}\n", "")
                : new ProcessResult(0, "package: name='fr.padnom.performance' versionCode='2' versionName='1.1.0'\n", ""));
            ReleaseChecks.VerifyPackage(path, V110, "apksigner", "aapt", digest.ToUpperInvariant(), runner);
            runner.Calls.Select(call => call.FileName).ShouldBe(["apksigner", "aapt"]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Manifest_version_id_and_debug_flag()
    {
        const string manifest = "package: name='fr.padnom.performance' versionCode='2' versionName='1.1.0'\n";
        ReleaseChecks.VerifyManifest(manifest, V110);
        foreach (var wrong in new[] { manifest.Replace("versionCode='2'", "versionCode='1'"),
                     manifest.Replace("fr.padnom.performance", "another.app"), manifest + "application-debuggable\n" })
            Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyManifest(wrong, V110));
    }

    [Fact]
    public void Other_or_multiple_signers_cannot_produce_a_compatible_update()
    {
        var digest = new string('a', 64);
        ReleaseChecks.VerifyCertificate("Signer #1 certificate SHA-256 digest: " + digest, digest);
        foreach (var output in new[] { "", "certificate SHA-256 digest: " + new string('b', 64),
                     string.Concat(Enumerable.Repeat("certificate SHA-256 digest: " + digest + "\n", 2)) })
            Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyCertificate(output, digest));
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyCertificate("certificate SHA-256 digest: " + digest, "not-a-digest"));
    }

    [Fact]
    public void Initial_and_repeated_verification_but_not_repeated_distribution()
    {
        ReleaseChecks.CheckVersion(V110, new("1.0", 1), requireIncrease: true);
        ReleaseChecks.CheckVersion(V110, V110);
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.CheckVersion(V110, V110, requireIncrease: true));
    }

    [Theory]
    [InlineData("1.0", 3)]
    [InlineData("1.1.0", 1)]
    [InlineData("1.2.0", 2)]
    public void Version_regression_and_changed_label_without_code_increase(string display, int code) =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.CheckVersion(new(display, code), V110));

    [Fact]
    public void Numeric_versions_compare_numerically()
    {
        ReleaseChecks.CheckVersion(new("1.10.0", 4), new("1.9.0", 3), requireIncrease: true);
        ReleaseChecks.CheckVersion(V110, new("1.1", 2));
    }

    [Fact]
    public void Project_version_validation()
    {
        const string xml = "<Project><PropertyGroup><ApplicationDisplayVersion>1.1.0</ApplicationDisplayVersion><ApplicationVersion>2</ApplicationVersion></PropertyGroup></Project>";
        ReleaseChecks.ParseProjectVersion(xml).ShouldBe(V110);
        foreach (var malformed in new[] { xml.Replace("1.1.0", "latest"), xml.Replace(">2<", ">0<"), xml.Replace("1.1.0", "1.1.0\n"), "<Project/>" })
            Should.Throw<ReleaseCheckException>(() => ReleaseChecks.ParseProjectVersion(malformed));
    }

    [Fact]
    public void Tracked_project_declares_a_valid_version() =>
        ReleaseChecks.ParseProjectVersion(File.ReadAllText(Path.Combine(RepositoryRoot(), ReleaseChecks.Project))).Code.ShouldBeGreaterThan(0);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ReleaseChecks.Project))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

public sealed class DebugPayloadTests
{
    private static MemoryStream Apk(params string[] omit)
    {
        var stream = new MemoryStream();
        using (var package = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = ReleaseChecks.WebPayload.Concat(ReleaseChecks.Assemblies.Select(assembly => $"lib/x86_64/lib_Performance.{assembly}.dll.so"));
            foreach (var name in entries.Except(omit))
            {
                using var entry = package.CreateEntry(name).Open();
                entry.Write("payload"u8);
            }
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Bundled_debug_payload_is_accepted() => ReleaseChecks.VerifyDebugPayload(Apk()).ShouldBe(["x86_64"]);

    [Fact]
    public void Fast_deployment_debug_apk_is_rejected() =>
        Should.Throw<ReleaseCheckException>(() => ReleaseChecks.VerifyDebugPayload(Apk("lib/x86_64/lib_Performance.App.dll.so")));
}

public sealed class AndroidSmokeTests
{
    [Fact]
    public void Dashboard_requires_package_and_home_content()
    {
        AndroidSmoke.IsDashboard("package=\"fr.padnom.performance\" text=\"Accueil\" text=\"Programme actif\"").ShouldBeTrue();
        AndroidSmoke.IsDashboard("package=\"fr.padnom.performance\" text=\"Chargement\"").ShouldBeFalse();
    }

    [Fact]
    public void System_anr_dialog_is_dismissed_with_wait_button()
    {
        const string ui = "text=\"System UI isn't responding\" <node text=\"Wait\" class=\"Button\" bounds=\"[100,200][300,260]\" />";
        AndroidSmoke.FindSystemAnr(ui).ShouldBe((true, (200, 230)));
        AndroidSmoke.FindSystemAnr("text=\"L'interface ne répond pas\"").ShouldBe((true, null));
        AndroidSmoke.FindSystemAnr("text=\"Accueil\"").ShouldBe((false, null));
    }
}

public sealed class VersionCheckTests
{
    [Fact]
    public void Baseline_before_android_project_uses_initial_installed_version()
    {
        var repository = Directory.CreateTempSubdirectory();
        try
        {
            var project = Path.Combine(repository.FullName, ReleaseChecks.Project);
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            File.WriteAllText(project, "<Project><PropertyGroup><ApplicationDisplayVersion>1.4.0</ApplicationDisplayVersion><ApplicationVersion>7</ApplicationVersion></PropertyGroup></Project>");
            var runner = new FakeRunner(call => call.Arguments.Contains("show") ? new ProcessResult(128, "", "does not exist") : new ProcessResult(0, "abc", ""));
            var (previous, current) = VersionCheck.Run(repository.FullName, "v0", requireIncrease: true, runner);
            previous.ShouldBe(VersionCheck.InitialInstalledVersion);
            current.ShouldBe(new ProjectVersion("1.4.0", 7));
        }
        finally { repository.Delete(recursive: true); }
    }

    [Fact]
    public void Unknown_baseline_ref_is_an_error()
    {
        var repository = Directory.CreateTempSubdirectory();
        try
        {
            var project = Path.Combine(repository.FullName, ReleaseChecks.Project);
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            File.WriteAllText(project, "<Project><PropertyGroup><ApplicationDisplayVersion>1.4.0</ApplicationDisplayVersion><ApplicationVersion>7</ApplicationVersion></PropertyGroup></Project>");
            var runner = new FakeRunner(_ => new ProcessResult(128, "", "unknown revision"));
            Should.Throw<ReleaseCheckException>(() => VersionCheck.Run(repository.FullName, "missing", false, runner));
        }
        finally { repository.Delete(recursive: true); }
    }
}

internal sealed class FakeRunner(Func<(string FileName, IReadOnlyList<string> Arguments), ProcessResult> respond) : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
    {
        Calls.Add((fileName, arguments));
        return respond((fileName, arguments));
    }
}
