using System.Collections.Concurrent;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Processing;

namespace TextSpeculator.Core.Services;

public sealed class CorpusIndexer
{
    public IReadOnlyList<IndexedSegment> BuildIndexParallel(IEnumerable<CorpusDocument> documents)
    {
        var bag = new ConcurrentBag<IndexedSegment>();

        Parallel.ForEach(documents, document =>
        {
            foreach (var segment in TextSplitter.SplitSegments(document.Content))
            {
                var tokens = TextTokenizer.Tokenize(segment);
                if (tokens.Count == 0)
                    continue;

                var normalized = tokens
                    .Where(TextTokenizer.IsWord)
                    .Select(TextTokenizer.Normalize)
                    .ToList();

                if (normalized.Count == 0)
                    continue;

                bag.Add(new IndexedSegment(
                    document.Name,
                    segment,
                    tokens,
                    normalized
                ));
            }
        });

        return bag
            .OrderBy(segment => segment.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(segment => segment.OriginalText, StringComparer.Ordinal)
            .ToList();
    }
}
