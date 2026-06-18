using TSnake.Core;
using TSnake.Loop;

namespace TSnake.Settings;

/// <summary>
/// Exactly the three things composition needs to start a session (plan §2.1): the engine's
/// <see cref="GameConfig"/>, the loop's <see cref="SpeedProfile"/>, and whether pause is allowed.
/// It is the output of <see cref="GameSettings.BuildSession"/> — the point where a human-facing
/// "Pro, level 2" becomes the plain records the engine and loop consume.
/// </summary>
/// <param name="Engine">Tuning for <c>GameEngine</c>.</param>
/// <param name="Speed">The speed-up curve for <c>GameLoop</c>.</param>
/// <param name="PauseAllowed">Whether the loop honours the pause key.</param>
public sealed record SessionConfig(GameConfig Engine, SpeedProfile Speed, bool PauseAllowed);
