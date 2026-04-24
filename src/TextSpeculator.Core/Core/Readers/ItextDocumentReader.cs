namespace TextSpeculator.Core.Readers;

public interface ITextDocumentReader
{
    bool CanRead(string extension);
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);
}