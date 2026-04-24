using TextSpeculator.Core.Models;
using TextSpeculator.Core.Processing;
using System.Collections.Concurrent;

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
                    .Where(t => TextTokenizer.IsWord(t))
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

        return bag.ToList();
    }
}