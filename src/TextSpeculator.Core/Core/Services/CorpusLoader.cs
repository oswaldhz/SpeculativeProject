using System.Collections.Concurrent;
using TextSpeculator.Core.Models;
using TextSpeculator.Core.Readers;

namespace TextSpeculator.Core.Services;

public sealed class CorpusLoader
{
    private readonly IReadOnlyList<ITextDocumentReader> _readers;

    public CorpusLoader(IEnumerable<ITextDocumentReader> readers)
    {
        _readers = readers.ToList();
    }

    public async Task<IReadOnlyList<CorpusDocument>> LoadDocumentsParallelAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var bag = new ConcurrentBag<CorpusDocument>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(paths, options, async (path, ct) =>
        {
            var extension = Path.GetExtension(path);
            var reader = _readers.FirstOrDefault(r => r.CanRead(extension));

            if (reader is null)
                return;

            var content = await reader.ReadAsync(path, ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                bag.Add(new CorpusDocument(
                    path,
                    Path.GetFileName(path),
                    content
                ));
            }
        });

        return bag.OrderBy(d => d.Name).ToList();
    }
}