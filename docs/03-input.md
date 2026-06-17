# Detailed Plan 03 — Input (Keyboard)

The third per-module plan. Input is the piece the high-level plan flagged as the main *responsiveness* risk, and it's where we settle the buffering policy deferred from plan 01. It's also the second real wire-up: combined with the renderer from plan 02, this plan lets you actually **steer the snake live** — a crude but playable build, still without the formal game loop.

It lives in the console app (`src/TSnake/Input/`), references `TSnake.Core` only for the `Direction` enum, and touches the `Console` keyboard and threading (which the engine never does).

---

## 1. What input owns, and what it does not

**Owns:** reading the keyboard without ever blocking the game loop, mapping raw keys to semantic commands (turn / pause / quit), and applying the buffering policy so the loop gets the right single turn to hand the engine each tick.

**Does NOT own:** game rules (it reports a `Pause` *command*; whether pause is honored — Easy-only — is the loop's call), deciding *when* to act on input (the loop drives timing), the reversal guard against the live heading (that stays in the engine, plan 01), and menu navigation (a later screens plan reuses the raw key channel).

---

## 2. The core design decisions

### 2.1 Non-blocking by construction: reader thread + channel

`Console.ReadKey` blocks until a key is pressed. Calling it on the loop thread would freeze rendering, so:

- A **long-lived background thread** sits in a loop calling `Console.ReadKey(intercept: true)` and pushes each raw `ConsoleKeyInfo` into a thread-safe channel. `intercept: true` keeps the keystroke off our rendered screen.
- The thread is `IsBackground = true` and runs for the whole app lifetime (it keeps serving whatever screen is active — game, menu, results), so there's no awkward "cancel a blocked `ReadKey`" problem; the process tears it down on exit.
- The **channel is pure transport.** All mapping and buffering happens on the *consumer* side, called by the loop once per tick on a single thread — so the gameplay-facing state needs no locks. The only thread-safe handoff is the channel itself.

This is why input "won't be an issue": the loop never waits on the keyboard, and the keyboard never waits on the loop.

### 2.2 The buffering policy: a bounded depth-2 turn buffer

This is the decision deferred from plan 01. The trade-off we identified:

- *Latest-wins* (collapse all presses to the most recent) can **drop a quick intermediate turn** — tap Down-then-Right to round a corner before the next tick and you only get Right, so you clip the corner.
- *Unbounded one-per-tick queue* preserves sequences but can **back up** — keep pressing and the snake keeps turning after you stop.

The fix is the middle ground: a **FIFO turn buffer capped at depth 2**. Each tick the loop dequeues **one** turn and hands it to `engine.SetDirection`; incoming turns are enqueued up to the cap.

- **Cap = 2** lets a single quick corner (two 90° turns) survive, while making runaway turning impossible — the buffer drains in at most two ticks once you stop pressing.
- **When the buffer is full, drop the *incoming* turn** (keep the two already committed). This preserves a deliberate quick corner instead of letting a later mash overwrite it.
- **Collapse no-op repeats:** a new turn equal to the last buffered turn is dropped (holding a key, OS key-repeat).

#### Where the reversal guard lives (the plan-01 subtlety)

Two layers, cleanly split so the input buffer never has to read engine state:

- **Within the buffer:** reject a new turn that is the 180° opposite of the *previous buffered turn*. This keeps a queued chain coherent (you can't buffer Up→Down).
- **The first turn against the live heading:** left to the engine's existing reversal guard. When the buffer is empty, the input layer enqueues the turn without checking heading; the engine rejects it next tick if it's a reversal (a harmless one-tick no-op). So input stays decoupled from the engine's current direction.

#### Worked example — a quick corner survives

Heading Right; player taps **Down** then **Right** between two ticks:
- Down: buffer empty → enqueue. Buffer `[Down]`.
- Right: vs last buffered `Down`, not opposite → enqueue. Buffer `[Down, Right]`.
- Tick 1: dequeue Down → engine (Down vs Right heading, legal) → moves down.
- Tick 2: dequeue Right → engine (Right vs Down heading, legal) → moves right. Corner rounded.

Under latest-wins the Down would have been lost. Under an unbounded queue, ten frantic presses would keep turning the snake for ten ticks; here, only two survive.

### 2.3 A swappable key source for the hardening path

The high-level plan keeps a Win32 `ReadConsoleInput` P/Invoke in reserve in case `Console.ReadKey` latency is ever perceptible at high speed. To make that swap painless, the reader reads from an **`IKeySource`** seam rather than calling `Console` directly. We start with the `Console.ReadKey` implementation; if measured latency demands it, a P/Invoke implementation drops in without touching the mapping or buffer code. The trigger symptom to watch for: input feeling laggy specifically at the fastest Terminator speeds.

---

## 3. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `IKeySource` | interface | `ConsoleKeyInfo ReadKey()` — the swappable seam (Console now, P/Invoke later) |
| `ConsoleKeySource` | class | default `IKeySource` wrapping `Console.ReadKey(intercept: true)` |
| `KeyboardReader` | `IDisposable` | owns the background thread; exposes a `ChannelReader<ConsoleKeyInfo>` |
| `InputCommand` | enum | `None, TurnUp, TurnDown, TurnLeft, TurnRight, Pause, Quit` |
| `KeyMap` | static (pure) | `ConsoleKeyInfo` → `InputCommand`; unit-testable |
| `TurnBuffer` | class (pure) | depth-2 FIFO with in-buffer reversal/duplicate rules; unit-testable |
| `InputService` | class | the loop-facing surface: drains the channel, fills the buffer, exposes pending turn + pause/quit flags |

---

## 4. Key mapping

Switch on `ConsoleKeyInfo.Key` (case-insensitive for letters, so `w` and `W` both work):

| Keys | Command |
|------|---------|
| `W`, `UpArrow` | `TurnUp` |
| `S`, `DownArrow` | `TurnDown` |
| `A`, `LeftArrow` | `TurnLeft` |
| `D`, `RightArrow` | `TurnRight` |
| `Spacebar` | `Pause` |
| `Escape` | `Quit` |
| anything else | `None` (ignored) |

The Console API gives key-*down* events only (no key-release), and OS key-repeat produces repeated down events — both fine here: repeats are absorbed by the duplicate-collapse and the depth cap, and Snake never needs key-hold state.

---

## 5. The loop-facing surface

```csharp
public sealed class InputService : IDisposable
{
    public InputService(IKeySource source);   // starts the background reader

    void Poll();                       // called once per tick: drain channel -> map -> buffer + flags
    bool TryNextTurn(out Direction d);  // dequeue at most one buffered turn for this tick
    bool ConsumePause();                // true once if a pause was requested since last poll
    bool ConsumeQuit();                 // true if quit was requested
}
```

Each tick the loop does roughly:

```
input.Poll();
if (input.TryNextTurn(out var dir)) engine.SetDirection(dir);
if (input.ConsumeQuit())  { ... }                 // loop/screens decide
if (input.ConsumePause()) { ... }                 // honored only in Easy (loop's rule)
```

While paused, the loop stops ticking but keeps calling `Poll()` so it can see the resume (`Pause` again) or `Quit`. The reader thread keeps running throughout.

---

## 6. Testing plan

The threaded reader is hard to unit-test, but it's deliberately thin; the logic that matters is pure and fully testable with no Console and no threads:

- **`KeyMap`** — every specified key maps to the right command; unknown keys map to `None`; both letter cases work.
- **`TurnBuffer`** — feed command sequences and assert the per-tick dequeue order. Cover: depth-2 cap (third turn dropped while full), in-buffer reversal rejection (Up then Down → Down dropped), duplicate collapse, and the quick-corner sequence from §2.2.
- **`InputService` with a fake `IKeySource`** — inject a scripted key stream and assert the resulting turns and flags, exercising the whole map→buffer pipeline deterministically. (The fake source is the input-side analogue of the engine's `FakeRandom`.)

What stays manual: that real keypresses feel responsive on Windows Terminal, verified by playing — and the basis for any latency measurement in the high-level plan's risk list.

---

## 7. Wire-up milestone for this plan

Extend the rendering harness from plan 02 with a real `InputService`: read WASD/arrows to steer, Space to pause, Esc to quit, with crude `Thread.Sleep` pacing (still **not** the real loop):

```
var engine  = new GameEngine(config, new SeededRandom(seed));
using var ui    = new ConsoleRenderer(theme);
using var input = new InputService(new ConsoleKeySource());

ui.Begin(engine.State);
while (engine.Status == GameStatus.Running)
{
    input.Poll();
    if (input.ConsumeQuit()) break;
    if (input.TryNextTurn(out var dir)) engine.SetDirection(dir);

    var result = engine.Tick();
    ui.Apply(result);
    Thread.Sleep(120);          // crude fixed pacing; speed-up is the loop's job (later)
}
```

At this point the game is **playable** — you steer a live snake that eats, grows, and crashes — even though the proper fixed-timestep loop, speed-up, difficulty, and screens are still ahead.

---

## 8. Definition of done

- `KeyboardReader` runs on a background thread and never blocks the consumer; the channel drains cleanly each tick.
- `KeyMap` and `TurnBuffer` behave per §4 and §2.2, proven by the pure tests.
- The harness lets you steer the snake with WASD and arrows, pause with Space, and quit with Esc, with no input lag at hand-driven speed, in both render themes.
- Reversals are rejected and quick corners survive, observable while playing.

---

## Decisions to confirm before the session

1. ✅ **Buffering = bounded depth-2 FIFO turn buffer** (resolves the plan-01 deferral). Confirm depth = 2 and **drop-incoming-when-full**.
2. **Reader = long-lived background thread** on blocking `Console.ReadKey(intercept: true)`, `IsBackground = true`; consumer-side, lock-free mapping/buffering. (Alternative: `KeyAvailable` polling — rejected for added latency.)
3. **Handoff = `System.Threading.Channels.Channel<ConsoleKeyInfo>`**, unbounded (transport); the gameplay cap lives in the depth-2 buffer, not the channel.
4. ✅ **Key map** per §4 — quit is **`Escape` only**; `Q` is deliberately *not* a quit key (too close to WASD, accidental-quit risk).
5. **Reversal guard split** — engine handles first-turn-vs-heading (existing); input buffer handles consecutive-buffered-turn. Confirm.
6. **`IKeySource` seam** now; P/Invoke `ReadConsoleInput` implementation deferred to hardening, swapped in only if latency demands.
7. **Pause/quit are commands, not actions** — Input reports them; the loop enforces Easy-only pause and decides quit semantics (confirm-to-exit vs. straight to menu). Confirm this separation.

Once settled, the session is: build `IKeySource`/`ConsoleKeySource`, `KeyboardReader`, `KeyMap`, `TurnBuffer`, and `InputService`; write the pure tests; then extend the harness so the snake is steerable live.
