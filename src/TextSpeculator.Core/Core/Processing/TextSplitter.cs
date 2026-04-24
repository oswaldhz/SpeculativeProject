using System.Text.RegularExpressions;

namespace TextSpeculator.Core.Processing;

public static class TextSplitter
{
    private static readonly Regex SplitRegex = new(
        @"(?<=[.!?;:])\s+|\r?\n+",
        RegexOptions.Compiled
    );

    public static IEnumerable<string> SplitSegments(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var part in SplitRegex.Split(text))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                yield return trimmed;
        }
    }
}