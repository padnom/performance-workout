using System.Globalization;
using System.Text;

namespace Performance.Domain;

/// <summary>
/// Muscles are read from an exercise's tags. <see cref="Exercise.MuscleGroup"/> is kept only as a stored,
/// derived value so older files and backups stay readable; it is the fallback when no tag names a muscle.
/// </summary>
public static class ExerciseMuscles
{
    public static readonly IReadOnlyList<string> Known =
    [
        "Pectoraux", "Dos", "Grand dorsal", "Trapèzes", "Épaules", "Arrière d’épaule", "Biceps", "Triceps", "Avant-bras",
        "Quadriceps", "Ischio-jambiers", "Fessiers", "Adducteurs", "Mollets", "Jambes", "Abdominaux", "Obliques", "Lombaires"
    ];

    // Hand-typed values seen in real data ("Epaile", "Isciaux", "Haut pectoraux", "biceps"...). Order matters:
    // "ischiojambiers" also contains "jambe".
    private static readonly (string Fragment, string Muscle)[] Aliases =
    [
        ("ischio", "Ischio-jambiers"), ("isciau", "Ischio-jambiers"), ("hamstring", "Ischio-jambiers"),
        ("arriere", "Arrière d’épaule"), ("pector", "Pectoraux"), ("epaul", "Épaules"), ("epail", "Épaules"), ("delto", "Épaules"),
        ("fessi", "Fessiers"), ("glute", "Fessiers"), ("quadri", "Quadriceps"), ("mollet", "Mollets"), ("abdo", "Abdominaux"),
        ("lombai", "Lombaires"), ("trapez", "Trapèzes"), ("biceps", "Biceps"), ("triceps", "Triceps"), ("avantbras", "Avant-bras"),
        ("dorsal", "Grand dorsal"), ("obliqu", "Obliques"), ("adduct", "Adducteurs"), ("jambe", "Jambes")
    ];

    /// <summary>The known muscle a tag or legacy muscle text names, or null when it names none.</summary>
    public static string? Canonical(string? value)
    {
        var key = Key(value);
        if (key.Length == 0) return null;
        foreach (var muscle in Known)
            if (Key(muscle) == key) return muscle;
        foreach (var (fragment, muscle) in Aliases)
            if (key.Contains(fragment, StringComparison.Ordinal)) return muscle;
        return null;
    }

    public static bool IsMuscle(string? tag) => Canonical(tag) is not null;

    /// <summary>Muscles named by the tags, in tag order, without duplicates.</summary>
    public static IReadOnlyList<string> Muscles(Exercise exercise) =>
        exercise.Tags.Select(Canonical).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Primary muscle: the stored muscle when the tags still name it (older data), otherwise the first muscle tag,
    /// preferring a precise muscle over the broad "Jambes". Without any muscle tag, the stored text is used as is.
    /// </summary>
    public static string Primary(Exercise exercise)
    {
        var muscles = Muscles(exercise);
        var stored = Canonical(exercise.MuscleGroup);
        if (stored is not null && muscles.Contains(stored, StringComparer.Ordinal)) return stored;
        return muscles.FirstOrDefault(muscle => muscle != "Jambes") ?? muscles.FirstOrDefault()
            ?? stored ?? exercise.MuscleGroup.Trim();
    }

    /// <summary>Tags for an editor: the primary muscle first, and the stored muscle added when no tag names one.</summary>
    public static IReadOnlyList<string> EditableTags(Exercise exercise)
    {
        var tags = exercise.Tags.ToList();
        var primary = Primary(exercise);
        if (Canonical(primary) is not { } muscle) return tags;
        var index = tags.FindIndex(tag => Canonical(tag) == muscle);
        if (index < 0) { tags.Insert(0, muscle); return tags; }
        var tag = tags[index];
        tags.RemoveAt(index);
        tags.Insert(0, tag);
        return tags;
    }

    /// <summary>The muscle to store for tags chosen in an editor: the first muscle tag, or an empty text.</summary>
    public static string FromTags(IEnumerable<string> tags) => tags.Select(Canonical).OfType<string>().FirstOrDefault() ?? "";

    private static string Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }
}
