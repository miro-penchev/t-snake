using TSnake.Loop;

namespace TSnake.Tests;

public class TickClockTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private readonly TickClock _clock = new();
    private readonly TimeSpan _interval = Ms(100);

    [Fact]
    public void NotDueBeforeTheIntervalElapses()
    {
        Assert.False(_clock.IsDue(now: Ms(99), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void DueExactlyAtTheInterval()
    {
        Assert.True(_clock.IsDue(now: Ms(100), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void DuePastTheInterval()
    {
        Assert.True(_clock.IsDue(now: Ms(150), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void AdvanceStepsByOneIntervalWhenOnTime()
    {
        // Fired right on time: schedule the next tick exactly one interval on, no drift.
        Assert.Equal(Ms(100), _clock.Advance(now: Ms(100), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void AdvanceStepsByOneIntervalWhenSlightlyLate()
    {
        // A small overshoot (within the backlog tolerance) still advances by exactly one interval,
        // so a steady tick rate is preserved rather than drifting with the clock.
        Assert.Equal(Ms(100), _clock.Advance(now: Ms(150), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void AdvanceStepsByOneIntervalAtTheBacklogBoundary()
    {
        // Exactly 2× behind is still tolerated (> factor, not >=): advance by one interval.
        Assert.Equal(Ms(100), _clock.Advance(now: Ms(200), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void AdvanceDropsTheBacklogWhenFarBehind()
    {
        // A big gap (GC pause / stall) snaps to now, so no catch-up burst teleports the snake.
        Assert.Equal(Ms(5000), _clock.Advance(now: Ms(5000), lastTick: Ms(0), _interval));
    }

    [Fact]
    public void PauseResumeModeledAsLastTickReset_IsNotImmediatelyDue()
    {
        // On resume the loop snaps lastTick = now; the next iteration must not be due (no burst).
        TimeSpan now = Ms(5000);
        TimeSpan lastTick = now; // resume snap
        Assert.False(_clock.IsDue(now, lastTick, _interval));
    }
}
