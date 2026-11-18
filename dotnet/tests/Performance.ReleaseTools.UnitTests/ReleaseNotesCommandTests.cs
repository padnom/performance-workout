using Performance.ReleaseTools;
using Shouldly;
using Xunit;

namespace Performance.ReleaseTools.UnitTests;

public sealed class ReleaseNotesCommandTests
{
    [Fact]
    public void CurrentProjectVersion_HasReleaseNotes()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ReleaseChecks.Project))) directory = directory.Parent;
        directory.ShouldNotBeNull("repository root not found");
        var version = ReleaseChecks.ParseProjectVersion(File.ReadAllText(Path.Combine(directory.FullName, ReleaseChecks.Project))).Display;

        ReleaseNotesCommand.Markdown(version).ShouldContain($"version {version}");
    }

    [Fact]
    public void UnknownVersion_FailsWithGuidance()
    {
        Should.Throw<ReleaseCheckException>(() => ReleaseNotesCommand.Markdown("0.0.1")).Message.ShouldContain("ReleaseNotes.cs");
    }
}
