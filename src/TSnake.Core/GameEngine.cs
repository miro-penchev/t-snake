namespace TSnake.Core;

/// <summary>
/// The pure, deterministic game core. One <see cref="Tick"/> moves the snake exactly one
/// cell; the engine never measures wall-clock time (speed is the outer loop's job). All
/// randomness flows through the injected <see cref="IRandom"/>, so a given seed plus a
/// fixed sequence of directions reproduces the exact same run.
/// </summary>
public sealed class GameEngine
{
    private readonly GameConfig _config;
    private readonly IRandom _rng;

    // Ordered body (head = First, tail = Last) for O(1) head-add / tail-remove,
    // kept in sync with a set for O(1) "is this cell part of the snake?" lookups.
    private readonly LinkedList<Point> _snake = new();
    private readonly HashSet<Point> _snakeCells = [];

    private readonly HashSet<Point> _obstacles = [];
    private readonly Dictionary<Point, int> _obstacleDespawnTick = [];

    private Point _food;
    private Direction _heading;
    private Direction _pending;
    private int _score;
    private int _tickCount;

    // Segments still owed to the snake from foods with GrowthPerFood > 1.
    private int _pendingGrowth;

    public GameEngine(GameConfig config, IRandom rng)
    {
        _config = config;
        _rng = rng;

        _heading = Direction.Right;
        _pending = Direction.Right;

        InitializeSnake();

        if (!TrySpawnFood(out _food))
        {
            // No room for even the first food: the board is already full.
            Status = GameStatus.Won;
        }
    }

    public GameStatus Status { get; private set; } = GameStatus.Running;

    /// <summary>A fresh read-only snapshot of the board for the renderer's initial frame.</summary>
    public GameState State => new(
        _config.Width,
        _config.Height,
        [.. _snake],
        _food,
        [.. _obstacles],
        _score,
        Status,
        _tickCount);

    /// <summary>Overwrite the single pending-direction slot. The reversal guard is applied in <see cref="Tick"/>.</summary>
    public void SetDirection(Direction next) => _pending = next;

    /// <summary>Advance the simulation by one cell and report what changed.</summary>
    public TickResult Tick()
    {
        if (Status != GameStatus.Running)
        {
            return new TickResult([], EndReason.None, null);
        }

        var changes = new List<CellChange>();

        // 1. Resolve direction: adopt the pending heading unless it would reverse onto our neck.
        if (_pending != _heading.Opposite())
        {
            _heading = _pending;
        }

        // 2. Compute the new head with wrap-around at the edges.
        Point oldHead = _snake.First!.Value;
        Point delta = _heading.Delta();
        Point newHead = new(
            Mod(oldHead.X + delta.X, _config.Width),
            Mod(oldHead.Y + delta.Y, _config.Height));

        bool eating = newHead == _food;
        bool willVacateTail = !eating && _pendingGrowth == 0;
        Point tail = _snake.Last!.Value;

        // 3. Collision checks at the new head cell.
        if (_obstacles.Contains(newHead))
        {
            Status = GameStatus.GameOver;
            return new TickResult(changes, EndReason.HitObstacle, newHead);
        }

        bool hitsBody = _snakeCells.Contains(newHead);
        if (hitsBody && willVacateTail && newHead == tail)
        {
            // Following your own vacating tail is legal — the cell is freed this tick.
            hitsBody = false;
        }

        if (hitsBody)
        {
            Status = GameStatus.GameOver;
            return new TickResult(changes, EndReason.HitSelf, newHead);
        }

        // 4. Resolve the move.
        _snake.AddFirst(newHead);
        _snakeCells.Add(newHead);
        changes.Add(new CellChange(oldHead, CellKind.SnakeBody));
        changes.Add(new CellChange(newHead, CellKind.SnakeHead));

        if (eating)
        {
            _score += _config.BasePointsPerFood * _config.DifficultyMultiplier * _config.LevelMultiplier;
            _pendingGrowth += _config.GrowthPerFood - 1; // the kept tail this tick covers the first segment

            if (!TrySpawnFood(out _food))
            {
                Status = GameStatus.Won;
                _tickCount++;
                return new TickResult(changes, EndReason.BoardFull, null);
            }

            changes.Add(new CellChange(_food, CellKind.Food));
        }
        else if (_pendingGrowth > 0)
        {
            _pendingGrowth--; // grow: keep the tail
        }
        else
        {
            _snake.RemoveLast();
            _snakeCells.Remove(tail);
            changes.Add(new CellChange(tail, CellKind.Empty));
        }

        // 5. Obstacle dynamics (Pro/Terminator). Easy mode disables this entirely.
        if (_config.ObstaclesEnabled)
        {
            UpdateObstacles(changes);
        }

        // 6. Advance the tick counter.
        _tickCount++;

        return new TickResult(changes, EndReason.None, null);
    }

    private void InitializeSnake()
    {
        // Head at the configured start position; body extends to the left (heading is Right).
        for (int i = 0; i < _config.InitialSnakeLength; i++)
        {
            Point cell = new(
                Mod(_config.StartPosition.X - i, _config.Width),
                _config.StartPosition.Y);

            // Guard against a degenerate wrap that would duplicate a cell on a tiny board.
            if (!_snakeCells.Add(cell))
            {
                break;
            }

            _snake.AddLast(cell);
        }
    }

    private void UpdateObstacles(List<CellChange> changes)
    {
        // Despawn expired obstacles first.
        if (_obstacleDespawnTick.Count > 0)
        {
            var expired = new List<Point>();
            foreach (var (cell, despawnAt) in _obstacleDespawnTick)
            {
                if (_tickCount >= despawnAt)
                {
                    expired.Add(cell);
                }
            }

            foreach (Point cell in expired)
            {
                _obstacles.Remove(cell);
                _obstacleDespawnTick.Remove(cell);
                changes.Add(new CellChange(cell, CellKind.Empty));
            }
        }

        // Maybe spawn a new obstacle.
        if (_obstacles.Count < _config.MaxObstacles && _rng.Next(100) < _config.ObstacleSpawnRate)
        {
            if (TryPickFreeCell(excludeFood: true, out Point cell))
            {
                _obstacles.Add(cell);
                _obstacleDespawnTick[cell] = _tickCount + _config.ObstacleLifetimeTicks;
                changes.Add(new CellChange(cell, CellKind.Obstacle));
            }
        }
    }

    // Food spawning never excludes the old food: after eating it sits under the new head
    // (already in the snake set), and at construction there is no prior food.
    private bool TrySpawnFood(out Point food) => TryPickFreeCell(excludeFood: false, out food);

    /// <summary>
    /// Picks a uniformly random cell that is free of the snake and every obstacle (and the
    /// current food when <paramref name="excludeFood"/> is set). Returns false when the board
    /// has no free cell left.
    /// </summary>
    private bool TryPickFreeCell(bool excludeFood, out Point cell)
    {
        var free = new List<Point>(_config.Width * _config.Height);
        for (int y = 0; y < _config.Height; y++)
        {
            for (int x = 0; x < _config.Width; x++)
            {
                Point p = new(x, y);
                if (_snakeCells.Contains(p) || _obstacles.Contains(p) || (excludeFood && p == _food))
                {
                    continue;
                }

                free.Add(p);
            }
        }

        if (free.Count == 0)
        {
            cell = default;
            return false;
        }

        cell = free[_rng.Next(free.Count)];
        return true;
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
