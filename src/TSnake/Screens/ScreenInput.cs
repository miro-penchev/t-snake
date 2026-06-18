using System.Threading.Channels;
using TSnake.Input;

namespace TSnake.Screens;

/// <summary>
/// The blocking, screen-side adapter over the <i>shared</i> <see cref="KeyboardReader"/> channel
/// (plan 07 §2.3). Where gameplay polls the same channel non-blockingly, screens <i>wait</i> for a
/// key and then act. Two read modes: <see cref="ReadCommand"/> maps a keystroke to a
/// <see cref="NavCommand"/> for menu navigation, and <see cref="ReadTextKey"/> returns the raw key
/// for name entry. Only one consumer (menu or game) touches the channel at a time, and the composition
/// root flushes it on every transition, so reads never cross phases.
/// </summary>
public sealed class ScreenInput(KeyboardReader reader)
{
    /// <summary>
    /// Blocks until a key arrives and maps it to a navigation command. A closed channel (redirected
    /// or EOF input) reads as <see cref="NavCommand.Back"/> so a screen can never hang forever.
    /// </summary>
    public NavCommand ReadCommand() => Map(ReadTextKey());

    /// <summary>
    /// Blocks until a key arrives and returns it raw, for the name-entry text mode (printable chars,
    /// Backspace, Enter, Esc). A closed channel yields an Escape so callers fall back to cancelling.
    /// </summary>
    public ConsoleKeyInfo ReadTextKey()
    {
        try
        {
            return reader.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (ChannelClosedException)
        {
            return new ConsoleKeyInfo('\0', ConsoleKey.Escape, shift: false, alt: false, control: false);
        }
    }

    private static NavCommand Map(ConsoleKeyInfo info) => info.Key switch
    {
        ConsoleKey.W or ConsoleKey.UpArrow => NavCommand.Up,
        ConsoleKey.S or ConsoleKey.DownArrow => NavCommand.Down,
        ConsoleKey.A or ConsoleKey.LeftArrow => NavCommand.Left,
        ConsoleKey.D or ConsoleKey.RightArrow => NavCommand.Right,
        ConsoleKey.Enter or ConsoleKey.Spacebar => NavCommand.Confirm,
        ConsoleKey.Escape => NavCommand.Back,
        _ => NavCommand.None,
    };
}
