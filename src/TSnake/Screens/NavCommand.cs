namespace TSnake.Screens;

/// <summary>
/// A navigation intent produced from a raw keystroke by <see cref="ScreenInput"/> (plan 07 §2.3).
/// Screens wait for one of these and act; it is the blocking, menu-side analogue of the gameplay
/// <c>InputCommand</c>. Arrows and WASD both fold into the four directions; Enter/Space are
/// <see cref="Confirm"/>; Esc is <see cref="Back"/>.
/// </summary>
public enum NavCommand
{
    None,
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Back,
}
