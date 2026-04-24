namespace TextSpeculator.Core.Readers;

public sealed class TxtDocumentReader : ITextDocumentReader
{
    public bool CanRead(string extension) => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

    public Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllTextAsync(path, cancellationToken);
    }
}