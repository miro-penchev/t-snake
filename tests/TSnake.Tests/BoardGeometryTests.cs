using TSnake.Core;
using TSnake.Rendering;

namespace TSnake.Tests;

public class BoardGeometryTests
{
    [Fact]
    public void TopLeftCellSitsOneInsideTheFrameOrigin()
    {
        var g = new BoardGeometry(BoardWidth: 10, BoardHeight: 8, OriginRow: 1, OriginCol: 1);
        Assert.Equal((2, 2), g.CellToConsole(new Point(0, 0)));
    }

    [Fact]
    public void CellsAreTwoColumnsWide()
    {
        var g = new BoardGeometry(10, 8, OriginRow: 1, OriginCol: 1);

        // X advances by two columns; Y advances by one row.
        Assert.Equal((2, 4), g.CellToConsole(new Point(1, 0)));
        Assert.Equal((3, 2), g.CellToConsole(new Point(0, 1)));
    }

    [Fact]
    public void BottomRightInteriorCellMapsCorrectly()
    {
        var g = new BoardGeometry(10, 8, OriginRow: 1, OriginCol: 1);

        // col = 1 + 1 + 9*2 = 20 ; row = 1 + 1 + 7 = 9
        Assert.Equal((9, 20), g.CellToConsole(new Point(9, 7)));
    }

    [Fact]
    public void OriginOffsetShiftsEveryCell()
    {
        var g = new BoardGeometry(10, 8, OriginRow: 5, OriginCol: 3);
        Assert.Equal((6, 4), g.CellToConsole(new Point(0, 0)));
    }

    [Fact]
    public void FrameAndHudDimensionsIncludeBorders()
    {
        var g = new BoardGeometry(10, 8, OriginRow: 1, OriginCol: 1);

        Assert.Equal(22, g.FrameWidth);  // 10*2 + 2 borders
        Assert.Equal(10, g.FrameHeight); // 8 + top + bottom
        Assert.Equal(11, g.HudRow);      // origin + frame height
        Assert.Equal(1, g.HudCol);
    }

    [Fact]
    public void CenteredKeepsTheSceneOnScreen()
    {
        var g = BoardGeometry.Centered(boardWidth: 10, boardHeight: 8, terminalCols: 80, terminalRows: 24);

        Assert.True(g.OriginRow >= 1);
        Assert.True(g.OriginCol >= 1);
        Assert.True(g.OriginCol + g.FrameWidth - 1 <= 80);
        Assert.True(g.HudRow <= 24);
    }

    [Fact]
    public void CenteredClampsOriginToOneOnATinyTerminal()
    {
        var g = BoardGeometry.Centered(boardWidth: 40, boardHeight: 30, terminalCols: 10, terminalRows: 5);

        Assert.Equal(1, g.OriginRow);
        Assert.Equal(1, g.OriginCol);
    }
}
