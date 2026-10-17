using System.Globalization;
using System.Text;

namespace Performance.App.Services;

public static class ShareFileName
{
    private const int MaximumSlugLength = 40;
    private const string FallbackSlug = "seance";

    public static string ForSession(string name, DateTime utcNow) =>
        $"performance-seance-{Slugify(name)}-{utcNow.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture)}.json";

    private static string Slugify(string name)
    {
        var slug = new StringBuilder();
        foreach (var character in (name ?? string.Empty).Normalize(NormalizationForm.FormD))
            Append(slug, character);
        var trimmed = slug.ToString().Trim('-');
        if (trimmed.Length > MaximumSlugLength) trimmed = trimmed[..MaximumSlugLength].TrimEnd('-');
        return trimmed.Length == 0 ? FallbackSlug : trimmed;
    }

    private static void Append(StringBuilder slug, char character)
    {
        if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) return;
        var lowered = char.ToLowerInvariant(character);
        if (lowered is >= 'a' and <= 'z' or >= '0' and <= '9') { slug.Append(lowered); return; }
        if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
    }
}
