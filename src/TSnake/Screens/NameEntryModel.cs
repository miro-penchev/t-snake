using System.Text;
using TSnake.Persistence;

namespace TSnake.Screens;

/// <summary>
/// The pure text buffer behind name entry (plan 07 §4): it accepts printable characters up to the
/// stored-name cap and removes the last one on Backspace. It deliberately does only the in-flight
/// editing rules; the authoritative cleanup (stripping control characters, trimming, capping) is
/// Persistence's <see cref="HighScoreEntry.Sanitize"/>, applied when the entry is built (plan 06).
/// </summary>
public sealed class NameEntryModel
{
    /// <summary>Length cap, matching the persisted-name bound so the buffer can't outgrow the field.</summary>
    public const int MaxLength = HighScoreEntry.MaxNameLength;

    private readonly StringBuilder _buffer = new(MaxLength);

    public NameEntryModel(string? initial = null)
    {
        if (initial is null)
        {
            return;
        }

        foreach (char c in initial)
        {
            Append(c); // reuse the same accept/cap rules for the seed value
        }
    }

    /// <summary>The current text.</summary>
    public string Value => _buffer.ToString();

    /// <summary>The number of characters entered so far.</summary>
    public int Length => _buffer.Length;

    /// <summary>
    /// Appends a printable character, ignoring control characters and any input past
    /// <see cref="MaxLength"/>.
    /// </summary>
    public void Append(char c)
    {
        if (!char.IsControl(c) && _buffer.Length < MaxLength)
        {
            _buffer.Append(c);
        }
    }

    /// <summary>Removes the last character, if any.</summary>
    public void Backspace()
    {
        if (_buffer.Length > 0)
        {
            _buffer.Length--;
        }
    }
}
