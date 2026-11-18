using System.Text;

namespace Performance.Application;

/// <summary>User-facing notes of one published Android version, shown once after the update.</summary>
public sealed record ReleaseNote(Version Version, IReadOnlyList<string> Items);

/// <summary>
/// Single source of the "Nouveautés" shown in the app after an update and of the GitHub release text.
/// Add an entry here before publishing a new version: the release workflow refuses a version without notes.
/// </summary>
public static class ReleaseNotes
{
    public const string SeenSetting = "app-release-notes-seen";

    /// <summary>Last version published before the "Nouveautés" window existed; used when nothing was recorded yet.</summary>
    private static readonly Version FirstVersionWithoutNotes = new(1, 4, 0);
    private const int MaximumShown = 3;

    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new(new Version(1, 5, 1),
        [
            "Fenêtre « Nouveautés » : après chaque mise à jour, l’application résume ce qui a changé.",
        ]),
        new(new Version(1, 5, 0),
        [
            "Historique : « Refaire cette séance » relance une séance terminée avec les mêmes exercices, dans le même ordre et avec le même nombre de séries.",
            "La salle utilisée ce jour-là est présélectionnée et les charges reprennent ta dernière performance.",
            "Les séances terminées gardent leur nom dans l’historique.",
        ]),
        new(new Version(1, 4, 0),
        [
            "Mise à jour intégrée : l’application propose et installe elle-même les nouvelles versions.",
            "Thème et logo de la salle (On Air en rouge et noir, Basic-Fit, toutes salles).",
        ]),
    ];

    public static ReleaseNote? For(string? version) =>
        AppReleaseParser.ParseVersion(version) is { } parsed ? All.FirstOrDefault(note => note.Version == parsed) : null;

    /// <summary>Notes newer than the last seen version, up to the installed one, newest first.</summary>
    public static IReadOnlyList<ReleaseNote> Unseen(string? lastSeen, string? installed)
    {
        if (AppReleaseParser.ParseVersion(installed) is not { } current) return [];
        var seen = AppReleaseParser.ParseVersion(lastSeen) ?? FirstVersionWithoutNotes;
        return All.Where(note => note.Version > seen && note.Version <= current)
            .OrderByDescending(note => note.Version).Take(MaximumShown).ToArray();
    }

    public static string ToMarkdown(ReleaseNote note)
    {
        var text = new StringBuilder($"## Nouveautés de la version {note.Version}\n\n");
        foreach (var item in note.Items) text.Append("- ").Append(item).Append('\n');
        return text.ToString();
    }
}
