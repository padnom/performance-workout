namespace Performance.ReleaseTools;

/// <summary>Compares the tracked project version with the one at a previous Git release or ref.</summary>
public static class VersionCheck
{
    public static readonly ProjectVersion InitialInstalledVersion = new("1.0", 1);

    public static (ProjectVersion Previous, ProjectVersion Current) Run(string repository, string baselineRef, bool requireIncrease, IProcessRunner runner)
    {
        var current = ReleaseChecks.ParseProjectVersion(File.ReadAllText(Path.Combine(repository, ReleaseChecks.Project)));
        var old = runner.Run("git", ["-C", repository, "show", $"{baselineRef}:{ReleaseChecks.Project}"]);
        ProjectVersion previous;
        if (old.ExitCode != 0)
        {
            // Only allow the initial Android release when the ref exists but predates the project.
            runner.RunChecked("git", "-C", repository, "rev-parse", "--verify", $"{baselineRef}^{{commit}}");
            previous = InitialInstalledVersion;
            Console.WriteLine("Baseline predates Android project; using initial installed version 1.0/code 1.");
        }
        else
        {
            previous = ReleaseChecks.ParseProjectVersion(old.Output);
        }
        ReleaseChecks.CheckVersion(current, previous, requireIncrease);
        return (previous, current);
    }
}
