namespace TSnake.Settings;

/// <summary>
/// The small block the file remembers day-to-day (plan §2.3): render mode, the last-played
/// difficulty/level, and the player name. The balance table stays code-defined, so a later
/// rebalance is not silently frozen by every user's saved copy.
/// </summary>
/// <param name="RenderMode">Preferred render mode; composition maps it to a theme.</param>
/// <param name="Difficulty">Last-played difficulty.</param>
/// <param name="Level">Last-played level (1–3).</param>
/// <param name="PlayerName">Name shown on the high-score table.</param>
public sealed record Preferences(RenderMode RenderMode, Difficulty Difficulty, int Level, string PlayerName);
