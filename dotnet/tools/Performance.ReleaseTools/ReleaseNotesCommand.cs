using Performance.Application;

namespace Performance.ReleaseTools;

/// <summary>Turns the in-app "Nouveautés" of the project version into the GitHub release text.</summary>
internal static class ReleaseNotesCommand
{
    public static string Markdown(string version) => ReleaseNotes.For(version) is { } note
        ? ReleaseNotes.ToMarkdown(note)
        : throw new ReleaseCheckException($"No release notes for {version}: add an entry to dotnet/src/Performance.Application/ReleaseNotes.cs.");
}
