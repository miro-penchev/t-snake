namespace TSnake.Input;

/// <summary>
/// Pure mapping from a raw <see cref="ConsoleKeyInfo"/> to an <see cref="InputCommand"/> (plan §4).
/// <see cref="ConsoleKey"/> already folds letter case (<c>w</c> and <c>W</c> both arrive as
/// <see cref="ConsoleKey.W"/>), so WASD works regardless of Shift. Quit is <see cref="ConsoleKey.Escape"/>
/// only — <c>Q</c> sits too close to WASD to risk as an accidental-exit key.
/// </summary>
public static class KeyMap
{
    public static InputCommand Map(ConsoleKeyInfo info) => info.Key switch
    {
        ConsoleKey.W or ConsoleKey.UpArrow => InputCommand.TurnUp,
        ConsoleKey.S or ConsoleKey.DownArrow => InputCommand.TurnDown,
        ConsoleKey.A or ConsoleKey.LeftArrow => InputCommand.TurnLeft,
        ConsoleKey.D or ConsoleKey.RightArrow => InputCommand.TurnRight,
        ConsoleKey.Spacebar => InputCommand.Pause,
        ConsoleKey.Escape => InputCommand.Quit,
        _ => InputCommand.None,
    };
}
