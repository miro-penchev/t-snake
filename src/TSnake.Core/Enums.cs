namespace TSnake.Core;

/// <summary>
/// The <i>semantic</i> content of a cell — no glyphs or colors, so the engine stays
/// free of any presentation concern. A collision is an event on <see cref="TickResult"/>,
/// not a cell kind (see plan decision #7).
/// </summary>
public enum CellKind
{
    Empty,
    SnakeHead,
    SnakeBody,
    Food,
    Obstacle,
}

/// <summary>Current state of play.</summary>
public enum GameStatus
{
    Running,
    Paused,
    GameOver,
    Won,
}

/// <summary>Why the game ended; drives the death effect and the results-screen message.</summary>
public enum EndReason
{
    None,
    HitSelf,
    HitObstacle,
    BoardFull,
}
