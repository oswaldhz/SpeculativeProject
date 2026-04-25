using System.Collections.Concurrent;
using System.Text;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Processing;

namespace TextSpeculator.Core.Services;

public sealed class SpeculationEngine
{
    private const int MaxContextWords = 6;

    private readonly IReadOnlyList<IndexedSegment> _segments;

    public SpeculationEngine(IReadOnlyList<IndexedSegment> segments)
    {
        _segments = segments;
    }

    public IReadOnlyList<SpeculationSuggestion> Suggest(string userText, int topK = 3, int maxWords = 4)
    {
        if (string.IsNullOrWhiteSpace(userText) || _segments.Count == 0 || topK <= 0 || maxWords <= 0)
            return Array.Empty<SpeculationSuggestion>();

        var text = userText.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<SpeculationSuggestion>();

        var userTokens = TextTokenizer.Tokenize(text);
        if (userTokens.Count == 0)
            return Array.Empty<SpeculationSuggestion>();

        var input = BuildSuggestionInput(text, userTokens);
        if (input is null)
            return Array.Empty<SpeculationSuggestion>();

        var bag = new ConcurrentBag<SpeculationSuggestion>();

        Parallel.ForEach(_segments, segment =>
        {
            var wordTokenPositions = GetWordTokenPositions(segment.Tokens);
            var normalizedSegmentTokens = segment.NormalizedTokens;
            if (normalizedSegmentTokens.Count != wordTokenPositions.Count)
                return;

            if (normalizedSegmentTokens.Count < input.NormalizedCoreTokens.Count + 1)
                return;

            for (int pos = 0; pos <= normalizedSegmentTokens.Count - input.NormalizedCoreTokens.Count - 1; pos++)
            {
                if (!MatchesAt(normalizedSegmentTokens, input.NormalizedCoreTokens, pos))
                    continue;

                var nextWordIndex = pos + input.NormalizedCoreTokens.Count;
                var nextTokenIndex = wordTokenPositions[nextWordIndex];
                var nextTokenNormalized = normalizedSegmentTokens[nextWordIndex];
                if (!nextTokenNormalized.StartsWith(input.NormalizedFragment, StringComparison.Ordinal))
                    continue;

                var snippet = BuildShortSnippet(segment.Tokens, nextTokenIndex, maxWords);
                if (string.IsNullOrWhiteSpace(snippet))
                    continue;

                var preview = input.TextBeforeFragment + snippet;
                var score = CalculateScore(input, nextTokenNormalized, snippet);

                bag.Add(new SpeculationSuggestion(
                    Text: snippet,
                    Preview: preview,
                    SourceDocument: segment.DocumentName,
                    Score: score
                ));

                break;
            }
        });

        return bag
            .GroupBy(suggestion => suggestion.Text, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(suggestion => suggestion.Score)
                .ThenBy(suggestion => suggestion.SourceDocument, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(suggestion => suggestion.Score)
            .ThenBy(suggestion => suggestion.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(suggestion => suggestion.SourceDocument, StringComparer.OrdinalIgnoreCase)
            .Take(topK)
            .ToList();
    }

    private static SuggestionInput? BuildSuggestionInput(string text, IReadOnlyList<string> userTokens)
    {
        var lastChar = text[^1];
        if (char.IsLetterOrDigit(lastChar))
        {
            var fragment = userTokens.Last();
            if (!TextTokenizer.IsWord(fragment))
                return null;

            var normalizedCoreTokens = userTokens
                .Take(userTokens.Count - 1)
                .Where(TextTokenizer.IsWord)
                .Select(TextTokenizer.Normalize)
                .TakeLast(MaxContextWords)
                .ToList();

            return new SuggestionInput(
                normalizedCoreTokens,
                TextTokenizer.Normalize(fragment),
                text[..^fragment.Length]);
        }

        if (!char.IsWhiteSpace(lastChar))
            return null;

        var normalizedUserTokens = userTokens
            .Where(TextTokenizer.IsWord)
            .Select(TextTokenizer.Normalize)
            .TakeLast(MaxContextWords)
            .ToList();

        return new SuggestionInput(
            normalizedUserTokens,
            string.Empty,
            text);
    }

    private static bool MatchesAt(
        IReadOnlyList<string> normalizedSegmentTokens,
        IReadOnlyList<string> normalizedUserCoreTokens,
        int startIndex)
    {
        for (int i = 0; i < normalizedUserCoreTokens.Count; i++)
        {
            if (normalizedSegmentTokens[startIndex + i] != normalizedUserCoreTokens[i])
                return false;
        }

        return true;
    }

    private static List<int> GetWordTokenPositions(IReadOnlyList<string> tokens)
    {
        var positions = new List<int>(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            if (TextTokenizer.IsWord(tokens[i]))
                positions.Add(i);
        }

        return positions;
    }

    private static int CalculateScore(SuggestionInput input, string nextTokenNormalized, string snippet)
    {
        var snippetWordCount = TextTokenizer.Tokenize(snippet).Count(TextTokenizer.IsWord);
        var fragmentScore = string.IsNullOrEmpty(input.NormalizedFragment)
            ? 0
            : Math.Min(input.NormalizedFragment.Length, nextTokenNormalized.Length);

        return (input.NormalizedCoreTokens.Count * 20) +
               (fragmentScore * 5) +
               snippetWordCount;
    }

    private static string BuildShortSnippet(IReadOnlyList<string> tokens, int startIndex, int maxWords)
    {
        var result = new List<string>();
        var wordCount = 0;

        for (int i = startIndex; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IsSentenceEndingPunctuation(token))
                break;

            result.Add(token);

            if (!TextTokenizer.IsWord(token))
                continue;

            wordCount++;
            if (wordCount >= maxWords)
                break;
        }

        var snippetBuilder = new StringBuilder();
        foreach (var token in result)
        {
            if (IsTokenizerPunctuation(token))
            {
                snippetBuilder.Append(token);
                continue;
            }

            if (snippetBuilder.Length > 0)
                snippetBuilder.Append(' ');

            snippetBuilder.Append(token);
        }

        return snippetBuilder.ToString().Trim();
    }

    private static bool IsSentenceEndingPunctuation(string token) =>
        token is "." or "!" or "?" or ";" or ":";

    private static bool IsTokenizerPunctuation(string token) =>
        token is "." or "," or "!" or "?" or ";" or ":";

    private sealed record SuggestionInput(
        IReadOnlyList<string> NormalizedCoreTokens,
        string NormalizedFragment,
        string TextBeforeFragment);
}
