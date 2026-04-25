using TextSpeculator.Core.Models;
using TextSpeculator.Core.Services;
using Xunit;

namespace TextSpeculator.Tests;

public class EngineTests
{
    [Fact]
    public void Suggest_AccentInsensitive_MatchesCorrectly()
    {
        var segments = new[]
        {
            new IndexedSegment(
                "doc1",
                "El ni\u00F1o juega.",
                new[] { "El", "ni\u00F1o", "juega", "." },
                new[] { "el", "nino", "juega" })
        };

        var engine = new SpeculationEngine(segments);

        var suggestions = engine.Suggest("El nin");
        Assert.NotEmpty(suggestions);
        Assert.Equal("ni\u00F1o juega", suggestions[0].Text);
    }

    [Fact]
    public void Suggest_AfterWhitespace_ReturnsNextWords()
    {
        var segments = new[]
        {
            new IndexedSegment(
                "doc1",
                "El ni\u00F1o juega en el parque.",
                new[] { "El", "ni\u00F1o", "juega", "en", "el", "parque", "." },
                new[] { "el", "nino", "juega", "en", "el", "parque" })
        };

        var engine = new SpeculationEngine(segments);

        var suggestions = engine.Suggest("El ni\u00F1o ");
        Assert.NotEmpty(suggestions);
        Assert.Equal("juega en el parque", suggestions[0].Text);
    }

    [Fact]
    public void Suggest_IgnoresPunctuationWhenFindingContinuation()
    {
        var segments = new[]
        {
            new IndexedSegment(
                "doc1",
                "Hola, mundo brillante.",
                new[] { "Hola", ",", "mundo", "brillante", "." },
                new[] { "hola", "mundo", "brillante" })
        };

        var engine = new SpeculationEngine(segments);

        var suggestions = engine.Suggest("Hola mu");
        Assert.NotEmpty(suggestions);
        Assert.Equal("mundo brillante", suggestions[0].Text);
    }

    [Fact]
    public void Suggest_UsesRecentContextWindow_ForLongDrafts()
    {
        var segments = new[]
        {
            new IndexedSegment(
                "doc1",
                "a b c d alpha beta gamma.",
                new[] { "a", "b", "c", "d", "alpha", "beta", "gamma", "." },
                new[] { "a", "b", "c", "d", "alpha", "beta", "gamma" })
        };

        var engine = new SpeculationEngine(segments);

        var suggestions = engine.Suggest("noise words a b c d alpha beta ga");
        Assert.NotEmpty(suggestions);
        Assert.Equal("gamma", suggestions[0].Text);
    }
}
