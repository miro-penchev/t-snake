using System.Diagnostics;
using TSnake.Core;
using TSnake.Input;
using TSnake.Rendering;

namespace TSnake.Loop;

/// <summary>
/// The conductor (plan §1): it decides <i>when</i> the engine ticks, drives the speed-up, and
/// orchestrates input → engine → renderer each cycle. It runs <b>one</b> session and returns its
/// <see cref="GameOutcome"/>. It is <i>handed</i> the engine, renderer and input (composition stays
/// in <c>Program</c>) and owns only the timing, the speed curve, and the running ↔ paused state.
/// </summary>
/// <remarks>
/// The loop iterates faster than it ticks: every iteration it polls input (so quit/pause feel
/// instant even at slow tick rates) and waits a short, precise slice; it ticks only when the
/// interval has elapsed, and renders only on change. A <see cref="WinTimerResolution"/> raised
/// for the duration keeps the waits crisp. Pause freezes the tick clock and snaps <c>lastTick</c>
/// on resume so the snake never teleports (plan §2.5).
/// </remarks>
public sealed class GameLoop
{
    // The inter-iteration wait never sleeps longer than this, so input stays responsive even when
    // the tick interval is large; near a tick we sleep the bulk then spin the final stretch.
    private const double MaxPollMs = 5.0;
    private const double SpinTailMs = 2.0;

    private readonly GameEngine _engine;
    private readonly IRenderer _renderer;
    private readonly InputService _input;
    private readonly SpeedProfile _speed;
    private readonly bool _pauseAllowed;
    private readonly TickClock _clock = new();

    public GameLoop(GameEngine engine, IRenderer renderer, InputService input, SpeedProfile speed, bool pauseAllowed)
    {
        _engine = engine;
        _renderer = renderer;
        _input = input;
        _speed = speed;
        _pauseAllowed = pauseAllowed;
    }

    /// <summary>Runs one session to its end (death / win / quit) and returns the outcome.</summary>
    public GameOutcome Run()
    {
        using var _ = new WinTimerResolution();

        _renderer.Begin(_engine.State);

        var sw = Stopwatch.StartNew();
        TimeSpan lastTick = sw.Elapsed;
        TimeSpan interval = TimeSpan.FromMilliseconds(_speed.BaseIntervalMs);
        bool running = true;

        while (true)
        {
            _input.Poll();

            if (_input.ConsumeQuit())
            {
                return Outcome(sw.Elapsed, EndReason.None, quit: true);
            }

            if (running)
            {
                if (_pauseAllowed && _input.ConsumePause())
                {
                    running = false;
                    _renderer.ShowPaused();
                }
                else if (_clock.IsDue(sw.Elapsed, lastTick, interval))
                {
                    // Pull a buffered turn only at the moment of a tick, so it applies on the
                    // freshest possible tick (plan §3).
                    if (_input.TryNextTurn(out Direction dir))
                    {
                        _engine.SetDirection(dir);
                    }

                    TickResult result = _engine.Tick();
                    _renderer.Apply(result);

                    if (_engine.Status != GameStatus.Running)
                    {
                        // A fatal collision erupts from the cell the snake died on; a board-full win
                        // has no collision cell, so it ends quietly.
                        if (result.CollisionCell is { } origin)
                        {
                            _renderer.PlayDeathEffect(origin);
                        }

                        return Outcome(sw.Elapsed, result.EndReason, quit: false);
                    }

                    interval = TimeSpan.FromMilliseconds(_speed.IntervalFor(_engine.State));
                    lastTick = _clock.Advance(sw.Elapsed, lastTick, interval);
                }
            }
            else if (_input.ConsumePause())
            {
                // Resume: clear the overlay cleanly and snap the clock so no catch-up burst fires.
                running = true;
                _renderer.Redraw(_engine.State);
                lastTick = sw.Elapsed;
            }

            PreciseWait(sw, lastTick, interval, running);
        }
    }

    private GameOutcome Outcome(TimeSpan elapsed, EndReason endReason, bool quit)
    {
        GameState state = _engine.State;
        return new GameOutcome(state.Score, endReason, state.Status, quit, state.TickCount, elapsed);
    }

    /// <summary>
    /// Sleeps the bulk of the time until the next interesting moment coarsely, then spins the final
    /// ~<see cref="SpinTailMs"/> to land precisely on a due tick. Never waits more than
    /// <see cref="MaxPollMs"/> so input is polled often. While paused there is no tick to hit, so it
    /// just idles one poll slice.
    /// </summary>
    private static void PreciseWait(Stopwatch sw, TimeSpan lastTick, TimeSpan interval, bool running)
    {
        if (!running)
        {
            Thread.Sleep((int)MaxPollMs);
            return;
        }

        TimeSpan target = lastTick + interval;
        double untilNextMs = (target - sw.Elapsed).TotalMilliseconds;

        if (untilNextMs <= 0)
        {
            return; // already due — loop again immediately and fire the tick
        }

        if (untilNextMs > MaxPollMs)
        {
            Thread.Sleep((int)MaxPollMs); // far from the tick: idle a slice, stay responsive
            return;
        }

        // Close to the tick: coarse-sleep all but the spin tail, then spin to land precisely.
        double sleepMs = untilNextMs - SpinTailMs;
        if (sleepMs > 0)
        {
            Thread.Sleep((int)sleepMs);
        }

        while (sw.Elapsed < target)
        {
            Thread.SpinWait(1);
        }
    }
}
