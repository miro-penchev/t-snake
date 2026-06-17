namespace TSnake.Core;

/// <summary>
/// Every tuning input the engine needs, as plain numbers. The engine never knows
/// about difficulty <i>names</i> or settings files — the Settings module (a later
/// plan) translates "Pro, level 2" into one of these records.
/// </summary>
/// <param name="Width">Board width in cells.</param>
/// <param name="Height">Board height in cells.</param>
/// <param name="BasePointsPerFood">Base score awarded per food eaten, before multipliers.</param>
/// <param name="GrowthPerFood">Segments the snake gains per food (classic = 1).</param>
/// <param name="DifficultyMultiplier">Score multiplier for the difficulty tier.</param>
/// <param name="LevelMultiplier">Score multiplier for the level.</param>
/// <param name="ObstaclesEnabled">When false the board is static (Easy mode).</param>
/// <param name="ObstacleSpawnRate">
/// Per-tick spawn chance as a percent in [0, 100]. Higher = more frequent.
/// </param>
/// <param name="ObstacleLifetimeTicks">How many ticks a spawned obstacle lives before despawning.</param>
/// <param name="MaxObstacles">Maximum obstacles on the board simultaneously.</param>
/// <param name="InitialSnakeLength">Starting snake length (head + body).</param>
/// <param name="StartPosition">Starting head position; body extends left of it.</param>
/// <param name="Seed">RNG seed, so a run can be reproduced.</param>
public sealed record GameConfig(
    int Width,
    int Height,
    int BasePointsPerFood,
    int GrowthPerFood,
    int DifficultyMultiplier,
    int LevelMultiplier,
    bool ObstaclesEnabled,
    int ObstacleSpawnRate,
    int ObstacleLifetimeTicks,
    int MaxObstacles,
    int InitialSnakeLength,
    Point StartPosition,
    int Seed);
