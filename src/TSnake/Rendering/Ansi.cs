namespace TSnake.Rendering;

/// <summary>
/// Minimal ANSI / VT escape-sequence builders. Pure strings — no <see cref="Console"/> calls —
/// so <see cref="FrameComposer"/> and its tests can use them freely. <see cref="Esc"/> already
/// includes the Control Sequence Introducer (<c>ESC [</c>).
/// </summary>
public static class Ansi
{
    /// <summary>Control Sequence Introducer: <c>ESC [</c>.</summary>
    public const string Esc = "\x1b[";

    public const string Reset = $"{Esc}0m";
    public const string HideCursor = $"{Esc}?25l";
    public const string ShowCursor = $"{Esc}?25h";
    public const string EnterAltScreen = $"{Esc}?1049h";
    public const string LeaveAltScreen = $"{Esc}?1049l";
    public const string ClearScreen = $"{Esc}2J";

    /// <summary>Cursor move to a 1-based (row, col).</summary>
    public static string MoveTo(int row, int col) => $"{Esc}{row};{col}H";

    /// <summary>24-bit truecolor foreground SGR.</summary>
    public static string Foreground(Rgb c) => $"{Esc}38;2;{c.R};{c.G};{c.B}m";

    /// <summary>24-bit truecolor background SGR.</summary>
    public static string Background(Rgb c) => $"{Esc}48;2;{c.R};{c.G};{c.B}m";
}
