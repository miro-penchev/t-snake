namespace TSnake.Loop;

/// <summary>
/// The trickiest timing logic, extracted as a pure unit so it can be tested with hand-fed
/// timestamps instead of a real clock. Given (now, lastTick, interval) it answers "is a tick
/// due?" and computes the next <c>lastTick</c> — normally <c>lastTick + interval</c>, but it
/// <i>drops the backlog</i> (snaps to now) when we have fallen badly behind, so a GC pause or
/// stall can never trigger a burst of catch-up ticks that would teleport the snake (plan §2.1).
/// </summary>
public sealed class TickClock
{
    /// <summary>How many intervals behind we tolerate before dropping the backlog.</summary>
    public const double DefaultBacklogDropFactor = 2.0;

    private readonly double _backlogDropFactor;

    public TickClock(double backlogDropFactor = DefaultBacklogDropFactor) => _backlogDropFactor = backlogDropFactor;

    /// <summary>True once at least <paramref name="interval"/> has elapsed since <paramref name="lastTick"/>.</summary>
    public bool IsDue(TimeSpan now, TimeSpan lastTick, TimeSpan interval) => now - lastTick >= interval;

    /// <summary>
    /// The next <c>lastTick</c> after a tick fires. Normally advances by exactly one interval, but
    /// if we fell more than <see cref="DefaultBacklogDropFactor"/> intervals behind it snaps to
    /// <paramref name="now"/> so the next tick is scheduled afresh (no catch-up burst).
    /// </summary>
    public TimeSpan Advance(TimeSpan now, TimeSpan lastTick, TimeSpan interval)
        => now - lastTick > interval * _backlogDropFactor
            ? now
            : lastTick + interval;
}
