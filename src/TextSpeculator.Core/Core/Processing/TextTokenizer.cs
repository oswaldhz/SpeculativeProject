using System.Text.RegularExpressions;

namespace TextSpeculator.Core.Processing;

public static class TextTokenizer
{
    private static readonly Regex TokenRegex = new(
        @"[A-Za-zÁÉÍÓÚáéíóúÑñÜü]+(?:'[A-Za-zÁÉÍÓÚáéíóúÑñÜü]+)?|[.,!?;:]",
        RegexOptions.Compiled
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

    public static string Normalize(string token)
        => token.ToLowerInvariant();

    public static bool IsWord(string token)
        => Regex.IsMatch(token, @"^[A-Za-zÁÉÍÓÚáéíóúÑñÜü]+(?:'[A-Za-zÁÉÍÓÚáéíóúÑñÜü]+)?$");
}