namespace TSnake.Input;

/// <summary>
/// A semantic command produced by <see cref="KeyMap"/> from a raw keystroke. Turns are the four
/// headings; <see cref="Pause"/> and <see cref="Quit"/> are reported as commands — whether they're
/// honored is the game loop's decision, not input's (plan §1, §7).
/// </summary>
public enum InputCommand
{
    None,
    TurnUp,
    TurnDown,
    TurnLeft,
    TurnRight,
    Pause,
    Quit,
}
