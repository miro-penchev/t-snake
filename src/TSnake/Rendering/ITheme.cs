using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>24-bit RGB color for truecolor SGR sequences.</summary>
public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>
/// One painted board cell: the exact text to emit (always <see cref="ITheme.CellWidth"/>
/// columns wide) plus optional colors. ASCII mode leaves both colors null.
/// </summary>
public readonly record struct GlyphCell(string Glyph, Rgb? Foreground = null, Rgb? Background = null);

/// <summary>The six box-drawing pieces used to draw the board frame.</summary>
public readonly record struct FrameStyle(
    string TopLeft,
    string TopRight,
    string BottomLeft,
    string BottomRight,
    string Horizontal,
    string Vertical);

/// <summary>
/// A render theme: a pure lookup table from semantic cell content to glyphs and colors
/// (plus the frame style and HUD color). All the cursor-positioning, batching, and redraw
/// machinery lives in <see cref="FrameComposer"/> and is shared across themes (plan §2.4).
/// </summary>
public interface ITheme
{
    /// <summary>Terminal columns per board cell — 2, for a roughly square aspect (plan §2.3).</summary>
    int CellWidth { get; }

    /// <summary>Glyph + colors for a cell, given whether it is this tick's fatal collision cell.</summary>
    GlyphCell Cell(CellKind kind, bool isCollision);

    /// <summary>The frame border pieces.</summary>
    FrameStyle Frame { get; }

    /// <summary>HUD / frame text color, or null for the default terminal palette (ASCII mode).</summary>
    Rgb? HudColor { get; }
}
