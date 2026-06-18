namespace TSnake.Settings;

/// <summary>
/// The tuning that <i>level</i> governs (plan §2.1): the board, the base/min pace, the snake's
/// starting length, the per-food scoring, and the level multiplier. Obstacle behaviour and the
/// speed-up rate come from the <see cref="DifficultyProfile"/> instead. Pure data.
/// </summary>
/// <param name="Width">Board width in cells.</param>
/// <param name="Height">Board height in cells.</param>
/// <param name="BaseIntervalMs">Starting tick interval before any speed-up.</param>
/// <param name="MinIntervalMs">Floor the tick interval can never drop below.</param>
/// <param name="InitialSnakeLength">Starting snake length (head + body).</param>
/// <param name="BasePointsPerFood">Base score per food, before multipliers.</param>
/// <param name="GrowthPerFood">Segments gained per food (classic = 1).</param>
/// <param name="Multiplier">The level's score multiplier.</param>
public sealed record LevelProfile(
    int Width,
    int Height,
    int BaseIntervalMs,
    int MinIntervalMs,
    int InitialSnakeLength,
    int BasePointsPerFood,
    int GrowthPerFood,
    int Multiplier);
