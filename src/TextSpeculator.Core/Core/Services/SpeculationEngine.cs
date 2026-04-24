using System.Collections.Concurrent;
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

        var lastChar = text[^1];
        if (!char.IsLetter(lastChar))
            return Array.Empty<SpeculationSuggestion>();

        var fragment = userTokens.Last();
        if (!TextTokenizer.IsWord(fragment))
            return Array.Empty<SpeculationSuggestion>();

        var userCore = userTokens.Take(userTokens.Count - 1).ToList();
        var bag = new ConcurrentBag<SpeculationSuggestion>();

        Parallel.ForEach(_segments, segment =>
        {
            var segmentTokens = segment.Tokens;
            if (segmentTokens.Count < userCore.Count + 1)
                return;

            for (int pos = 0; pos <= segmentTokens.Count - userCore.Count - 1; pos++)
            {
                bool match = true;

                for (int i = 0; i < userCore.Count; i++)
                {
                    if (!string.Equals(
                            segmentTokens[pos + i],
                            userCore[i],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                    continue;

                var nextToken = segmentTokens[pos + userCore.Count];
                if (!TextTokenizer.IsWord(nextToken))
                    continue;

                if (!nextToken.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
                    continue;

                var snippet = BuildShortSnippet(segmentTokens, pos + userCore.Count, maxWords);
                if (string.IsNullOrWhiteSpace(snippet))
                    continue;

                var preview = text[..^fragment.Length] + snippet;

                var score = userCore.Count * 10 + Math.Min(maxWords, snippet.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

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

        return string.Join(" ", result).Trim();
    }
}