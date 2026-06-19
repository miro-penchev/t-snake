using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// Pure mapping from board coordinates to 1-based terminal (row, col), plus frame and HUD
/// placement. No <see cref="Console"/> calls — fully unit-testable.
/// <see cref="OriginRow"/> / <see cref="OriginCol"/> are the 1-based terminal position of the
/// frame's top-left corner; the playable cells sit one row/column inside it.
/// </summary>
public readonly record struct BoardGeometry(int BoardWidth, int BoardHeight, int OriginRow, int OriginCol)
{
    /// <summary>Terminal columns per board cell (plan §2.3). Shared by both themes.</summary>
    public const int CellWidth = 2;

    /// <summary>Frame width in columns, including both vertical borders.</summary>
    public int FrameWidth => BoardWidth * CellWidth + 2;

    /// <summary>Frame height in rows, including the top and bottom borders.</summary>
    public int FrameHeight => BoardHeight + 2;

    /// <summary>1-based row of the HUD line — one row below the bottom border.</summary>
    public int HudRow => OriginRow + FrameHeight;

    /// <summary>1-based column where the HUD text starts (aligned with the frame).</summary>
    public int HudCol => OriginCol;

    /// <summary>Maps a board cell to the 1-based terminal (row, col) of its left column.</summary>
    public (int Row, int Col) CellToConsole(Point cell) =>
        (OriginRow + 1 + cell.Y, OriginCol + 1 + cell.X * CellWidth);

    /// <summary>
    /// The Euclidean distance (in board cells) from <paramref name="origin"/> to the farthest
    /// corner — i.e. how many expanding rings the game-over blast needs to cover the whole board.
    /// Distance is measured in board-cell units (cells are ~square at two columns each), so the
    /// blast reads as a circle rather than an ellipse.
    /// </summary>
    public int MaxRadiusFrom(Point origin)
    {
        int dx = Math.Max(origin.X, BoardWidth - 1 - origin.X);
        int dy = Math.Max(origin.Y, BoardHeight - 1 - origin.Y);
        return (int)Math.Ceiling(Math.Sqrt((double)dx * dx + (double)dy * dy));
    }

    /// <summary>
    /// Total rows the whole scene occupies: frame plus the HUD line. Used by the startup
    /// minimum-size check and to center the scene.
    /// </summary>
    public static int TotalRows(int boardHeight) => boardHeight + 2 + 1;

    /// <summary>Total columns the whole scene occupies (just the frame width).</summary>
    public static int TotalCols(int boardWidth) => boardWidth * CellWidth + 2;

    /// <summary>Centers the scene in a terminal of the given size; origin is clamped to ≥ 1.</summary>
    public static BoardGeometry Centered(int boardWidth, int boardHeight, int terminalCols, int terminalRows)
    {
        int originCol = Math.Max(1, (terminalCols - TotalCols(boardWidth)) / 2 + 1);
        int originRow = Math.Max(1, (terminalRows - TotalRows(boardHeight)) / 2 + 1);
        return new BoardGeometry(boardWidth, boardHeight, originRow, originCol);
    }
}
