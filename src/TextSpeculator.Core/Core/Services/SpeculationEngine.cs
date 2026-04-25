using System.Collections.Concurrent;
using System.Text;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Processing;

namespace TextSpeculator.Core.Services;

public sealed class SpeculationEngine
{
    private readonly IReadOnlyList<IndexedSegment> _segments;

    public SpeculationEngine(IReadOnlyList<IndexedSegment> segments)
    {
        _segments = segments;
    }

    public IReadOnlyList<SpeculationSuggestion> Suggest(string userText, int topK = 3, int maxWords = 4)
    {
        if (string.IsNullOrWhiteSpace(userText) || _segments.Count == 0)
            return Array.Empty<SpeculationSuggestion>();

        var text = userText.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<SpeculationSuggestion>();

        var userTokens = TextTokenizer.Tokenize(text);
        if (userTokens.Count == 0)
            return Array.Empty<SpeculationSuggestion>();

        // Must end with a letter (continue typing)
        var lastChar = text[^1];
        if (!char.IsLetter(lastChar))
            return Array.Empty<SpeculationSuggestion>();

        var fragment = userTokens.Last();
        if (!TextTokenizer.IsWord(fragment))
            return Array.Empty<SpeculationSuggestion>();

        // Normalize user input for accent/case‑insensitive matching
        var normalizedFragment = TextTokenizer.Normalize(fragment);
        var userCoreTokens = userTokens.Take(userTokens.Count - 1).ToList();
        var normalizedUserCore = userCoreTokens
            .Where(TextTokenizer.IsWord)
            .Select(TextTokenizer.Normalize)
            .ToList();

        var bag = new ConcurrentBag<SpeculationSuggestion>();

        Parallel.ForEach(_segments, segment =>
        {
            var normalizedSegmentTokens = segment.NormalizedTokens;
            if (normalizedSegmentTokens.Count < normalizedUserCore.Count + 1)
                return;

            // Search for a match of the normalized user core tokens inside the segment's normalized tokens
            for (int pos = 0; pos <= normalizedSegmentTokens.Count - normalizedUserCore.Count - 1; pos++)
            {
                bool match = true;
                for (int i = 0; i < normalizedUserCore.Count; i++)
                {
                    if (normalizedSegmentTokens[pos + i] != normalizedUserCore[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match) continue;

                // Candidate continuation: next token after the matched core
                var nextToken = segment.Tokens[pos + normalizedUserCore.Count];
                if (!TextTokenizer.IsWord(nextToken))
                    continue;

                // Compare diacritic‑free versions
                var nextTokenNormalized = TextTokenizer.Normalize(nextToken);
                if (!nextTokenNormalized.StartsWith(normalizedFragment, StringComparison.Ordinal))
                    continue;

                var snippet = BuildShortSnippet(segment.Tokens, pos + normalizedUserCore.Count, maxWords);
                if (string.IsNullOrWhiteSpace(snippet))
                    continue;

                var preview = text[..^fragment.Length] + snippet;

                var score = normalizedUserCore.Count * 10 +
                            Math.Min(maxWords, snippet.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

                bag.Add(new SpeculationSuggestion(
                    Text: snippet,
                    Preview: preview,
                    SourceDocument: segment.DocumentName,
                    Score: score
                ));

                break; // Only take the first match in this segment
            }
        });

        return bag
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .ToList();
    }

    private static string BuildShortSnippet(IReadOnlyList<string> tokens, int startIndex, int maxWords)
    {
        var result = new List<string>();
        var wordCount = 0;

        for (int i = startIndex; i < tokens.Count; i++)
        {
            var token = tokens[i];
            result.Add(token);

            if (TextTokenizer.IsWord(token))
            {
                wordCount++;
                if (wordCount >= maxWords)
                    break;
            }

            if (token is "." or "!" or "?" or ";" or ":")
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

    private static bool IsTokenizerPunctuation(string token) =>
        token is "." or "," or "!" or "?" or ";" or ":";
}
