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

---

## Summary (for exam docs — fill in at the end)

A short synthesis to write once the project is done:

- **Most common type of obstacle:** 
- **Biggest single blocker and how it was overcome:** 
- **Where AI assistance helped most:** 
- **Where AI assistance fell short / needed correction:** 
- **What I'd do differently next time:** 
