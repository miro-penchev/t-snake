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

### 2026-06-18 — Settings: a type named after its own namespace, and a workaround that should have been a fix
- **Category:** dotnet/api
- **Context:** Plan 05 — the Settings module (`TSnake.Settings`): difficulty/level tuning, the `BuildSession` merge, and JSON load/save with overlay + field-by-field validation. The plan prescribes both the folder/namespace `Settings` and a class named `Settings`.
- **Obstacle:** Following the plan literally produced a class `Settings` inside namespace `TSnake.Settings`. It compiled fine from top-level `Program` (no enclosing `TSnake.*` namespace), but every reference from the test project (`namespace TSnake.Tests`) failed: the bare identifier `Settings` binds to the sibling *namespace* `TSnake.Settings`, not the type, so `Settings.LoadOrDefault` became "namespace used like a type." A separate self-inflicted snag: naming a helper property `Path` shadowed `System.IO.Path`, breaking `Path.Combine` in a field initializer.
- **Resolution:** First pass worked around the collision with a `using SettingsModel = TSnake.Settings.Settings;` alias in the test — green, but ugly and contagious (every future `TSnake.*` consumer, e.g. the Screens menu, would need the same alias). On reviewer push-back the root cause was fixed instead: renamed the class to `GameSettings` (file, ctor, factory/return types, doc `<see cref>`s, `Program`, tests), dropping the alias entirely. The `Path` property was renamed to `FilePath`.
- **Lesson / note:** Don't name a type the same as a segment of its namespace — it resolves fine from outside the namespace tree but shadows the type for any sibling `TSnake.*` consumer. When a plan's suggested name causes a language-level collision, fix the name, don't paper over it with an alias: the alias spreads to every caller and hides the smell. Treat an alias-as-workaround as a signal to reconsider the name. (Also: avoid shadowing common BCL types like `Path` with members.)

### 2026-06-19 — Full game play-tested end to end: all plans implemented as designed
- **Category:** process
- **Context:** All seven plans now landed (Core → Rendering → Input → Game loop → Settings → Persistence → Screens). Ran several full game sessions by hand: main menu → difficulty/level selection → play (with pause, speed-up, food, growth, collisions) → game-over results → high-score table.
- **Obstacle:** None — this is a positive checkpoint rather than an obstacle. Worth recording precisely *because* the earlier game-loop entry (the "done that wasn't") showed that green builds and passing tests don't equal a good play experience; the only way to confirm the whole thing feels right is to actually play it.
- **Resolution:** Multiple manual sessions confirmed everything works smoothly and as planned — timing/speed-up feel right, pause has no teleport, rendering is clean, settings carry through into a session, scores persist and surface in the high-score view, and the screen flow ties it all together. No defects surfaced; all plans are implemented and behaving as designed.
- **Lesson / note:** The modular plan-by-plan approach paid off: each module was play-tested as it landed, so by the time the screens tied them together there were no integration surprises. The human play-test remains the final acceptance gate — it's what turns "builds + tests pass" into "confirmed done," and here it confirmed the whole build holds together.

### 2026-06-19 — Game-over explosion: a timed animation vs. the renderer's "doesn't own timing" rule
- **Category:** design
- **Context:** First post-plan polish item (not in any of the seven plans): a shockwave that erupts from the cell the snake died on and expands to consume the whole board before the results/name-entry screen appears.
- **Obstacle:** Two design tensions. (1) *Where does a timed animation live?* Plan 02 is explicit that the renderer "does NOT own timing/when to draw (the loop decides)" — yet an explosion is inherently a sequence of frames with delays between them. Putting `Thread.Sleep` pacing in `ConsoleRenderer` brushes against that boundary; putting per-frame composition in the loop would leak board geometry (origin→corner distance, cell mapping) out of the rendering layer that owns it. (2) *Making the blast look circular*, when a board cell is two terminal columns wide but one row tall — naïve column/row distance would render an ellipse.
- **Resolution:** Kept the project's pure-compose / thin-write split: `FrameComposer.ComposeExplosionFrame(origin, radius)` is a *pure* function returning one frame's ANSI (testable with zero `Console`), and `ITheme.Explosion(tier)` is a pure heat-ramp lookup (white-hot wavefront → burnt ember) added alongside the existing glyph table. The boundary call: the per-frame *sleep loop* lives in `ConsoleRenderer.PlayDeathEffect` (it owns geometry, composer, and the writer), and the loop merely *triggers* it once at the fatal tick — reading plan 02's rule as "the renderer doesn't drive the per-tick game cadence," which a one-shot end cinematic isn't. For (2), distance is measured in *board-cell units* (`BoardGeometry.MaxRadiusFrom`, and the composer's per-cell `sqrt(dx²+dy²)`), since the 2-wide cell is already designed to read as square — so the wave is a circle. A board-full *win* has no collision cell, so it ends quietly; only deaths explode. Pure parts got tests (composer wavefront-vs-center tiers, geometry max radius, theme tier clamping); the animation *feel* stays a play-test check.
- **Lesson / note:** A stated layering rule ("renderer doesn't own timing") is about the *hot path*, not an absolute — a self-contained end animation is a reasonable exception, but it's worth naming the exception out loud rather than silently breaking the rule. The pure-compose/thin-write split kept paying dividends: the visually complex bit (an expanding, cooling shockwave) is still just a pure string function under test, and only the sleep cadence is judged by eye. When geometry has a non-square mapping, pick the distance space deliberately (board cells, not terminal columns) or the effect distorts.

---

## Summary (for exam docs — fill in at the end)

A short synthesis to write once the project is done:

- **Most common type of obstacle:** 
- **Biggest single blocker and how it was overcome:** 
- **Where AI assistance helped most:** 
- **Where AI assistance fell short / needed correction:** 
- **What I'd do differently next time:** 
