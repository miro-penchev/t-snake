namespace TSnake.Core;

/// <summary>
/// A read-only snapshot the renderer reads <i>once</i> to draw the initial frame.
/// Thereafter it paints only the cells in <see cref="TickResult.Changes"/>.
/// </summary>
public sealed class GameState
{
    internal GameState(
        int width,
        int height,
        IReadOnlyList<Point> snakeCells,
        Point food,
        IReadOnlyCollection<Point> obstacles,
        int score,
        GameStatus status,
        int tickCount,
        int foodEaten)
    {
        Width = width;
        Height = height;
        SnakeCells = snakeCells;
        Food = food;
        Obstacles = obstacles;
        Score = score;
        Status = status;
        TickCount = tickCount;
        FoodEaten = foodEaten;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Snake cells, head first.</summary>
    public IReadOnlyList<Point> SnakeCells { get; }

    public Point Food { get; }
    public IReadOnlyCollection<Point> Obstacles { get; }
    public int Score { get; }
    public GameStatus Status { get; }
    public int TickCount { get; }

    /// <summary>How many foods have been eaten this run — the speed-up driver (the classic ramp).</summary>
    public int FoodEaten { get; }
}
