namespace TSnake.Settings;

/// <summary>
/// The tuning that <i>difficulty</i> governs (plan §2.1): the score multiplier, whether pause is
/// allowed, the obstacle behaviour, and how aggressively the speed ramps up. Levels layer the
/// board and pace on top of this; <see cref="GameSettings.BuildSession"/> merges the two by one
/// explicit rule. Pure data — no behaviour of its own.
/// </summary>
/// <param name="Multiplier">The difficulty's score multiplier (score = base × this × level mult).</param>
/// <param name="PauseAllowed">Whether the player may pause; Easy-only by design.</param>
/// <param name="ObstaclesEnabled">When false the board is static (Easy).</param>
/// <param name="ObstacleSpawnRate">Per-tick spawn chance as a percent in [0, 100].</param>
/// <param name="ObstacleLifetimeTicks">Ticks a spawned obstacle lives before despawning.</param>
/// <param name="MaxObstacles">Maximum obstacles on the board simultaneously.</param>
/// <param name="ShrinkRate">Speed-up shrink rate in [0, 1); 0 = constant speed (Easy).</param>
public sealed record DifficultyProfile(
    int Multiplier,
    bool PauseAllowed,
    bool ObstaclesEnabled,
    int ObstacleSpawnRate,
    int ObstacleLifetimeTicks,
    int MaxObstacles,
    double ShrinkRate);
