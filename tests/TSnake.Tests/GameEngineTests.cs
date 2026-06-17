using TSnake.Core;

namespace TSnake.Tests;

public class GameEngineTests
{
    /// <summary>Builds a config with sensible test defaults; override only what a test cares about.</summary>
    private static GameConfig Config(
        int width = 10,
        int height = 10,
        int basePoints = 10,
        int growth = 1,
        int difficulty = 1,
        int level = 1,
        bool obstacles = false,
        int spawnRate = 0,
        int lifetime = 0,
        int maxObstacles = 0,
        int snakeLength = 1,
        Point? start = null,
        int seed = 0)
        => new(
            width, height, basePoints, growth, difficulty, level,
            obstacles, spawnRate, lifetime, maxObstacles,
            snakeLength, start ?? new Point(width / 2, height / 2), seed);

    private static Point Head(GameEngine engine) => engine.State.SnakeCells[0];

    // ---- Movement ---------------------------------------------------------

    [Fact]
    public void MovesRightByOneCell()
    {
        var engine = new GameEngine(Config(start: new Point(5, 5)), new FakeRandom(0));
        engine.SetDirection(Direction.Right);
        engine.Tick();
        Assert.Equal(new Point(6, 5), Head(engine));
    }

    [Fact]
    public void MovesDownByOneCell()
    {
        var engine = new GameEngine(Config(start: new Point(5, 5)), new FakeRandom(0));
        engine.SetDirection(Direction.Down);
        engine.Tick();
        Assert.Equal(new Point(5, 6), Head(engine));
    }

    [Fact]
    public void MovesUpByOneCell()
    {
        var engine = new GameEngine(Config(start: new Point(5, 5)), new FakeRandom(0));
        engine.SetDirection(Direction.Up);
        engine.Tick();
        Assert.Equal(new Point(5, 4), Head(engine));
    }

    [Fact]
    public void MovesLeftByOneCell()
    {
        // Start heading Right, so reach a Left move via an intermediate Up turn.
        var engine = new GameEngine(Config(start: new Point(5, 5)), new FakeRandom(0));
        engine.SetDirection(Direction.Up);
        engine.Tick();
        engine.SetDirection(Direction.Left);
        engine.Tick();
        Assert.Equal(new Point(4, 4), Head(engine));
    }

    // ---- Wrap-around ------------------------------------------------------

    [Fact]
    public void WrapsAtRightEdge()
    {
        var engine = new GameEngine(Config(5, 5, start: new Point(4, 2)), new FakeRandom(0));
        engine.SetDirection(Direction.Right);
        engine.Tick();
        Assert.Equal(new Point(0, 2), Head(engine));
    }

    [Fact]
    public void WrapsAtBottomEdge()
    {
        var engine = new GameEngine(Config(5, 5, start: new Point(2, 4)), new FakeRandom(0));
        engine.SetDirection(Direction.Down);
        engine.Tick();
        Assert.Equal(new Point(2, 0), Head(engine));
    }

    [Fact]
    public void WrapsAtTopEdge()
    {
        var engine = new GameEngine(Config(5, 5, start: new Point(2, 0)), new FakeRandom(0));
        engine.SetDirection(Direction.Up);
        engine.Tick();
        Assert.Equal(new Point(2, 4), Head(engine));
    }

    [Fact]
    public void WrapsAtLeftEdge()
    {
        // Turn Up first (Left is the reverse of the initial Right heading), then cross the left edge.
        var engine = new GameEngine(Config(5, 5, start: new Point(0, 2)), new FakeRandom(0));
        engine.SetDirection(Direction.Up);
        engine.Tick(); // (0,1)
        engine.SetDirection(Direction.Left);
        engine.Tick(); // x: -1 -> 4
        Assert.Equal(new Point(4, 1), Head(engine));
    }

    // ---- Eating, growth, scoring -----------------------------------------

    [Fact]
    public void EatingGrowsSnakeAndScoresAndRespawnsFood()
    {
        // Head (0,0) heading Right; first free cell is (1,0) -> food directly ahead.
        // After eating, the next free cell is (2,0) -> new food.
        var engine = new GameEngine(
            Config(5, 5, start: new Point(0, 0)),
            new FakeRandom(0, 0));

        engine.SetDirection(Direction.Right);
        engine.Tick();

        GameState state = engine.State;
        Assert.Equal(2, state.SnakeCells.Count);          // grew by 1
        Assert.Equal(10, state.Score);                    // base * 1 * 1
        Assert.Equal(new Point(2, 0), state.Food);        // respawned on a free cell
        Assert.DoesNotContain(state.Food, state.SnakeCells);
    }

    [Fact]
    public void ScoreUsesDifficultyAndLevelMultipliers()
    {
        var engine = new GameEngine(
            Config(5, 5, basePoints: 10, difficulty: 2, level: 3, start: new Point(0, 0)),
            new FakeRandom(0, 0));

        engine.SetDirection(Direction.Right);
        engine.Tick();

        Assert.Equal(60, engine.State.Score); // 10 * 2 * 3
    }

    [Fact]
    public void GrowthPerFoodGreaterThanOneAddsMultipleSegments()
    {
        // Food ahead at (1,5); new food parked at (0,0) so we don't eat again while growing.
        var engine = new GameEngine(
            Config(growth: 3, start: new Point(0, 5)),
            new FakeRandom(50, 0));

        engine.SetDirection(Direction.Right);
        engine.Tick(); // eat at (1,5): length 1 -> 2, owed 2 more
        engine.Tick(); // (2,5): length 3
        engine.Tick(); // (3,5): length 4

        Assert.Equal(4, engine.State.SnakeCells.Count); // start 1 + growth 3
        Assert.Equal(GameStatus.Running, engine.State.Status);
    }

    // ---- Collisions -------------------------------------------------------

    [Fact]
    public void SelfCollisionEndsGame()
    {
        // Length-5 snake turns tightly enough to bite a non-tail segment.
        var engine = new GameEngine(
            Config(snakeLength: 5, start: new Point(5, 5)),
            new FakeRandom(0));

        engine.SetDirection(Direction.Up);
        engine.Tick(); // (5,4)
        engine.SetDirection(Direction.Left);
        engine.Tick(); // (4,4)
        engine.SetDirection(Direction.Down);
        TickResult result = engine.Tick(); // tries (4,5) which is still body

        Assert.Equal(GameStatus.GameOver, engine.Status);
        Assert.Equal(EndReason.HitSelf, result.EndReason);
        Assert.Equal(new Point(4, 5), result.CollisionCell);
        Assert.Equal(new Point(4, 4), Head(engine)); // head did not advance into the crash cell
    }

    [Fact]
    public void FollowingTheVacatingTailIsLegal()
    {
        // Same loop but length 4: the head lands exactly on the tail, which vacates this tick.
        var engine = new GameEngine(
            Config(snakeLength: 4, start: new Point(3, 2)),
            new FakeRandom(0));

        engine.SetDirection(Direction.Up);
        engine.Tick(); // (3,1)
        engine.SetDirection(Direction.Left);
        engine.Tick(); // (2,1)
        engine.SetDirection(Direction.Down);
        TickResult result = engine.Tick(); // (2,2) == vacating tail -> allowed

        Assert.Equal(GameStatus.Running, engine.Status);
        Assert.Equal(EndReason.None, result.EndReason);
        Assert.Equal(new Point(2, 2), Head(engine));
        Assert.Equal(4, engine.State.SnakeCells.Count);
    }

    [Fact]
    public void HittingAnObstacleEndsGame()
    {
        // 5x1 strip keeps free-cell ordering trivial.
        // RNG: [0] initial food at (0,0); [0] tick-1 spawn roll (<100 -> spawn);
        //      [2] obstacle at (4,0) (free list is (1,0),(2,0),(4,0)).
        var engine = new GameEngine(
            Config(5, 1, obstacles: true, spawnRate: 100, lifetime: 1000, maxObstacles: 5,
                   start: new Point(2, 0)),
            new FakeRandom(0, 0, 2));

        engine.SetDirection(Direction.Right);
        engine.Tick(); // (3,0); obstacle spawns at (4,0)
        Assert.Contains(new Point(4, 0), engine.State.Obstacles);

        TickResult result = engine.Tick(); // tries (4,0) -> obstacle

        Assert.Equal(GameStatus.GameOver, engine.Status);
        Assert.Equal(EndReason.HitObstacle, result.EndReason);
        Assert.Equal(new Point(4, 0), result.CollisionCell);
        Assert.Equal(new Point(3, 0), Head(engine)); // did not advance
    }

    // ---- Input policy -----------------------------------------------------

    [Fact]
    public void ReversalInputIsIgnored()
    {
        var engine = new GameEngine(Config(start: new Point(5, 5)), new FakeRandom(0));
        engine.SetDirection(Direction.Left); // opposite of the initial Right heading
        engine.Tick();
        Assert.Equal(new Point(6, 5), Head(engine)); // kept moving Right
        Assert.Equal(GameStatus.Running, engine.Status); // not fatal
    }

    // ---- Terminal states --------------------------------------------------

    [Fact]
    public void BoardFullProducesWon()
    {
        // 2x1 board: eating the only other cell fills the board, leaving nowhere to respawn food.
        var engine = new GameEngine(
            Config(2, 1, start: new Point(0, 0)),
            new FakeRandom(0));

        engine.SetDirection(Direction.Right);
        TickResult result = engine.Tick();

        Assert.Equal(GameStatus.Won, engine.Status);
        Assert.Equal(EndReason.BoardFull, result.EndReason);
        Assert.Equal(2, engine.State.SnakeCells.Count); // fills the 2-cell board
    }

    [Fact]
    public void TickAfterGameOverIsANoOp()
    {
        var engine = new GameEngine(
            Config(snakeLength: 5, start: new Point(5, 5)),
            new FakeRandom(0));

        engine.SetDirection(Direction.Up);
        engine.Tick();
        engine.SetDirection(Direction.Left);
        engine.Tick();
        engine.SetDirection(Direction.Down);
        engine.Tick(); // GameOver

        TickResult after = engine.Tick();
        Assert.Empty(after.Changes);
        Assert.Equal(EndReason.None, after.EndReason);
        Assert.Equal(GameStatus.GameOver, engine.Status);
    }

    // ---- Invariants (property-style) -------------------------------------

    [Fact]
    public void InvariantsHoldOverALongRun()
    {
        // base/diff/level all 1 and growth 1, so length == startLength + score at every tick.
        const int width = 10;
        const int height = 10;
        const int startLength = 3;
        var engine = new GameEngine(
            Config(width, height, basePoints: 1, growth: 1, snakeLength: startLength,
                   start: new Point(2, 0)),
            new SeededRandom(12345));

        int ticks = 0;
        foreach (Direction dir in Lawnmower(width, height, startX: 2))
        {
            engine.SetDirection(dir);
            engine.Tick();
            ticks++;

            GameState state = engine.State;
            if (state.Status != GameStatus.Running)
            {
                break;
            }

            // No duplicate cells.
            Assert.Equal(state.SnakeCells.Count, state.SnakeCells.Distinct().Count());

            // Head always inside the board.
            Point head = state.SnakeCells[0];
            Assert.InRange(head.X, 0, width - 1);
            Assert.InRange(head.Y, 0, height - 1);

            // Length matches food eaten (score) for these multipliers.
            Assert.Equal(startLength + state.Score, state.SnakeCells.Count);
        }

        Assert.True(ticks > 30, $"expected a long run, only ticked {ticks} times");
    }

    /// <summary>
    /// A boustrophedon (lawnmower) sweep over the board: never reverses 180°, so it is a
    /// valid input stream and covers cells so the snake actually eats along the way.
    /// </summary>
    private static IEnumerable<Direction> Lawnmower(int width, int height, int startX)
    {
        int x = startX;
        int y = 0;
        bool goingRight = true;

        while (true)
        {
            if (goingRight && x < width - 1)
            {
                x++;
                yield return Direction.Right;
            }
            else if (!goingRight && x > 0)
            {
                x--;
                yield return Direction.Left;
            }
            else if (y < height - 1)
            {
                y++;
                goingRight = !goingRight;
                yield return Direction.Down;
            }
            else
            {
                yield break;
            }
        }
    }
}
