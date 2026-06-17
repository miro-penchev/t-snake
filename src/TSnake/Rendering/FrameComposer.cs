using System.Text;
using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// Turns engine state into the exact ANSI string for a frame — and nothing else. Pure: no
/// <see cref="Console"/> calls, so the produced escape sequence is fully assertable in tests
/// (plan §2.5). A separate thin writer (in <see cref="ConsoleRenderer"/>) issues the single
/// batched write per frame (plan §2.2).
/// </summary>
public sealed class FrameComposer(BoardGeometry geometry, ITheme theme)
{
    private readonly BoardGeometry _geo = geometry;
    private readonly ITheme _theme = theme;

    /// <summary>
    /// A full frame from a snapshot: the border, every board cell, and the HUD line — used by
    /// <c>Begin</c> and by every <c>Redraw</c> (start, resume-from-pause, resize, recovery).
    /// O(width × height).
    /// </summary>
    public string ComposeFull(GameState state, string modeLabel)
    {
        var sb = new StringBuilder();

        AppendFrame(sb);

        // Whole board as Empty first, then overlay occupied cells (obstacles, food, snake).
        for (int y = 0; y < _geo.BoardHeight; y++)
        {
            for (int x = 0; x < _geo.BoardWidth; x++)
            {
                AppendCell(sb, new Point(x, y), CellKind.Empty, isCollision: false);
            }
        }

        foreach (Point obstacle in state.Obstacles)
        {
            AppendCell(sb, obstacle, CellKind.Obstacle, isCollision: false);
        }

        AppendCell(sb, state.Food, CellKind.Food, isCollision: false);

        // Body last-to-first so the head is painted on top of any overlap.
        IReadOnlyList<Point> snake = state.SnakeCells;
        for (int i = snake.Count - 1; i >= 0; i--)
        {
            AppendCell(sb, snake[i], i == 0 ? CellKind.SnakeHead : CellKind.SnakeBody, isCollision: false);
        }

        AppendHud(sb, state.Score, modeLabel);
        sb.Append(Ansi.Reset);
        return sb.ToString();
    }

    /// <summary>
    /// The per-tick fast path: paint exactly the changed cells, plus the HUD when
    /// <paramref name="hudScore"/> is supplied (the score changed), plus the collision marker
    /// when the game ended this tick.
    /// </summary>
    public string ComposeDelta(TickResult tick, int? hudScore, string modeLabel)
    {
        var sb = new StringBuilder();

        foreach (CellChange change in tick.Changes)
        {
            AppendCell(sb, change.Cell, change.Kind, isCollision: false);
        }

        if (tick.EndReason != EndReason.None && tick.CollisionCell is { } cell)
        {
            AppendCell(sb, cell, CellKind.SnakeHead, isCollision: true);
        }

        if (hudScore is { } score)
        {
            AppendHud(sb, score, modeLabel);
        }

        sb.Append(Ansi.Reset);
        return sb.ToString();
    }

    /// <summary>
    /// A small centered "PAUSED" box drawn over the board. Resuming repaints the covered cells
    /// via <c>Redraw</c>, so nothing under the box needs to be remembered (plan §6).
    /// </summary>
    public string ComposePaused()
    {
        const string label = "PAUSED";
        FrameStyle f = _theme.Frame;
        int innerCols = _geo.BoardWidth * BoardGeometry.CellWidth;
        int boxInner = label.Length + 2;                 // one space of padding each side
        int startCol = _geo.OriginCol + 1 + Math.Max(0, (innerCols - (boxInner + 2)) / 2);
        int midRow = _geo.OriginRow + 1 + _geo.BoardHeight / 2;

        var sb = new StringBuilder();
        if (_theme.HudColor is { } c)
        {
            sb.Append(Ansi.Foreground(c));
        }

        sb.Append(Ansi.MoveTo(midRow - 1, startCol)).Append(f.TopLeft);
        AppendRepeat(sb, f.Horizontal, boxInner);
        sb.Append(f.TopRight);

        sb.Append(Ansi.MoveTo(midRow, startCol)).Append(f.Vertical).Append(' ').Append(label).Append(' ').Append(f.Vertical);

        sb.Append(Ansi.MoveTo(midRow + 1, startCol)).Append(f.BottomLeft);
        AppendRepeat(sb, f.Horizontal, boxInner);
        sb.Append(f.BottomRight);

        sb.Append(Ansi.Reset);
        return sb.ToString();
    }

    private void AppendCell(StringBuilder sb, Point cell, CellKind kind, bool isCollision)
    {
        (int row, int col) = _geo.CellToConsole(cell);
        GlyphCell g = _theme.Cell(kind, isCollision);

        sb.Append(Ansi.MoveTo(row, col));
        if (g.Background is { } bg)
        {
            sb.Append(Ansi.Background(bg));
        }

        if (g.Foreground is { } fg)
        {
            sb.Append(Ansi.Foreground(fg));
        }

        sb.Append(g.Glyph);

        // Reset after a colored cell so colors never bleed into the next move.
        if (g.Foreground is not null || g.Background is not null)
        {
            sb.Append(Ansi.Reset);
        }
    }

    private void AppendFrame(StringBuilder sb)
    {
        FrameStyle f = _theme.Frame;
        int innerCols = _geo.BoardWidth * BoardGeometry.CellWidth;
        int rightCol = _geo.OriginCol + 1 + innerCols;
        Rgb? color = _theme.HudColor;

        if (color is { } c)
        {
            sb.Append(Ansi.Foreground(c));
        }

        // Top border.
        sb.Append(Ansi.MoveTo(_geo.OriginRow, _geo.OriginCol)).Append(f.TopLeft);
        AppendRepeat(sb, f.Horizontal, innerCols);
        sb.Append(f.TopRight);

        // Left and right vertical borders.
        for (int y = 0; y < _geo.BoardHeight; y++)
        {
            int row = _geo.OriginRow + 1 + y;
            sb.Append(Ansi.MoveTo(row, _geo.OriginCol)).Append(f.Vertical);
            sb.Append(Ansi.MoveTo(row, rightCol)).Append(f.Vertical);
        }

        // Bottom border.
        sb.Append(Ansi.MoveTo(_geo.OriginRow + _geo.FrameHeight - 1, _geo.OriginCol)).Append(f.BottomLeft);
        AppendRepeat(sb, f.Horizontal, innerCols);
        sb.Append(f.BottomRight);

        if (color is not null)
        {
            sb.Append(Ansi.Reset);
        }
    }

    private void AppendHud(StringBuilder sb, int score, string modeLabel)
    {
        sb.Append(Ansi.MoveTo(_geo.HudRow, _geo.HudCol));
        if (_theme.HudColor is { } c)
        {
            sb.Append(Ansi.Foreground(c));
        }

        sb.Append($" Score: {score,-6}  Mode: {modeLabel} ");

        if (_theme.HudColor is not null)
        {
            sb.Append(Ansi.Reset);
        }
    }

    private static void AppendRepeat(StringBuilder sb, string piece, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sb.Append(piece);
        }
    }
}
