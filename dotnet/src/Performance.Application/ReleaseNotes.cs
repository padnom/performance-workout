using System.Text;

namespace Performance.Application;

/// <summary>User-facing notes of one published Android version, shown once after the update.</summary>
public sealed record ReleaseNote(Version Version, IReadOnlyList<string> Items, IReadOnlyList<string>? EnglishItems = null)
{
    /// <summary>Items in the display language; French when no English text was written for this version.</summary>
    public IReadOnlyList<string> DisplayedItems => DisplayLanguage.IsEnglish && EnglishItems is { Count: > 0 } ? EnglishItems : Items;
}

/// <summary>
/// Single source of the "Nouveautés" shown in the app after an update and of the GitHub release text.
/// Add an entry here before publishing a new version: the release workflow refuses a version without notes.
/// Write the French items and their English translation (shown when the app is in English).
/// </summary>
public static class ReleaseNotes
{
    public const string SeenSetting = "app-release-notes-seen";

    /// <summary>Last version published before the "Nouveautés" window existed; used when nothing was recorded yet.</summary>
    private static readonly Version FirstVersionWithoutNotes = new(1, 4, 0);
    private const int MaximumShown = 3;

    public static IReadOnlyList<ReleaseNote> All { get; } =
    [
        new(new Version(1, 9, 0),
        [
            "Renomme tes séances : ouvre une séance de « Mes séances » ou d’un de tes programmes dans Programmes, puis touche ✎ Renommer.",
        ],
        [
            "Rename your workouts: open a workout from “My workouts” or one of your programs in Programs, then tap ✎ Rename.",
        ]),
        new(new Version(1, 8, 0),
        [
            "Séances favorites : touche l’étoile d’une séance dans l’Historique ou dans Programmes, puis retrouve-la avec le filtre « ★ Favoris ».",
            "Une séance libre mise en favori depuis l’Historique est enregistrée dans « Mes séances ».",
            "Les favoris portent désormais sur les séances et non plus sur les programmes : les séances de tes programmes favoris restent favorites.",
        ],
        [
            "Favorite workouts: tap a workout’s star in History or in Programs, then find it with the “★ Favorites” filter.",
            "A free workout favorited from History is saved to “My workouts”.",
            "Favorites now apply to workouts instead of programs: the workouts of your favorite programs stay favorites.",
        ]),
        new(new Version(1, 7, 0),
        [
            "L’application est disponible en anglais : elle suit la langue du téléphone, modifiable dans Réglages → Langue.",
            "Bibliothèque : chaque variante affiche sa description et ses exercices spécifiques, avec un nom plus parlant (ex. « Jambes · B · Presse & fente »).",
        ],
        [
            "The app is now available in English: it follows the phone language, changeable in Settings → Language.",
            "Library: each variant shows its description and its own exercises, with a clearer name (e.g. “Jambes · B · Presse & fente”).",
        ]),
        new(new Version(1, 6, 0),
        [
            "Programmes devient une bibliothèque : cherche une séance par nom, exercice ou muscle, filtre-la (favoris, mes séances, ta salle, classiques, ciblées) et démarre-la avec ▶.",
            "Créer, importer et exporter des programmes sont regroupés dans le menu ⋯ de la page Programmes.",
            "La semaine planifiée n’est plus affichée, sur l’Accueil comme dans Programmes. Tes données restent conservées.",
            "Pendant une séance, ajoute ou supprime une série sur chaque exercice.",
            "Exercices : le muscle se choisit avec les tags ; le champ « Muscle principal » disparaît et les fautes de frappe existantes sont reconnues.",
        ],
        [
            "Programs becomes a library: search a workout by name, exercise or muscle, filter it (favorites, my workouts, your gym, classic, targeted) and start it with ▶.",
            "Creating, importing and exporting programs are grouped in the ⋯ menu of the Programs page.",
            "The planned week is no longer shown, on Home or in Programs. Your data is kept.",
            "During a workout, add or remove a set on each exercise.",
            "Exercises: the muscle is chosen with tags; the “Main muscle” field is gone and existing typos are recognized.",
        ]),
        new(new Version(1, 5, 2),
        [
            "Change de salle (et de thème) directement depuis le bouton en haut à droite, sans passer par les réglages.",
            "Pendant une séance, ce bouton change la salle de la séance en cours.",
        ],
        [
            "Switch gym (and theme) straight from the button at the top right, without going through the settings.",
            "During a workout, this button changes the gym of the current workout.",
        ]),
        new(new Version(1, 5, 1),
        [
            "Fenêtre « Nouveautés » : après chaque mise à jour, l’application résume ce qui a changé.",
        ],
        [
            "“What’s new” window: after each update, the app sums up what changed.",
        ]),
        new(new Version(1, 5, 0),
        [
            "Historique : « Refaire cette séance » relance une séance terminée avec les mêmes exercices, dans le même ordre et avec le même nombre de séries.",
            "La salle utilisée ce jour-là est présélectionnée et les charges reprennent ta dernière performance.",
            "Les séances terminées gardent leur nom dans l’historique.",
        ],
        [
            "History: “Redo this workout” restarts a finished workout with the same exercises, in the same order and with the same number of sets.",
            "The gym used that day is preselected and weights start from your last performance.",
            "Finished workouts keep their name in the history.",
        ]),
        new(new Version(1, 4, 0),
        [
            "Mise à jour intégrée : l’application propose et installe elle-même les nouvelles versions.",
            "Thème et logo de la salle (On Air en rouge et noir, Basic-Fit, toutes salles).",
        ],
        [
            "Built-in updates: the app offers and installs new versions itself.",
            "Gym theme and logo (On Air in red and black, Basic-Fit, all gyms).",
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
        if (note.EnglishItems is not { Count: > 0 } english) return text.ToString();
        text.Append($"\n## What’s new in version {note.Version}\n\n");
        foreach (var item in english) text.Append("- ").Append(item).Append('\n');
        return text.ToString();
    }
}
