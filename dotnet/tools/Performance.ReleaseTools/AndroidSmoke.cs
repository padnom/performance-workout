using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Performance.ReleaseTools;

/// <summary>Installs the actual standalone APK on the emulator, verifies launch and render, and collects CI evidence.</summary>
public sealed partial class AndroidSmoke(IProcessRunner runner, string outputDirectory)
{
    private const string Package = ReleaseChecks.ApplicationId;
    // Software-rendered CI emulators can take well over 30 s for the first WebView frame.
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(120);

    public void Run(string apk)
    {
        Directory.CreateDirectory(outputDirectory);
        Adb("wait-for-device");
        Adb("install", "-r", apk);
        Adb("shell", "am", "force-stop", Package);
        Adb("logcat", "-c");
        var resolved = Adb("shell", "cmd", "package", "resolve-activity", "--brief", Package).Output.Trim().Split('\n');
        var component = resolved[^1].Trim();
        if (!component.Contains('/')) throw new ReleaseCheckException("No Android launcher activity found.");
        Adb("shell", "am", "start", "-W", "-n", component);
        var clock = Stopwatch.StartNew();
        var rendered = false;
        while (clock.Elapsed < RenderTimeout)
        {
            if (!IsRunning()) throw new ReleaseCheckException("Application process exited after launch.");
            AdbUnchecked("shell", "rm", "-f", "/sdcard/performance-smoke.xml");
            AdbUnchecked("shell", "uiautomator", "dump", "/sdcard/performance-smoke.xml");
            var ui = AdbUnchecked("shell", "cat", "/sdcard/performance-smoke.xml").Output;
            File.WriteAllText(Path.Combine(outputDirectory, "launch-ui.xml"), ui);
            if (DismissSystemAnr(ui)) continue;
            if (IsDashboard(ui)) { rendered = true; break; }
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
        if (!rendered)
            throw new ReleaseCheckException($"Process started but the Blazor dashboard did not render within {RenderTimeout.TotalSeconds:0} seconds.");
        Console.WriteLine($"Dashboard rendered after {clock.Elapsed.TotalSeconds:0} s.");
        // Catch late startup crashes, including native crashes after the first frame.
        Thread.Sleep(TimeSpan.FromSeconds(3));
        if (!IsRunning()) throw new ReleaseCheckException("Application process exited after dashboard rendering.");
        if (Adb("logcat", "-b", "crash", "-d").Output.Contains(Package, StringComparison.Ordinal))
            throw new ReleaseCheckException("Android crash buffer contains an application crash.");
        Console.WriteLine("Standalone Release APK installed; process and Blazor dashboard verified on emulator.");
    }

    internal static bool IsDashboard(string ui) =>
        ui.Contains(Package, StringComparison.Ordinal) && ui.Contains("Accueil", StringComparison.Ordinal) && ui.Contains("Programme actif", StringComparison.Ordinal);

    /// <summary>Where to tap "Wait" when an overloaded emulator shows "System UI isn't responding" above the app.</summary>
    internal static (bool Shown, (int X, int Y)? Wait) FindSystemAnr(string ui)
    {
        if (!ui.Contains("isn't responding", StringComparison.Ordinal) && !ui.Contains("ne répond pas", StringComparison.Ordinal)) return (false, null);
        var match = WaitButton().Match(ui);
        if (!match.Success) return (true, null);
        var bounds = Enumerable.Range(1, 4).Select(i => int.Parse(match.Groups[i].Value)).ToArray();
        return (true, ((bounds[0] + bounds[2]) / 2, (bounds[1] + bounds[3]) / 2));
    }

    private bool DismissSystemAnr(string ui)
    {
        var (shown, wait) = FindSystemAnr(ui);
        if (!shown) return false;
        if (wait is var (x, y)) AdbUnchecked("shell", "input", "tap", x.ToString(), y.ToString());
        else AdbUnchecked("shell", "input", "keyevent", "KEYCODE_BACK");
        Console.WriteLine("Dismissed a system \"not responding\" dialog shown by the emulator.");
        Thread.Sleep(TimeSpan.FromSeconds(2));
        return true;
    }

    /// <summary>Echoes the visible text and relevant log lines so failures can be read from the job log.</summary>
    public void PrintDiagnostics()
    {
        try
        {
            var uiPath = Path.Combine(outputDirectory, "launch-ui.xml");
            var ui = File.Exists(uiPath) ? File.ReadAllText(uiPath) : "";
            var texts = VisibleText().Matches(ui).Select(match => match.Groups[1].Value).Where(text => text.Trim().Length > 0).Take(80).ToList();
            Console.WriteLine("--- Visible UI text ---");
            Console.WriteLine(texts.Count > 0 ? string.Join(" | ", texts) : "(none)");
            var log = AdbUnchecked("logcat", "-d").Output.Split('\n');
            var pid = AdbUnchecked("shell", "pidof", Package).Output.Trim();
            Console.WriteLine($"--- Application process: {(pid.Length > 0 ? pid : "not running")} ---");
            Console.WriteLine("--- Application logcat (last 150 lines) ---");
            var relevant = log.Where(line => (pid.Length > 0 && line.Contains($" {pid} ", StringComparison.Ordinal))
                                             || (RelevantLog().IsMatch(line) && !line.Contains(" E ", StringComparison.Ordinal))).ToList();
            Console.WriteLine(string.Join('\n', relevant.TakeLast(150)));
            Console.WriteLine("--- Crash buffer ---");
            var crashes = AdbUnchecked("logcat", "-b", "crash", "-d").Output;
            Console.WriteLine(crashes.Length > 0 ? crashes[Math.Max(0, crashes.Length - 4000)..] : "(empty)");
        }
        catch (Exception error) when (error is IOException or ReleaseCheckException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"Diagnostics unavailable: {error.Message}");
        }
    }

    public void CollectEvidence()
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "logcat.txt"), AdbUnchecked("logcat", "-d").Output);
            ProcessRunner.RunToFile("adb", ["exec-out", "screencap", "-p"], Path.Combine(outputDirectory, "launch.png"), TimeSpan.FromSeconds(15));
        }
        catch (Exception error) when (error is IOException or ReleaseCheckException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine($"Evidence collection incomplete: {error.Message}");
        }
    }

    private bool IsRunning() => AdbUnchecked("shell", "pidof", Package).Output.Trim().Length > 0;
    private ProcessResult Adb(params string[] arguments) => runner.RunChecked("adb", arguments);
    private ProcessResult AdbUnchecked(params string[] arguments) => runner.Run("adb", arguments);

    [GeneratedRegex("text=\"(?:Wait|Attendre)\"[^>]*bounds=\"\\[(\\d+),(\\d+)\\]\\[(\\d+),(\\d+)\\]\"")] private static partial Regex WaitButton();
    [GeneratedRegex("(?:text|content-desc)=\"([^\"]+)\"")] private static partial Regex VisibleText();
    [GeneratedRegex("performance|mono|dotnet|AndroidRuntime|chromium|WebView|FATAL| E ", RegexOptions.IgnoreCase)] private static partial Regex RelevantLog();
}
