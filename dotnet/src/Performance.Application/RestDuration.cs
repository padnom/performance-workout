using System.Text.RegularExpressions;

namespace Performance.Application;

public static class RestDuration
{
    public static int Parse(string? rest)
    {
        rest ??= "";
        // Preserve the web parser's order, including "2–3 min" matching "3 min" first.
        var clock = Regex.Match(rest, @"^(\d+):(\d{2})$");
        if (clock.Success && int.TryParse(clock.Groups[1].Value, out var minutes)
            && int.TryParse(clock.Groups[2].Value, out var seconds) && minutes <= 10)
            return Math.Clamp(minutes * 60 + seconds, 15, 600);
        if (rest.Contains("3 min", StringComparison.Ordinal)) return 180;
        if (rest.Contains("2–3", StringComparison.Ordinal) || rest.Contains("min 30–2", StringComparison.Ordinal)) return 150;
        if (rest.Contains("2 min", StringComparison.Ordinal)) return 120;
        if (rest.Contains("1 min 30", StringComparison.Ordinal)) return 90;
        return 60;
    }
}
