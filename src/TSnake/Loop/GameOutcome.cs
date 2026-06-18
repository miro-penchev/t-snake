using TSnake.Core;

namespace TSnake.Loop;

/// <summary>
/// The result of one play session, returned by <see cref="GameLoop.Run"/> — enough for the
/// results screen and high-score persistence (later plans) to consume.
/// </summary>
/// <param name="Score">Final score.</param>
/// <param name="EndReason">Why the game ended; <see cref="EndReason.None"/> when the player quit.</param>
/// <param name="Status">The engine's final status (e.g. <see cref="GameStatus.GameOver"/> / <see cref="GameStatus.Won"/>).</param>
/// <param name="Quit">True when the session ended because the player pressed quit rather than dying/winning.</param>
/// <param name="Ticks">How many ticks the session ran.</param>
/// <param name="Elapsed">Wall-clock duration of the session.</param>
public sealed record GameOutcome(
    int Score,
    EndReason EndReason,
    GameStatus Status,
    bool Quit,
    int Ticks,
    TimeSpan Elapsed);
