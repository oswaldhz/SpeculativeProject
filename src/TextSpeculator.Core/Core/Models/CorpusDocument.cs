namespace TextSpeculator.Core.Models;

public sealed record CorpusDocument(
    string Path,
    string Name,
    string Content
);