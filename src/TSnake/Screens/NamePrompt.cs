using TSnake.Persistence;
using TSnake.Rendering;

namespace TSnake.Screens;

/// <summary>
/// The shared name-entry prompt (plan 07 §2.3 text mode), used both to edit the name in the menu and
/// to confirm/edit it when a score qualifies (plan 05 &amp; 06). It drives the pure
/// <see cref="NameEntryModel"/> from the screen-side text channel and applies Persistence's
/// <see cref="HighScoreEntry.Sanitize"/> on commit, so the boundary cleanup stays in one place.
/// </summary>
public sealed class NamePrompt(TextUi ui, ScreenInput input)
{
    /// <summary>
    /// Shows the prompt pre-filled with <paramref name="initial"/>. Returns the sanitized name on
    /// Enter, or <c>null</c> if the player cancelled (Esc) or left it blank.
    /// </summary>
    public string? Prompt(string initial, ITheme theme)
    {
        var entry = new NameEntryModel(initial);

        while (true)
        {
            Draw(entry, theme);
            ConsoleKeyInfo key = input.ReadTextKey();

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    string committed = HighScoreEntry.Sanitize(entry.Value);
                    return committed.Length > 0 ? committed : null;
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.Backspace:
                    entry.Backspace();
                    break;
                default:
                    entry.Append(key.KeyChar);
                    break;
            }
        }
    }

    private void Draw(NameEntryModel entry, ITheme theme)
    {
        string shown = entry.Length > 0 ? entry.Value : "_";
        ui.RenderBox(
            [
                new TextUi.Line("Enter your name"),
                new TextUi.Line(string.Empty),
                new TextUi.Line(shown, Highlight: true),
                new TextUi.Line(string.Empty),
                new TextUi.Line("Enter = save    Esc = cancel"),
            ],
            theme);
    }
}
