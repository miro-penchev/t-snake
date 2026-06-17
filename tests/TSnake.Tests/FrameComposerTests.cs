using TSnake.Core;
using TSnake.Rendering;

namespace TSnake.Tests;

public class FrameComposerTests
{
    private static readonly BoardGeometry Geo = new(BoardWidth: 10, BoardHeight: 8, OriginRow: 1, OriginCol: 1);

    private static FrameComposer Ascii() => new(Geo, new AsciiMonochromeTheme());

    [Fact]
    public void DeltaPaintsExactlyTheChangedCellsAtTheRightPosition()
    {
        var tick = new TickResult(
            [new CellChange(new Point(1, 0), CellKind.SnakeHead)],
            EndReason.None,
            CollisionCell: null);

        string s = Ascii().ComposeDelta(tick, hudScore: null, modeLabel: "x");

        Assert.Contains("\x1b[2;4H", s); // (x=1,y=0) -> row 2, col 4 (two-wide cell)
        Assert.Contains("@ ", s);        // ASCII head glyph
    }

    [Fact]
    public void DeltaOmitsHudWhenScoreUnchanged()
    {
        var tick = new TickResult(
            [new CellChange(new Point(0, 0), CellKind.SnakeBody)],
            EndReason.None,
            null);

        string s = Ascii().ComposeDelta(tick, hudScore: null, modeLabel: "x");

        Assert.DoesNotContain("Score", s);
    }

    [Fact]
    public void DeltaWritesHudWhenScoreSupplied()
    {
        var tick = new TickResult([], EndReason.None, null);

        string s = Ascii().ComposeDelta(tick, hudScore: 30, modeLabel: "Pro");

        Assert.Contains("\x1b[11;1H", s); // HUD row/col
        Assert.Contains("Score: 30", s);
        Assert.Contains("Mode: Pro", s);
    }

    [Fact]
    public void DeltaPaintsTheCollisionMarkerWhenTheGameEnds()
    {
        var tick = new TickResult([], EndReason.HitSelf, new Point(2, 3));

        string s = Ascii().ComposeDelta(tick, hudScore: null, modeLabel: "x");

        Assert.Contains("\x1b[5;6H", s); // (x=2,y=3) -> row 5, col 6
        Assert.Contains("X ", s);        // ASCII collision glyph
    }

    [Fact]
    public void AsciiDeltaEmitsNoTruecolorCodes()
    {
        var tick = new TickResult(
            [new CellChange(new Point(0, 0), CellKind.Food)],
            EndReason.None,
            null);

        string s = Ascii().ComposeDelta(tick, hudScore: 10, modeLabel: "x");

        Assert.DoesNotContain("38;2", s); // no truecolor foreground
        Assert.DoesNotContain("48;2", s); // no truecolor background
    }

    [Fact]
    public void UnicodeDeltaEmitsWellFormedTruecolorSgr()
    {
        var composer = new FrameComposer(Geo, new UnicodeColorTheme());
        var tick = new TickResult(
            [new CellChange(new Point(0, 0), CellKind.SnakeHead)],
            EndReason.None,
            null);

        string s = composer.ComposeDelta(tick, hudScore: null, modeLabel: "x");

        Assert.Contains("\x1b[38;2;", s); // truecolor foreground SGR
        Assert.Contains("\x1b[48;2;", s); // truecolor background SGR
        Assert.Contains("██", s);
    }

    [Fact]
    public void FullFramePaintsBorderSnakeFoodAndHud()
    {
        // A real snapshot from the engine: 5x3 board, single-cell snake at (2,1).
        // FakeRandom(0) puts the first food at the first free cell, (0,0).
        var config = new GameConfig(
            Width: 5, Height: 3, BasePointsPerFood: 10, GrowthPerFood: 1,
            DifficultyMultiplier: 1, LevelMultiplier: 1, ObstaclesEnabled: false,
            ObstacleSpawnRate: 0, ObstacleLifetimeTicks: 0, MaxObstacles: 0,
            InitialSnakeLength: 1, StartPosition: new Point(2, 1), Seed: 0);
        var engine = new GameEngine(config, new FakeRandom(0));
        GameState state = engine.State;

        var composer = new FrameComposer(
            new BoardGeometry(state.Width, state.Height, OriginRow: 1, OriginCol: 1),
            new AsciiMonochromeTheme());

        string s = composer.ComposeFull(state, modeLabel: "Easy");

        Assert.Contains("\x1b[1;1H+", s);  // top-left frame corner
        Assert.Contains("\x1b[3;6H@ ", s); // head at (2,1) -> row 3, col 6
        Assert.Contains("\x1b[2;2H* ", s); // food at (0,0) -> row 2, col 2
        Assert.Contains("Score: 0", s);
        Assert.Contains("Mode: Easy", s);
    }
}
