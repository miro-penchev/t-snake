# t-snake — High-Level Plan

A first-pass architecture and implementation sketch for the terminal Snake remake. This is intentionally high level; detailed per-module plans come later, before each Claude Code implementation session.

---

## 1. Goals & constraints

- **Platform:** Windows Terminal only. No cross-terminal or cross-platform support is a goal.
- **Stack:** C# on .NET 10 (current LTS). Console application.
- **Nature:** Proof-of-concept / tribute, not commercial. Code clarity and a clean module split matter more than feature breadth.
- **Feel:** Smooth rendering and *responsive* input even at high speed are the hard requirements. Input must never block or stutter the render loop.

---

## 2. Design pillars

1. **Decouple input, simulation, and rendering.** Each runs on its own clock/responsibility so a slow or busy stage never freezes the others.
2. **Render by diffing.** Only redraw cells that changed, and write the frame to the terminal in a single batched call. This is the main weapon against flicker and lag in Windows Terminal.
3. **Data-driven difficulty/levels.** Speed curves, obstacle rates, and multipliers are config values, not hard-coded branches scattered through the engine.
4. **Everything testable that can be.** Game rules (movement, collision, scoring, wrap-around) are pure logic with no console dependency, so they can be unit-tested headlessly.

---

## 3. Module breakdown

Responsibilities are separated so they can be planned and implemented one at a time.

```
                         +-------------------+
                         |     Program       |  entry point, wiring,
                         |   (composition)   |  menu -> game -> results
                         +---------+---------+
                                   |
        +--------------+-----------+-----------+---------------+
        |              |                       |               |
        v              v                       v               v
+---------------+  +-----------+        +--------------+  +-------------+
|    Input      |  |  Engine   |        |   Renderer   |  | Persistence |
|  (keyboard)   |  | (core sim)|        |  (output)    |  | (highscores)|
+-------+-------+  +-----+-----+        +------+-------+  +------+------+
        |                |                     ^                 ^
        |  direction     | game state          | frame           | scores
        +--------------> |  ------------------> |                 |
                         |                                        |
                         +------------------ Settings ------------+
                          (difficulty, level params, render mode)
```

### Input module
- Dedicated reader (background thread / task) running `Console.ReadKey(intercept: true)` in a loop, pushing key events into a thread-safe queue / `System.Threading.Channels` channel.
- The engine *drains* the channel each tick and keeps the last valid direction. This prevents input lag and key-buffer build-up at high speed.
- Maps WASD + arrows to directions; rejects 180° reversals; handles `Space` (pause, Easy only), and quit/menu keys.
- **Hardening path:** if `ReadKey` latency is unacceptable, fall back to Win32 `ReadConsoleInput` via P/Invoke for lower-latency raw input.

### Engine (core simulation)
- Pure game state: board dimensions, snake body, food, obstacles, score, status (running/paused/over).
- Per-tick update: apply direction, move snake, resolve wrap-around at edges, detect collisions (self / obstacle), handle eating + growth, update score.
- No `Console` calls here — it only produces state. Drives the difficulty behaviour via injected settings.

### Renderer (output)
- Holds a back buffer (grid of cell = glyph + color) and a front buffer.
- Each frame: build the new back buffer from engine state, diff against front, and emit only changed cells as a single batched string (cursor moves + glyphs + ANSI color codes), written once to stdout.
- Two themes behind one interface: **Unicode+color** and **ASCII monochrome**. The theme is just a glyph/color table; the diff/draw machinery is shared.
- Setup: UTF-8 output encoding, enable virtual terminal processing (ANSI), hide cursor, use the alternate screen buffer, restore terminal state on exit.

### Persistence (high scores)
- High-score table stored as JSON via `System.Text.Json` in a user data folder (e.g. `%APPDATA%\t-snake\`).
- Entry: name, score, difficulty, level, date. Load on start, insert + trim + save on game over.

### Settings
- Difficulty (`Easy` / `Pro` / `Terminator`) and per-level parameters: base tick interval, speed-up curve, obstacle spawn/despawn rates, score multiplier.
- Render mode (Unicode vs ASCII) and any board options.
- Loaded from a config file (JSON) with sensible defaults baked in.

---

## 4. The game loop

A fixed-timestep loop with input and rendering decoupled from the simulation tick:

```
loop:
    drain input channel  -> update pending direction
    if running:
        if time since last tick >= current_tick_interval:
            engine.Update(direction)      # move, collide, eat, score
            renderer.Draw(engine.State)   # diff + batched write
            advance tick timing
    else (paused / menu):
        render overlay, idle lightly
    precise short wait (do not busy-spin the CPU)
```

- **Speed-up** = shrinking `current_tick_interval` over time, driven by the active difficulty's curve.
- Timing uses `Stopwatch` for resolution; avoid relying on `Thread.Sleep` precision for the tick itself.

---

## 5. Difficulty & level behaviour

| Mode        | Speed-up        | Obstacles                    | Pause | Multiplier |
|-------------|-----------------|------------------------------|-------|------------|
| Easy        | none            | none (static board)          | yes   | x1         |
| Pro         | gradual         | appear/disappear dynamically | no    | higher     |
| Terminator  | gradual, faster | more frequent, dynamic       | no    | highest    |

- Three levels layer on top, each contributing its own parameters and a level multiplier.
- All of the above are values read from Settings, so balancing is tuning data, not code changes.

---

## 6. Suggested project layout

```
t-snake/
  src/
    TSnake/
      Program.cs
      Engine/        (state, rules, collision, food, obstacles, scoring)
      Input/         (reader, key mapping, direction state)
      Rendering/     (buffers, diff, themes: unicode/ascii)
      Persistence/   (highscore store)
      Settings/      (difficulty + level config, render mode)
  tests/
    TSnake.Tests/    (engine rules: movement, wrap, collision, scoring)
  docs/
    HIGH_LEVEL_PLAN.md
    AI_DEV_LOG.md
```

---

## 7. Anticipated risks (to confirm during build)

- **Input latency / dropped keys at high speed** — main risk; mitigated by the dedicated reader + channel, with the P/Invoke fallback in reserve.
- **Flicker / tearing in Windows Terminal** — mitigated by diff rendering + single batched writes; needs real measurement.
- **Timing precision** for the tick at fast speeds — Stopwatch-driven loop; verify on target hardware.
- **Terminal state restoration** on quit/crash (cursor, alt buffer, colors) — handle in a finally/dispose path.

---

## 8. Rough roadmap

1. Project skeleton, settings model, terminal setup (VT mode, alt buffer, encoding).
2. Engine core + unit tests (movement, wrap, collision, eat/grow, scoring) — headless.
3. Renderer: buffers, diff, batched write, both themes.
4. Input module: reader thread + channel + key mapping; wire into the loop.
5. Game loop + difficulty/level driving (speed-up, obstacles).
6. High-score persistence + menus (difficulty, level, render mode, scores).
7. Tuning, hardening, and measurement (latency, flicker) on the target setup.

Each step gets its own detailed plan before its Claude Code session.
