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
            new IndexedSegment("doc1", "El niño juega.",
                new[] { "El", "niño", "juega", "." },
                new[] { "el", "nino", "juega" })
        };
        var engine = new SpeculationEngine(segments);

        var suggestions = engine.Suggest("El nin");
        Assert.NotEmpty(suggestions);
        // Lookup is accent-insensitive, but returned snippets preserve original corpus text from raw tokens.
        Assert.Equal("niño juega", suggestions[0].Text);
    }
}
