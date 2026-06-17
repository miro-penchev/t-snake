using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// The retro look: plain ASCII glyphs, no color at all. Every glyph is two columns wide to
/// match <see cref="UnicodeColorTheme"/>'s geometry (plan §5).
/// </summary>
public sealed class AsciiMonochromeTheme : ITheme
{
    public int CellWidth => 2;

    public Rgb? HudColor => null;

    public FrameStyle Frame => new("+", "+", "+", "+", "-", "|");

    public GlyphCell Cell(CellKind kind, bool isCollision)
    {
        if (isCollision)
        {
            return new GlyphCell("X ");
        }

        return kind switch
        {
            CellKind.Empty => new GlyphCell("  "),
            CellKind.SnakeHead => new GlyphCell("@ "),
            CellKind.SnakeBody => new GlyphCell("o "),
            CellKind.Food => new GlyphCell("* "),
            CellKind.Obstacle => new GlyphCell("# "),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
