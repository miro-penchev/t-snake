# Detailed Plan 04 — Game Loop & Speed/Difficulty Timing

The fourth per-module plan, and the one the previous three were building toward. The loop is the conductor: it decides *when* the engine ticks, drives the difficulty speed-up, and orchestrates input → engine → renderer into a single coordinated cycle. It replaces the throwaway `Thread.Sleep` harness with real `Stopwatch`-driven timing.

It lives in the console app (`src/TSnake/`, e.g. a `GameLoop` plus a small `Loop/` or `Timing/` folder), references `TSnake.Core` for the engine types, and *receives* the renderer and input service rather than constructing them (so composition stays in `Program`).

---

## 1. What the loop owns, and what it does not

**Owns:** the timing (when to tick), the speed-up curve, the running ↔ paused state of a single play session, and the orchestration of input/engine/renderer each cycle. It runs **one game** and returns its outcome.

**Does NOT own:** game rules (engine), drawing details (renderer), key reading (input), the menu/results *screens* and high-score saving (composition + later plans), and difficulty *names* — like the engine, it gets numbers and flags, not "Pro".

The loop is handed everything it needs and returns a result:

```csharp
public sealed class GameLoop
{
    public GameLoop(GameEngine engine, IRenderer renderer, InputService input,
                    SpeedProfile speed, bool pauseAllowed);

    public GameOutcome Run();   // runs one session to its end (death / win / quit)
}
```

`GameOutcome` carries the final score, `EndReason`, status, and run length (ticks/elapsed) — enough for the results screen and persistence to consume later.

---

## 2. The core design decisions

### 2.1 Discrete simulation, real-time pacing (a changing-timestep accumulator)

Snake is inherently discrete — one tick = one cell. The loop never interpolates sub-cell motion; it just decides *when* the next whole tick fires. It keeps a `Stopwatch` and a `lastTick` timestamp, and fires a tick when `Elapsed − lastTick ≥ interval`. The interval is **not** constant: it shrinks as the game speeds up (§2.3). So it's a fixed-timestep loop whose timestep changes over the session.

Two safety rules baked in:
- **At most one tick per loop iteration.** Combined with iterating faster than the tick rate, this makes the classic "spiral of death" impossible.
- **Drop the backlog if we fall behind.** If `Elapsed − lastTick` ever exceeds the interval by a lot (a GC pause, a stall), snap `lastTick = Elapsed` instead of firing a burst of catch-up ticks. For a game, smoothness beats temporal accuracy — a backlog burst would teleport the snake.

### 2.2 Iterate often, tick when due, render only on change

The loop iterates faster than it ticks. Each iteration it **polls input** (cheap — just drains the channel the reader thread already filled) so quit and pause feel instant even at slow tick rates. It only **ticks** when the interval has elapsed, and only **renders** when something actually changed:
- after a tick → `renderer.Apply(result)` (a few cells + HUD),
- on entering pause → `renderer.ShowPaused()`,
- on resume → `renderer.Redraw(state)`.

Idle iterations draw nothing, which keeps terminal writes to the minimum — the same anti-flicker discipline as the renderer plan.

### 2.3 Speed-up = a shrinking interval from a `SpeedProfile`

Speed is purely the loop's concern (the engine is identical at every speed). The loop owns a `SpeedProfile` — plain numbers, like `GameConfig` is for the engine:

- `BaseIntervalMs` — starting tick interval (per level).
- `MinIntervalMs` — a floor, so it never becomes unplayably fast.
- a **shrink rate** — how aggressively the interval drops.

Easy sets the shrink rate to zero (constant speed). Pro shrinks gradually; Terminator shrinks faster — that single number is most of what separates the tiers' *feel* (the "more obstacles" difference lives in the engine's `GameConfig`, not here). The interval is recomputed after each tick via a pure function:

```csharp
public sealed record SpeedProfile(int BaseIntervalMs, int MinIntervalMs, double ShrinkRate, SpeedDriver Driver)
{
    public int IntervalFor(GameState state);   // pure: base shrunk by driver, clamped to floor
}
```

**Speed-up driver: food eaten (confirmed).** The interval shrinks as a function of food eaten / snake length — the classic Snake ramp, where the speed you face is the speed you earned and a skilled run naturally accelerates. (Time-based ramping was considered and rejected: it would speed the game up even during cautious play, punishing patience rather than rewarding progress.)

### 2.4 Timing precision without busy-spinning

This addresses the high-level plan's "timing precision at fast speeds" risk and its "don't busy-spin" caution together. The default `Thread.Sleep` granularity on Windows (~15 ms) is too coarse for, say, a 50 ms tick — the unevenness is visible. So:

- Raise the timer resolution to ~1 ms with `timeBeginPeriod(1)` (a `winmm.dll` P/Invoke), paired with `timeEndPeriod(1)` on teardown.
- For the inter-iteration wait, **sleep the bulk** of the remaining time coarsely, then **briefly spin** (a `Stopwatch`-checked tight loop) only for the final ~1–2 ms to land precisely. CPU stays low (mostly sleeping); the spin is short enough not to be the "busy-spin" the plan warned against.

A no-P/Invoke alternative (spin a slightly longer tail, no `timeBeginPeriod`) is noted in the decisions, in case we'd rather avoid the native call.

### 2.5 Pause freezes the clock (the one subtle loop bug)

Pause is Easy-only, enforced here via the `pauseAllowed` flag (the loop honors `input.ConsumePause()` only when set). The trap: if the `Stopwatch` keeps running while paused, then on resume `Elapsed − lastTick` is huge and the loop wants to fire a flood of ticks — the snake jumps. So:

- On **pause**: stop advancing the tick clock, draw the overlay, keep polling input (to catch resume / quit), draw nothing else.
- On **resume**: `renderer.Redraw(state)` to clear the overlay cleanly (per plan 02), and **snap `lastTick = Elapsed`** so no catch-up burst occurs.

---

## 3. The loop skeleton

```
timeBeginPeriod(1)                      // raise timer resolution (paired teardown in finally)
renderer.Begin(engine.State)
sw = Stopwatch.StartNew()
lastTick = sw.Elapsed
interval = speed.BaseIntervalMs
running = true

while true:
    input.Poll()
    if input.ConsumeQuit():  outcome = Quit;  break

    if running:
        if pauseAllowed && input.ConsumePause():
            running = false
            renderer.ShowPaused()
        else if (sw.Elapsed - lastTick) >= interval:
            if input.TryNextTurn(out dir): engine.SetDirection(dir)
            result = engine.Tick()
            renderer.Apply(result)
            if engine.Status != Running:           // GameOver or Won
                outcome = Outcome(engine.State, result.EndReason);  break

            interval = speed.IntervalFor(engine.State)
            // advance the grid; drop backlog if we fell badly behind
            lastTick = (sw.Elapsed - lastTick > 2*interval) ? sw.Elapsed : lastTick + interval
    else:  // paused
        if input.ConsumePause():
            running = true
            renderer.Redraw(engine.State)
            lastTick = sw.Elapsed                  // no catch-up burst

    PreciseWait()                                   // sleep bulk + short spin tail
// finally: timeEndPeriod(1); (terminal restore is the renderer's TerminalSession dispose)
return outcome
```

Note the ordering: input is drained every iteration (responsive quit/pause), but a *turn* is pulled from the buffer only at the moment of a tick — so a buffered turn applies on the very next tick, which is the freshest it can be.

---

## 4. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `SpeedProfile` | record (pure) | base/min interval + shrink rate + driver; `IntervalFor(state)` computes the current interval |
| `SpeedDriver` | enum | `FoodEaten` (the chosen driver). Kept as an enum only as a future seam; not a branch we need now |
| `TickClock` | class (pure) | given (now, lastTick, interval) decides "tick due?" and computes the next `lastTick` with backlog-drop — extracted so the trickiest timing logic is testable without real time |
| `GameOutcome` | record | final score, `EndReason`, status, ticks/elapsed |
| `GameLoop` | class | the orchestrator (real `Stopwatch`, the wait, the state machine) |

`timeBeginPeriod`/`timeEndPeriod` live in a tiny `WinTimerResolution : IDisposable` so the raise/restore is paired and exception-safe.

---

## 5. Testing plan

The loop itself is timing + I/O heavy, but the parts that are easy to get *wrong* are extracted as pure units and tested with synthetic time — no real clock, no Console:

- **`SpeedProfile.IntervalFor`** — starts at base; shrinks correctly per the driver; never drops below the floor; Easy (zero shrink) stays constant. 
- **`TickClock`** — with hand-fed timestamps: fires once when due, not when not due, never more than once per call, and drops the backlog (no burst) when handed a large gap. The pause case is modeled as "lastTick reset to now → next call not due."
- The orchestration (input/engine/renderer wiring, the spin-wait, `timeBeginPeriod`) is verified by **playing**: this is where smoothness, speed-up feel, and pause behavior get judged by eye, and where any flicker/latency measurement from the high-level plan's risk list happens.

---

## 6. Wire-up milestone & definition of done

This plan turns the steerable harness into a real game session:

- A full session runs at a chosen difficulty/level: Easy holds a constant speed; Pro and Terminator accelerate, Terminator faster — visibly and smoothly, with no stutter at the fastest interval.
- **Pause works in Easy** (Space): the snake freezes, the overlay shows, and on resume it continues from exactly where it was — **no teleport**. Pause is rejected in Pro/Terminator.
- **Esc quits** the session promptly even at slow tick rates.
- Death or board-full ends the session and `Run()` returns a populated `GameOutcome`.
- The terminal is restored on every exit path (normal, quit, exception) — `WinTimerResolution` and the renderer's `TerminalSession` both dispose in `finally`.
- `SpeedProfile` and `TickClock` unit tests pass.

At this point everything except the *surrounding* app (settings, persistence, menus, results screen) is real and playable end to end.

---

## Decisions to confirm before the session

1. **Timing approach** — `Stopwatch` accumulator + `timeBeginPeriod(1)` + coarse-sleep-then-short-spin wait; one tick per iteration; drop backlog when behind. *(Alternative: no `timeBeginPeriod`, rely on a slightly longer spin tail — simpler, marginally higher CPU.)*
2. ✅ **Speed-up driver = `FoodEaten`** (classic feel — the speed you face is the speed you earned; rewards aggressive play). `Elapsed`/`Blend` rejected.
3. **`SpeedProfile` shape** — base interval, min-interval floor, shrink rate, driver; Easy = zero shrink. Actual numbers deferred to the Settings plan.
4. **Loop scope = one play session**, returning `GameOutcome`. Menus, results screen, and high-score saving are composition/later plans.
5. **Pause freezes the clock and snaps `lastTick` on resume**; `pauseAllowed` passed as a flag (true only for Easy).
6. **Render only on change** (after tick / overlay transitions), never every iteration.
7. **Extract `SpeedProfile` + `TickClock` as pure, testable units**; keep the `Stopwatch`/wait/`timeBeginPeriod` in the thin orchestrator.

Once settled — and once you've picked the speed-up driver — the session is: build `SpeedProfile`, `TickClock`, `WinTimerResolution`, `GameOutcome`, and `GameLoop`; write the pure timing tests; then run a full, properly-paced game with working speed-up and pause.
