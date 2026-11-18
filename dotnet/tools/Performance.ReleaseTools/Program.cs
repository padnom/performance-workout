using Performance.ReleaseTools;

const string Usage = """
    Performance release checks.

    Usage:
      verify-release-apk <apk> --project <csproj> --build-tools <dir> [--expected-certificate <file>]
      verify-debug-apk <apk>
      verify-version [--repo <dir>] [--baseline-ref <ref>] [--require-increase]
      project-version [--project <csproj>]
      smoke-android <apk> --output <dir>
    """;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 2 : 0;
}

var command = args[0];
var options = Options.Parse(args[1..]);
var runner = new ProcessRunner();
try
{
    switch (command)
    {
        case "verify-release-apk":
        {
            var version = ReleaseChecks.ParseProjectVersion(File.ReadAllText(options.Required("--project")));
            var buildTools = options.Required("--build-tools");
            var certificateFile = options.Value("--expected-certificate");
            var certificate = certificateFile is null ? null : File.ReadAllText(certificateFile).Trim();
            ReleaseChecks.VerifyPackage(options.Argument(0, "apk"), version,
                Path.Combine(buildTools, "apksigner"), Path.Combine(buildTools, "aapt"), certificate, runner);
            Console.WriteLine($"Release {version.Display} (code {version.Code}), signature{(certificate is null ? "" : ", existing certificate")}, arm64-v8a/x86_64 and AOT payload verified.");
            break;
        }
        case "verify-debug-apk":
        {
            using var apk = File.OpenRead(options.Argument(0, "apk"));
            Console.WriteLine($"Standalone Debug APK verified: {string.Join(", ", ReleaseChecks.VerifyDebugPayload(apk))}");
            break;
        }
        case "verify-version":
        {
            var (previous, current) = VersionCheck.Run(options.Value("--repo") ?? ".", options.Value("--baseline-ref") ?? "HEAD",
                options.Flag("--require-increase"), runner);
            Console.WriteLine($"Version verified: {previous} -> {current}.");
            break;
        }
        case "project-version":
            Console.WriteLine(ReleaseChecks.ParseProjectVersion(File.ReadAllText(options.Value("--project") ?? ReleaseChecks.Project)).Display);
            break;
        case "smoke-android":
        {
            var smoke = new AndroidSmoke(runner, options.Required("--output"));
            try { smoke.Run(options.Argument(0, "apk")); }
            catch (Exception error) when (error is ReleaseCheckException or IOException or System.ComponentModel.Win32Exception)
            {
                smoke.PrintDiagnostics();
                throw;
            }
            finally { smoke.CollectEvidence(); }
            break;
        }
        default:
            Console.Error.WriteLine($"Unknown command '{command}'.\n{Usage}");
            return 2;
    }
    return 0;
}
catch (Exception error) when (error is ReleaseCheckException or IOException or UnauthorizedAccessException
                              or System.Xml.XmlException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine($"{command} failed: {error.Message}");
    return 1;
}

internal sealed class Options
{
    private readonly List<string> _arguments = [];
    private readonly Dictionary<string, string?> _named = [];

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) options._arguments.Add(args[i]);
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) options._named[args[i]] = args[++i];
            else options._named[args[i]] = null;
        }
        return options;
    }

    public string Argument(int index, string name) =>
        index < _arguments.Count ? _arguments[index] : throw new ReleaseCheckException($"Missing <{name}> argument.");
    public string? Value(string name) => _named.GetValueOrDefault(name);
    public string Required(string name) => Value(name) ?? throw new ReleaseCheckException($"Missing {name} option.");
    public bool Flag(string name) => _named.ContainsKey(name);
}
