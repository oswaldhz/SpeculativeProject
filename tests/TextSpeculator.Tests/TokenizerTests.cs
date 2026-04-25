using TextSpeculator.Core.Processing;
using Xunit;

namespace TextSpeculator.Tests;

public class TokenizerTests
{
    [Fact]
    public void Tokenize_AccentedWord_ReturnsTokens()
    {
        var tokens = TextTokenizer.Tokenize("caf\u00E9, ni\u00F1o.");
        Assert.Equal(new[] { "caf\u00E9", ",", "ni\u00F1o", "." }, tokens);
    }

    [Fact]
    public void Normalize_RemovesDiacritics()
    {
        var norm = TextTokenizer.Normalize("Caf\u00E9");
        Assert.Equal("cafe", norm);
    }

    [Fact]
    public void IsWord_RecognizesAccentedLetters()
    {
        Assert.True(TextTokenizer.IsWord("ni\u00F1o"));
        Assert.True(TextTokenizer.IsWord("l'avion"));
        Assert.False(TextTokenizer.IsWord("."));
    }

    [Fact]
    public void Normalize_ConvertsCurlyApostrophes()
    {
        var norm = TextTokenizer.Normalize("l\u2019avion");
        Assert.Equal("l'avion", norm);
    }
}
