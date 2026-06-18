using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// The smooth, colorful look: block / geometric glyphs in 24-bit truecolor. All glyphs are
/// single-width characters doubled to fill the two-column cell, so alignment can never break
/// on a double-width code point (plan §5).
/// </summary>
public sealed class UnicodeColorTheme : ITheme
{
    private static readonly Rgb BoardBg = new(18, 18, 26);
    private static readonly Rgb EmptyDot = new(44, 44, 56);
    private static readonly Rgb Head = new(120, 255, 120);
    private static readonly Rgb Body = new(40, 200, 80);
    private static readonly Rgb FoodRed = new(230, 50, 50);
    private static readonly Rgb ObstacleGray = new(130, 130, 130);
    private static readonly Rgb Collision = new(255, 60, 60);

    public int CellWidth => 2;

    public Rgb? HudColor => new Rgb(200, 200, 210);

    public FrameStyle Frame => new("╔", "╗", "╚", "╝", "═", "║");

    public GlyphCell Cell(CellKind kind, bool isCollision)
    {
        if (isCollision)
        {
            // A single mark padded to the two-column cell — "✖✖" reads as two separate X's.
            return new GlyphCell("✖ ", Collision, BoardBg);
        }

        return kind switch
        {
            CellKind.Empty => new GlyphCell("··", EmptyDot, BoardBg),
            CellKind.SnakeHead => new GlyphCell("██", Head, BoardBg),
            CellKind.SnakeBody => new GlyphCell("██", Body, BoardBg),
            CellKind.Food => new GlyphCell("●●", FoodRed, BoardBg),
            CellKind.Obstacle => new GlyphCell("▓▓", ObstacleGray, BoardBg),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
