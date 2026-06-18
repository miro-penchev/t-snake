using TSnake.Screens;

namespace TSnake.Tests;

public class NameEntryModelTests
{
    [Fact]
    public void StartsEmptyByDefault()
    {
        var entry = new NameEntryModel();
        Assert.Equal(string.Empty, entry.Value);
        Assert.Equal(0, entry.Length);
    }

    [Fact]
    public void SeedsFromTheInitialValue()
    {
        var entry = new NameEntryModel("Ada");
        Assert.Equal("Ada", entry.Value);
    }

    [Fact]
    public void AppendsPrintableCharacters()
    {
        var entry = new NameEntryModel();
        entry.Append('A');
        entry.Append('b');
        Assert.Equal("Ab", entry.Value);
    }

    [Fact]
    public void IgnoresControlCharacters()
    {
        var entry = new NameEntryModel();
        entry.Append('A');
        entry.Append('\n');
        entry.Append('\t');
        entry.Append('\0');
        Assert.Equal("A", entry.Value);
    }

    [Fact]
    public void BackspaceRemovesTheLastCharacter()
    {
        var entry = new NameEntryModel("Grace");
        entry.Backspace();
        Assert.Equal("Grac", entry.Value);
    }

    [Fact]
    public void BackspaceOnEmptyIsHarmless()
    {
        var entry = new NameEntryModel();
        entry.Backspace();
        Assert.Equal(string.Empty, entry.Value);
    }

    [Fact]
    public void CapsLengthAtTheMaximum()
    {
        var entry = new NameEntryModel();
        for (int i = 0; i < NameEntryModel.MaxLength + 10; i++)
        {
            entry.Append('x');
        }

        Assert.Equal(NameEntryModel.MaxLength, entry.Length);
    }

    [Fact]
    public void SeedingOverLengthIsAlsoCapped()
    {
        var entry = new NameEntryModel(new string('y', NameEntryModel.MaxLength + 5));
        Assert.Equal(NameEntryModel.MaxLength, entry.Length);
    }
}
