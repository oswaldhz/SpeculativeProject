namespace TextSpeculator.Core.Models;

public sealed record IndexedSegment(
    string DocumentName,
    string OriginalText,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> NormalizedTokens
);