using System.Diagnostics;

namespace Performance.ReleaseTools;

public sealed record ProcessResult(int ExitCode, string Output, string Error);

public interface IProcessRunner
{
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan? timeout = null);
}

public sealed class ProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new ReleaseCheckException($"Cannot start {fileName}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(45)))
        {
            process.Kill(entireProcessTree: true);
            throw new ReleaseCheckException($"{fileName} {string.Join(' ', arguments)} timed out.");
        }
        return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }

    /// <summary>Writes the raw standard output of a command (binary such as a screenshot) to a file.</summary>
    public static void RunToFile(string fileName, IReadOnlyList<string> arguments, string path, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new ReleaseCheckException($"Cannot start {fileName}.");
        using (var file = File.Create(path)) process.StandardOutput.BaseStream.CopyTo(file);
        if (!process.WaitForExit(timeout)) process.Kill(entireProcessTree: true);
    }
}

public static class ProcessRunnerExtensions
{
    public static ProcessResult RunChecked(this IProcessRunner runner, string fileName, params string[] arguments)
    {
        var result = runner.Run(fileName, arguments);
        if (result.ExitCode != 0)
            throw new ReleaseCheckException($"{Path.GetFileName(fileName)} {string.Join(' ', arguments)} failed ({result.ExitCode}): {result.Error.Trim()}");
        return result;
    }
}
