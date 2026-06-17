namespace TSnake.Core;

/// <summary>
/// What happened during one <see cref="GameEngine.Tick"/>: the cells that changed, and —
/// if the game just ended — why, and the cell the head tried to enter when it died.
/// </summary>
/// <param name="Changes">The cells whose content changed this tick (2–4 in the common case).</param>
/// <param name="EndReason"><see cref="EndReason.None"/> unless the game ended this tick.</param>
/// <param name="CollisionCell">The cell the head collided with, or null if no fatal collision.</param>
public sealed record TickResult(
    IReadOnlyList<CellChange> Changes,
    EndReason EndReason,
    Point? CollisionCell);
