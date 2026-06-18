# AI-Assisted Development Log — t-snake

A running log of obstacles, friction points, and notable decisions encountered while building this project with AI assistance. A **summary of this log is required for the final exam documentation**, so keep entries short, honest, and dated.

## How to use

Add one entry per obstacle (or notable decision). Keep it factual: what happened, how it was resolved, and what was learned. The "Category" tag makes it easy to group things for the summary later.

Suggested categories: `prompting`, `tooling`, `design`, `bug`, `terminal/rendering`, `input`, `performance`, `dotnet/api`, `process`, `other`.

---

## Log

### YYYY-MM-DD — <short title>
- **Category:** <tag>
- **Context:** What I was trying to do / which part of the project.
- **Obstacle:** What went wrong or got in the way.
- **Resolution:** How it was solved (or worked around, or still open).
- **Lesson / note:** What to remember for next time.

---

<!-- Copy the block above for each new entry. Newest entries at the top or bottom — pick one and stay consistent. -->

### 2026-06-17 — Engine core: collision modelling & the tail-follow off-by-one
- **Category:** design
- **Context:** Plan 01 — building `TSnake.Core` (deterministic, tick-based engine) and its tests.
- **Obstacle:** Two correctness traps. (1) How to represent a collision — tempting to add a `CellKind.Collision`, but a crashed cell still *contains* a body segment or obstacle, so collision is an event, not content. (2) The classic self-collision off-by-one: moving the head onto the current tail cell is legal *when the tail is about to vacate* (not growing this tick), and illegal when growing/eating.
- **Resolution:** (1) Collisions are reported on `TickResult` (`EndReason` + `CollisionCell`); `CellKind` stays pure content. (2) Computed `willVacateTail = !eating && pendingGrowth == 0` and excluded the tail from the self-hit check only then. Both got dedicated tests (`FollowingTheVacatingTailIsLegal` vs `SelfCollisionEndsGame`), with a length-5 loop to bite a non-tail segment and a length-4 loop to land on the vacating tail.
- **Lesson / note:** Make randomness injectable (`IRandom`) from day one — a `FakeRandom` returning a scripted index sequence made food/obstacle placement exact and every rule headlessly testable. A 5×1 board kept free-cell ordering trivial for the obstacle-hit test.

### 2026-06-17 — Rendering: the score that wasn't in the contract, and P/Invoke for VT mode
- **Category:** terminal/rendering
- **Context:** Plan 02 — building the renderer (`TerminalSession`, two themes, `BoardGeometry`, `FrameComposer`, `ConsoleRenderer`) and a throwaway harness.
- **Obstacle:** Two snags. (1) `IRenderer.Apply(TickResult)` is the documented fast-path signature, but `TickResult` carries no score — so the renderer literally can't read the new score to repaint the HUD. (2) Enabling Windows virtual-terminal processing needs `GetStdHandle`/`Get`/`SetConsoleMode`; the modern `[LibraryImport]` source generator emits `unsafe` marshalling code, which fails to compile under our default settings.
- **Resolution:** (1) Kept the interface verbatim and tracked the score locally in `ConsoleRenderer`: `Begin`/`Redraw` seed it from the snapshot, and a tick containing a `Food` cell change (a newly spawned food ⇒ the snake just ate) bumps it by an injected `pointsPerFood`. `FrameComposer` stays pure — it's just handed a number. (2) Added `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` to the `TSnake` csproj. Per-cell color reset in the composer prevents color bleed between cursor moves.
- **Lesson / note:** A delta-based render contract should carry *enough* state to paint the HUD, or the consumer ends up re-deriving it (here, "did a `Food` change appear?"). Fine for a throwaway harness; worth revisiting if the score formula gains per-tick variation. Also: the pure compose/write split paid off immediately — every escape sequence (positions, glyphs, truecolor SGR, "no color in ASCII mode") is asserted in unit tests with zero `Console` involvement.

### 2026-06-18 — Game loop: testing time without a clock, and the pause teleport trap
- **Category:** design
- **Context:** Plan 04 — the `GameLoop` orchestrator plus its pure timing units (`SpeedProfile`, `TickClock`, `GameOutcome`, `WinTimerResolution`), replacing the input harness's `Thread.Sleep` pacing.
- **Obstacle:** Three snags. (1) The plan's `SpeedProfile.IntervalFor(GameState)` is hard to unit-test because `GameState`'s constructor is `internal` to `TSnake.Core` — tests can't fabricate a state with an arbitrary snake length. (2) The classic pause bug: if the `Stopwatch` keeps running while paused, `Elapsed − lastTick` is huge on resume and the loop fires a burst of catch-up ticks that teleports the snake. (3) Picking a speed-up *driver* metric without adding state to the engine — there's no `FoodEaten` field on `GameState`.
- **Resolution:** (1) Extracted the pure math into `IntervalAt(int progress)` and tested that directly (base at 0, multiplicative shrink, floored at min, zero-shrink constant); `IntervalFor(GameState)` just delegates, covered by one engine-backed test. (2) `TickClock.Advance` drops the backlog (snaps `lastTick = now`) past 2× the interval, and on resume the loop snaps `lastTick = sw.Elapsed` so no burst fires — both modeled with hand-fed timestamps, no real clock. (3) Used snake length as the `FoodEaten` proxy (the snake lengthens only by eating), avoiding any Core change. The inter-iteration wait sleeps the bulk then spins the final ~2 ms, capped at a 5 ms poll slice so quit/pause stay responsive.
- **Lesson / note:** When a public method takes a hard-to-construct type, extract the logic-heavy core onto a primitive-typed overload and test *that* — the wrapper becomes trivial. Timing code is testable if you keep it as pure functions of `(now, lastTick, interval)` and inject the clock only at the edge (`GameLoop` holds the lone `Stopwatch`). The orchestration itself (spin-wait feel, smoothness, pause-with-no-teleport) is still judged by playing — not everything wants a unit test.

### 2026-06-18 — Game loop: a "done" that wasn't — a proxy metric and a test that masked it
- **Category:** process
- **Context:** Plan 04, immediately after declaring it done. To avoid touching `TSnake.Core`, the speed-up driver used the snake's **length** as a stand-in for "food eaten" (see the previous entry, resolution #3). Build was green, all tests passed, and the speed visibly ramped, so the AI reported the plan complete.
- **Obstacle:** Play-testing exposed two problems. (1) Snake length includes the initial 4 segments, so `(1 − rate)^4` was applied *before the first food* — the game started already shrunk below the base interval ("too fast for an entry level"). (2) The plan's own testing note says *"`IntervalFor` — starts at base"*, but the shipped `IntervalFor(state)` did **not** start at base. The unit test asserted `IntervalAt(0) == base` on the *pure helper* while the real `GameState` path went through the length proxy — so the suite was green while the actual behaviour was wrong. The extract-and-test-the-core pattern (praised in the previous entry) had quietly tested the wrong path and given false confidence.
- **Resolution:** Added a real `FoodEaten` counter to the engine and `GameState` (the small Core change originally avoided), pointed `SpeedProfile` at it, and rewrote the integration test to drive a real engine through one food and assert `IntervalFor(state)` starts at base then shrinks one step. Also slowed the placeholder harness numbers for a comfortable entry pace.
- **Lesson / note:** "Builds + tests pass" is not "done" — especially when a shortcut deviates from the plan's stated intent. Two concrete rules learned: (a) when you substitute a proxy for what the spec asks (length for food eaten), flag it as a deviation out loud rather than reporting completion; the cheap Core change would have been correct from the start. (b) A test on an extracted pure helper only earns confidence if the production path actually flows through it — assert the end-to-end behaviour (`IntervalFor(state)`), not just the inner function, or the test can pass while the integration is broken. The human play-test caught exactly the case the proxy broke.

---

## Summary (for exam docs — fill in at the end)

A short synthesis to write once the project is done:

- **Most common type of obstacle:** 
- **Biggest single blocker and how it was overcome:** 
- **Where AI assistance helped most:** 
- **Where AI assistance fell short / needed correction:** 
- **What I'd do differently next time:** 
