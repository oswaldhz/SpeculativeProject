using TextSpeculator.Core.Processing;
using Xunit;

namespace TextSpeculator.Tests;

public class TokenizerTests
{
    [Fact]
    public void Tokenize_AccentedWord_ReturnsTokens()
    {
        var tokens = TextTokenizer.Tokenize("café, niño.");
        Assert.Equal(new[] { "café", ",", "niño", "." }, tokens);
    }

    [Fact]
    public void Normalize_RemovesDiacritics()
    {
        var norm = TextTokenizer.Normalize("Café");
        Assert.Equal("cafe", norm);
    }

    [Fact]
    public void IsWord_RecognizesAccentedLetters()
    {
        Assert.True(TextTokenizer.IsWord("niño"));
        Assert.True(TextTokenizer.IsWord("l'avion"));
        Assert.False(TextTokenizer.IsWord("."));
    }
}