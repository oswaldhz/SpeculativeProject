namespace TextSpeculator.Core.Models;

public sealed record SpeculationSuggestion(
    string Text,
    string Preview,
    string SourceDocument,
    int Score
);