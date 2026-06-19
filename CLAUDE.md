# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

T-Snake is a keyboard-driven terminal Snake remake in **C# on .NET 10**, targeting **Windows Terminal only** (ANSI/VT, truecolor, UTF-8). It's an educational proof-of-concept built module-by-module with AI assistance. The classic rules apply, with edges that **wrap around**, three difficulty tiers (Easy/Pro/Terminator), three levels, two render themes (Unicode+color / ASCII monochrome), and a persistent high-score table.

## Commands

The solution is `TSnake.slnx` at the repo root; run `dotnet` from there.

```bash
dotnet build                              # build all three projects (warnings are errors — see below)
dotnet test                               # run the full xUnit suite
dotnet test --filter FullyQualifiedName~FrameComposer   # run one test class
dotnet test --filter "Name=SelfCollisionEndsGame"       # run one test by method name
dotnet run --project src/TSnake           # launch the game (needs a real interactive terminal)
```

- **`TreatWarningsAsErrors=true`** (in `Directory.Build.props`, inherited by all projects): any warning fails the build. `Nullable` and `ImplicitUsings` are enabled solution-wide.
- The game is an interactive TUI — `dotnet run` only works in a real terminal, and the rendering/input *feel* can't be asserted in tests. **Behavioral verification is by playing it.** Unit tests cover the pure logic only.

## Architecture

### The hard boundary: engine purity is compiler-enforced

The engine lives in its own library, `src/TSnake.Core`, which references **nothing of ours and never touches `Console` or I/O**. The console app `src/TSnake` references Core; `tests/TSnake.Tests` references Core only. Reference direction is one-way (`TSnake → TSnake.Core`, `TSnake.Tests → TSnake.Core`). **Never add `Console`, file, threading, or timing code to `TSnake.Core`** — that boundary is the whole point, and the project split exists to enforce it.

### The engine is deterministic and tick-based (`TSnake.Core`)

One `Tick()` moves the snake exactly one cell. The engine has **no concept of wall-clock time** — "speed" and "speed-up" are entirely the outer loop calling `Tick()` more or less often. Anything time-like inside the engine (obstacle lifetimes) is counted in *ticks*. All randomness goes through an injected `IRandom`, so **same seed + same inputs ⇒ identical run** — that's what makes the rules headlessly unit-testable (tests inject `FakeRandom` with a scripted index sequence).

- `GameEngine` exposes `State` (a read-only `GameState` snapshot), `SetDirection(next)` (one pending-direction slot, not a queue), and `Tick()` returning a `TickResult`.
- A **collision is an event on `TickResult`** (`EndReason` + `Point? CollisionCell`), *not* a `CellKind`. `CellKind` stays pure content. Don't add `CellKind.Collision`.
- The engine takes a `GameConfig` of plain numbers; it never knows difficulty *names* (Settings translates those).

### Input is non-blocking by construction (`src/TSnake/Input`)

A background thread (`KeyboardReader`, `IsBackground`) sits on blocking `Console.ReadKey` via the swappable `IKeySource` seam and pushes raw keys into a `System.Threading.Channels` channel — **pure transport**. All mapping (`KeyMap`, pure) and buffering (`TurnBuffer`, pure **depth-2 FIFO**) happen consumer-side on the loop thread, so gameplay state needs no locks. The depth-2 buffer lets a quick two-turn corner survive without runaway turning. The reversal guard is split: the buffer rejects reversing the *previous buffered* turn; the engine rejects the first turn vs. the *live heading*.

### Rendering splits pure-compose from thin-write (`src/TSnake/Rendering`)

`FrameComposer` is **pure**: given geometry + theme + state, it returns the exact ANSI string — no `Console` calls, fully assertable in tests. `ConsoleRenderer` is the thin writer that issues **one batched write per frame**. Per tick it paints only `TickResult.Changes` (2–4 cells); `Redraw(GameState)` does a full repaint for disturbed-screen cases (start, resume-from-pause, resize). Themes (`UnicodeColorTheme`, `AsciiMonochromeTheme`) are **pure lookup tables** behind `ITheme`; all positioning/batching is shared. Each board cell is **2 terminal columns wide** for a square aspect — measure blast/animation distances in *board-cell units*, not columns, or circles become ellipses. Note: `TickResult` carries no score, so `ConsoleRenderer` tracks the score locally (a tick containing a new `Food` cell ⇒ the snake ate).

### The loop owns timing, not the renderer (`src/TSnake/Loop`)

`GameLoop` is `Stopwatch`-driven and iterates faster than it ticks: every iteration it polls input (so quit/pause feel instant even at slow tick rates), then ticks only when the interval has elapsed. Speed-up shrinks the interval as **food is eaten** (`GameState.FoodEaten` is the driver). Pause freezes the tick clock and snaps `lastTick` on resume so the snake never teleports. The timing math is extracted into pure, tested helpers (`SpeedProfile`, `TickClock`). Plan 02 states the renderer "does not own timing" — that means the per-tick cadence; a self-contained one-shot animation (e.g. the game-over blast) hosting its own frame delays in the renderer is the documented exception.

### Settings and Persistence share `%APPDATA%\t-snake\`

`src/TSnake/Settings` translates "Pro, level 2" into a `GameConfig` + speed/session config and stores user `Preferences` as `settings.json`. `src/TSnake/Persistence` stores the high-score table as `highscores.json`. Both follow the same robustness rules: **load is total** (missing/corrupt → defaults/empty, never throws; invalid entries dropped) and **save is atomic** (temp file + replace, so a mid-write crash can't destroy the file). `HighScoreTable` is one shared board (difficulty/level stored per entry; multipliers exist precisely so harder play ranks on a shared leaderboard); `Add` returns a 1-based rank and `Qualifies` gates the name prompt.

### Screens are the glue and the state machine (`src/TSnake/Screens`)

`Program.cs` is a thin composition root: it builds the long-lived shared services and wraps the whole run in a single `TerminalSession` so the terminal is **restored on every exit path** (normal, quit, exception). `AppController` is the state machine sequencing menu → game → results → menu. Screens **clear and fully redraw** on each change (opposite of the diffing board) and use a separate **blocking** `ScreenInput` off the *same* keyboard channel; the channel is **flushed on every phase transition** so a stray keypress doesn't leak between phases.

## Conventions and gotchas

- **Don't name a type after a segment of its own namespace.** A class `Settings` in namespace `TSnake.Settings` shadows the type for any sibling `TSnake.*` consumer — hence `GameSettings`. (See the dev log.)
- **Pure core, manual feel.** Extract the logic-heavy part into a pure, primitive-typed unit and test *that*; leave rendering smoothness, input responsiveness, and timing feel to play-testing. Beware: a test on an extracted helper only earns confidence if the production path actually flows through it.
- **"Builds + tests pass" is not "done."** The human play-test is the acceptance gate for anything visual or timing-related.

## Documentation to read first

- `docs/HIGH_LEVEL_PLAN.md` — the overall architecture, design pillars, and roadmap.
- `docs/01..07-*.md` — one detailed plan per module (engine, rendering, input, game loop, settings, persistence, screens). Each has a "definition of done" and the decisions that were settled. Read the relevant plan before changing a module.
- `docs/AI_DEV_LOG.md` — a running, dated log of obstacles and notable decisions. **Append a short entry here when you hit a real obstacle or make a non-obvious design call** (the project relies on it for an exam summary); newest entries at the bottom.

This is a trunk-based repo: commit straight to `main`, no feature branches (commit/push only when the user asks).
</content>
</invoke>
