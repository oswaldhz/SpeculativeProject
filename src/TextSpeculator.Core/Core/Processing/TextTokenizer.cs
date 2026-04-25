using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TextSpeculator.Core.Processing;

public static class TextTokenizer
{
    private const string WordPattern = "[\\p{L}\\p{N}]+(?:['\\u2019][\\p{L}\\p{N}]+)*";

    private static readonly Regex WordRegex = new(
        $"^{WordPattern}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex TokenRegex = new(
        $"{WordPattern}|[.,!?;:]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return TokenRegex
            .Matches(text)
            .Select(match => match.Value)
            .ToList();
    }

    public static string Normalize(string token)
    {
        if (string.IsNullOrEmpty(token))
            return token;

        token = token
            .ToLowerInvariant()
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'');

        return RemoveDiacritics(token);
    }

    public static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(chars).Normalize(NormalizationForm.FormC);
    }

    public static bool IsWord(string token) => WordRegex.IsMatch(token);
}
