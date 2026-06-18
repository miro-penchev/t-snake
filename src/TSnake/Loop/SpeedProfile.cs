using TSnake.Core;

namespace TSnake.Loop;

/// <summary>
/// The speed-up curve, as plain numbers — the loop's equivalent of <see cref="GameConfig"/>.
/// Speed is purely the loop's concern (the engine is identical at every speed): each tick the
/// interval is recomputed from the current state, shrinking with food eaten and clamped to a
/// floor so it never becomes unplayably fast. With no food eaten the interval is exactly the base.
/// Easy sets <see cref="ShrinkRate"/> to zero for a constant speed. Actual numbers per
/// difficulty/level are the Settings plan's job.
/// </summary>
/// <param name="BaseIntervalMs">Starting tick interval, before any shrink.</param>
/// <param name="MinIntervalMs">Floor the interval can never drop below.</param>
/// <param name="ShrinkRate">Per-step multiplicative shrink in [0, 1); 0 = constant speed (Easy).</param>
/// <param name="Driver">What the curve is a function of. <see cref="SpeedDriver.FoodEaten"/>.</param>
public sealed record SpeedProfile(int BaseIntervalMs, int MinIntervalMs, double ShrinkRate, SpeedDriver Driver)
{
    /// <summary>
    /// The current tick interval for <paramref name="state"/> — pure: the base shrunk by the
    /// driver's progress and clamped to the floor.
    /// </summary>
    public int IntervalFor(GameState state) => IntervalAt(Progress(state));

    /// <summary>
    /// The pure interval math for a raw progress count, extracted so the curve is testable without
    /// constructing a <see cref="GameState"/>: <c>round(base · (1 − shrink)^progress)</c>, floored
    /// at <see cref="MinIntervalMs"/>. With <see cref="ShrinkRate"/> = 0 it is constant at the base.
    /// </summary>
    public int IntervalAt(int progress)
    {
        if (progress <= 0 || ShrinkRate <= 0)
        {
            return Floor(BaseIntervalMs);
        }

        double factor = Math.Pow(1.0 - ShrinkRate, progress);
        return Floor((int)Math.Round(BaseIntervalMs * factor));
    }

    // Progress is food eaten (0 at the start), so the opening interval is exactly the base and the
    // ramp is the speed you earned — not pre-shrunk by the snake's starting length.
    private int Progress(GameState state) => Driver switch
    {
        SpeedDriver.FoodEaten => state.FoodEaten,
        _ => throw new ArgumentOutOfRangeException(nameof(state), Driver, "Unknown speed driver."),
    };

    private int Floor(int interval) => Math.Max(interval, MinIntervalMs);
}
