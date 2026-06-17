# Detailed Plan 01 — Solution Setup & Engine (Core)

This is the first per-module plan. It covers two things, because they belong together: how the solution is laid out, and the design of the **Engine** — the pure game core that everything else depends on. Nothing here touches the keyboard or the screen; those are later plans.

---

## Part A — Solution setup

### A.1 Project structure

The high-level plan put everything in one project. This plan refines that: the engine lives in its **own class library** so it physically cannot reference any console/I/O type. The boundary "the engine has no I/O" stops being a rule we have to remember and becomes something the compiler enforces.

```
t-snake/
  TSnake.sln
  Directory.Build.props          # shared build settings
  .editorconfig
  src/
    TSnake.Core/                 # the engine: pure logic, NO Console, NO I/O
    TSnake/                      # console app: Input, Rendering, Persistence, wiring
  tests/
    TSnake.Tests/                # unit tests, references TSnake.Core only
  docs/
    HIGH_LEVEL_PLAN.md
    AI_DEV_LOG.md
    plans/
      01-solution-and-engine.md  # this file
```

Reference direction (one-way, no cycles): `TSnake` → `TSnake.Core`, and `TSnake.Tests` → `TSnake.Core`. The Core project references nothing of ours.

### A.2 Scaffolding commands

Run from the repo root. Target framework is `net10.0` (current LTS, confirmed).

```bash
dotnet --version                 # sanity-check: should be 10.x

dotnet new sln    -n TSnake
dotnet new classlib -n TSnake.Core  -o src/TSnake.Core
dotnet new console  -n TSnake       -o src/TSnake
dotnet new xunit    -n TSnake.Tests -o tests/TSnake.Tests

dotnet sln add src/TSnake.Core src/TSnake tests/TSnake.Tests
dotnet add src/TSnake reference src/TSnake.Core
dotnet add tests/TSnake.Tests reference src/TSnake.Core

# remove the placeholder Class1.cs that classlib generates
del src\TSnake.Core\Class1.cs    # PowerShell/cmd
```

(Test framework: xUnit is the default choice here — well documented and friendly to AI-assisted work. If you'd rather use something else, this is the moment to swap it.)

### A.3 Shared build settings

`Directory.Build.props` at the repo root, so all three projects inherit the same rules:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

`Nullable=enable` is worth keeping on from day one — for a game full of grid lookups it catches a class of null bugs before they happen. `TreatWarningsAsErrors` keeps the codebase honest; relax it later only if it gets in the way.

The existing `.gitignore` is the standard Visual Studio template and already ignores `bin/`, `obj/`, and friends, so no change is needed there.

### A.4 Definition of done for setup

`dotnet build` and `dotnet test` both succeed from a clean checkout, the solution opens cleanly, and the three projects reference each other as described. A single trivial passing test in `TSnake.Tests` proves the test pipeline works end to end.

---

## Part B — Engine (TSnake.Core)

### B.1 What the engine owns, and what it does not

**Owns (pure logic):** the board, the snake, the food, the obstacles, the score, the game status, and the rule for advancing one step (move, wrap, collide, eat, grow, score, spawn).

**Does NOT own:** reading the keyboard (it is *given* a direction), drawing (the renderer *reads* its state), saving high scores, and timing/speed. Speed is purely how often the outer loop calls the engine — the engine itself has no concept of wall-clock time.

### B.2 The core design decision: deterministic, tick-based

The engine advances in discrete **ticks**. One `Tick()` = the snake moves exactly one cell. It never measures real time. Two consequences, both deliberate:

- **Speed is the loop's job.** "Speed-up" in Pro/Terminator means the loop calls `Tick()` more frequently. The engine code is identical at every speed. Anything time-like inside the engine (e.g. how often obstacles appear) is counted in *ticks*, not milliseconds.
- **Same seed + same inputs ⇒ identical game.** All randomness (food placement, obstacle spawning) goes through one injected, seedable random source. Given a seed and a fixed sequence of directions, the engine reproduces the exact same run every time. This is what makes the whole core unit-testable and bug reports reproducible.

### B.3 Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `Point` | readonly record struct (X, Y) | grid coordinate; value equality so it works as a `HashSet`/`Dictionary` key |
| `Direction` | enum (Up, Down, Left, Right) | heading; has a delta and an "opposite" helper |
| `CellKind` | enum (Empty, SnakeHead, SnakeBody, Food, Obstacle) | *semantic* content of a cell — no glyphs/colors, so it stays pure |
| `GameStatus` | enum (Running, Paused, GameOver, Won) | current state of play |
| `EndReason` | enum (None, HitSelf, HitObstacle, BoardFull) | *why* the game ended; drives the death effect and the results-screen message |
| `GameConfig` | record | all tuning inputs the engine needs (see B.6) |
| `GameState` | read-only view | snapshot the renderer reads: dimensions, snake cells, food, obstacles, score, status, tick count |
| `CellChange` | readonly struct (Point, CellKind) | one cell that changed this tick |
| `TickResult` | record | what happened this tick: the list of `CellChange`s, plus `EndReason` and a `Point? CollisionCell` (the cell the head tried to enter when it died) |
| `IRandom` | interface | abstraction over the random source so tests can inject a deterministic one |
| `GameEngine` | class | the engine itself |

> **On representing collisions.** A collision is treated as an *event* on `TickResult` (`EndReason` + `CollisionCell`), not as a `CellKind`. `CellKind` describes what *occupies* a cell, and after a crash that cell still holds a body segment or an obstacle — so "collision" isn't content, it's a moment. Keeping it on `TickResult` also tells the renderer and the results screen *what* was hit and *where*, which a single `CellKind.Collision` would flatten. (Alternative, if you'd prefer the renderer to be a pure paint-by-kind loop with no special-casing: add `CellKind.Collision` and mark the fatal cell with it. Simpler render path, slightly less honest `CellKind`. Listed as an open decision below.)

### B.4 Data structures (the parts that matter for correctness/perf)

- **Snake body:** an ordered structure (e.g. `LinkedList<Point>` or a deque) so head-add and tail-remove are O(1), *plus* a `HashSet<Point>` of occupied snake cells so "did the head hit the body?" is an O(1) lookup instead of an O(length) scan. Keep the two in sync.
- **Obstacles:** `HashSet<Point>`, with per-obstacle bookkeeping (the tick it should despawn) for the dynamic modes.
- **Food:** a single `Point` (classic Snake). Multiple-food is a possible later variation, not in scope now.

### B.5 The `Tick()` algorithm

Given the buffered next direction, one tick does:

1. **Resolve direction.** Adopt the buffered direction unless it is the direct opposite of the current heading (you cannot reverse onto your own neck). Otherwise keep the current heading.
2. **Compute the new head** = current head + direction delta, then apply **wrap-around** with modulo over board width/height so the edges connect.
3. **Collision checks at the new head cell.** On a hit, set `Status = GameOver`, record the `EndReason`, set `CollisionCell` to this cell, and do **not** advance the snake into it (the head stops at the failed move; the renderer draws the crash there):
   - hits an obstacle → `EndReason.HitObstacle`.
   - hits the snake body → `EndReason.HitSelf` — **with one classic subtlety**: if the snake is *not* about to grow this tick, the current tail cell is being vacated, so moving into it is legal. Exclude the tail from the self-collision check unless this tick eats food. (This is exactly the kind of off-by-one rule that bites people — it gets its own test, and probably its own dev-log entry.)
4. **Resolve the move:**
   - if the new head is on the food: **grow** (add head, do *not* remove tail), add score (`base × difficulty × level` multipliers), then spawn new food on a random free cell. If there is no free cell left, set `Status = Won` / `EndReason.BoardFull`.
   - otherwise: normal move (add head, remove tail).
5. **Obstacle dynamics** (Pro/Terminator only): based on config rates and the tick count / RNG, possibly spawn or despawn obstacles. A newly spawned obstacle must never land on the snake, the food, or an existing obstacle. (Easy mode config disables this entirely — the board stays static.)
6. **Increment the tick count** and record every cell that changed into the `TickResult`.

### B.6 What the engine is given (`GameConfig`)

A plain record so the engine never has to know about settings files or difficulty *names* — it just gets numbers:

- Board width and height.
- Score: base points per food, growth per food (default 1).
- Multipliers: difficulty multiplier, level multiplier.
- Obstacle behaviour: enabled (bool), spawn rate, lifetime, max simultaneous obstacles.
- Initial snake length / start position.
- RNG seed (so a run can be reproduced).

The Settings module (a later plan) is responsible for translating "Pro, level 2" into one of these `GameConfig` records. The engine stays oblivious.

### B.7 Public surface (signatures only — implementation happens in the Claude Code session)

```csharp
public sealed class GameEngine
{
    public GameEngine(GameConfig config, IRandom rng);

    public GameState State { get; }     // read-only view for the renderer
    public GameStatus Status { get; }

    public void SetDirection(Direction next);   // overwrite the single pending-direction slot
    public TickResult Tick();                   // advance one step; returns what changed
}
```

**The engine holds one pending-direction slot, not a queue.** `SetDirection` overwrites it; `Tick()` applies it (with the reversal guard against the live heading). The input *channel* is just transport between the reader thread and the loop — it is the **loop** that decides which single direction to hand the engine each tick. How the loop collapses or buffers presses (latest-wins vs. a small bounded turn buffer) is an input-policy decision deferred to the Input plan (02); it does not change this engine contract.

The renderer never recomputes the scene: it reads `State` once to draw the initial frame, then on each tick paints only the cells in `TickResult.Changes`. That keeps per-frame work to the 2–4 cells that actually moved, which is the whole point of the rendering approach we settled on.

### B.8 Edge cases to handle (and to turn into tests)

- Self-collision **vs.** the legal "follow your own vacating tail" move.
- Reversal rejection (Left while moving Right is ignored, not fatal).
- Wrap-around correct at all four edges and the four corners.
- Food respawn must pick a cell that is free of snake **and** obstacles.
- **Board full:** no free cell left to place food → treat as `Won` (a clean terminal state rather than a crash).
- Obstacle spawn never overlaps snake, food, or another obstacle.
- Multiple presses between ticks: the engine only ever has the one direction the loop handed it via `SetDirection` for that tick. (Whether the loop keeps the *latest* press or feeds a queued sequence is the Input plan's concern, not the engine's.)

### B.9 Testing plan (TSnake.Tests)

All tests are headless and deterministic via a seeded `IRandom`. Coverage to aim for:

- Movement in each of the four directions advances the head by one cell.
- Wrap-around at every edge.
- Eat → length grows by `growth per food`, score increases by the right multiplied amount, new food appears on a free cell.
- Self-collision ends the game; following the vacating tail does **not**.
- Hitting an obstacle ends the game.
- Reversal input is ignored.
- Board-full produces `Won`.
- **Invariants** (good as property-style checks): the snake never contains a duplicate cell; the head is always inside the board; snake length matches food eaten × growth + start length.

### B.10 Definition of done for the engine

`TSnake.Core` builds with no I/O dependencies, the test suite above passes, and a tiny throwaway console harness (or a test) can run a scripted sequence of directions to completion. At that point the game is fully *playable in logic* — no screen yet, but every rule works and is proven.

---

## Decisions to confirm before the session

These were baked in as defaults; items 1–6 are now **confirmed**. Two further decisions came out of the design review:

1. ✅ **Engine as a separate `TSnake.Core` library** (vs. a folder in the app). Recommended for the enforced boundary.
2. ✅ **xUnit** as the test framework.
3. ✅ **Single food** on the board at a time (classic).
4. ✅ **Growth = 1 segment** per food.
5. ✅ **Board full = Won** as the only non-death end state.
6. ✅ **Easy mode = obstacles disabled** in config (matches the spec: nothing changes during play except growth).
7. ✅ **Collision representation** — event on `TickResult` (`EndReason` + `CollisionCell`); `CellKind` stays pure content.
8. **Input buffering policy** (latest-wins vs. bounded depth-2 turn buffer) — does **not** affect the engine; deferred to the Input plan (02). The engine contract is fixed: one pending slot + reversal guard.

Once these are settled, the Claude Code session for this plan is: scaffold the solution (Part A), then build `TSnake.Core` and its tests (Part B) until B.9 is green.
