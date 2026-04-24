using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TextSpeculator.Core.Processing;

public static class TextTokenizer
{
    // Match any word-like token (letters + apostrophe) or punctuation
    private static readonly Regex TokenRegex = new(
        @"\p{L}+(?:'\p{L}+)?|[.,!?;:]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return TokenRegex
            .Matches(text)
            .Select(m => m.Value)
            .ToList();
    }

    /// <summary>Normalize a token for matching: lowercase and remove diacritics.</summary>
    public static string Normalize(string token)
    {
        if (string.IsNullOrEmpty(token))
            return token;

        // First lowercase, then remove diacritics
        token = token.ToLowerInvariant();
        return RemoveDiacritics(token);
    }

    /// <summary>Remove combining diacritical marks from a string.</summary>
    public static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();
        return new string(chars);
    }

    /// <summary>Check if a token is a word (not punctuation).</summary>
    public static bool IsWord(string token)
    {
        // Use a pattern that matches letters + optional apostrophe
        return Regex.IsMatch(token, @"^\p{L}+(?:'\p{L}+)?$", RegexOptions.CultureInvariant);
    }
}