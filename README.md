# t-snake

T-Snake is a remake of the classic arcade Snake game, built to run entirely inside **Windows Terminal**. It is a tribute to the earliest versions of the game and a proof-of-concept project (not commercial) created for an AI-assisted development course.

## What it is

A keyboard-driven terminal Snake game written in **C# on .NET 10 (LTS)**. Classic rules apply: eat to grow and score, die if you bite yourself or hit an obstacle. The screen edges wrap around (snake reappears on the opposite side).

## Features

- **Three difficulty tiers**
  - *Easy* — nothing changes during play except the snake growing; pause allowed.
  - *Pro* — gradual speed-up, plus obstacles that appear and disappear dynamically.
  - *Terminator* — like Pro, but faster speed-up and more frequent obstacles.
- **Three levels** with their own parameters.
- **Scoring** based on food eaten, with multipliers for harder modes and higher levels. Persistent high-score table.
- **Two render modes**
  - *Unicode + color* — smoother, more detailed, playful look.
  - *ASCII monochrome* — deliberately retro (e.g. `@` for the snake head).
- **Controls** — keyboard only: WASD and arrow keys. `Space` pauses (Easy mode only).

## Requirements

- Windows + Windows Terminal
- .NET 10 SDK or later

> Not designed or tested for other terminals or platforms.

## Building and running

The solution is `TSnake.slnx` at the repo root.

```bash
dotnet build                     # build all projects (warnings are treated as errors)
dotnet test                      # run the full xUnit suite
dotnet run --project src/TSnake  # launch the game (needs a real interactive terminal)
```

The game is an interactive TUI, so `dotnet run` only works in a real terminal. Unit tests cover the pure logic (engine rules, input mapping, timing, settings, persistence, screen models); rendering and input *feel* are verified by playing.

## Project structure

The codebase is split into three projects, with a one-way reference direction (`TSnake → TSnake.Core`, `TSnake.Tests → TSnake.Core`):

- **`src/TSnake.Core`** — the deterministic, tick-based game engine. References nothing of ours and never touches `Console` or I/O; this purity is compiler-enforced by the project split and is what makes the rules headlessly unit-testable.
- **`src/TSnake`** — the console application: input (non-blocking keyboard reader), rendering (pure frame composition + thin batched writer, two themes), the `Stopwatch`-driven game loop, settings, persistence, and the screen state machine.
- **`tests/TSnake.Tests`** — the xUnit suite covering the pure logic.

## Status

Implemented and playable. All planned modules are in place — engine, rendering, input, game loop, settings, persistence, and screens (menu → game → results) — each backed by unit tests. Remaining work is tuning, hardening, and measurement (input latency, flicker) on the target setup.

See [`docs/HIGH_LEVEL_PLAN.md`](docs/HIGH_LEVEL_PLAN.md) for the architecture and roadmap, the per-module plans in [`docs/01..07-*.md`](docs/), and [`docs/AI_DEV_LOG.md`](docs/AI_DEV_LOG.md) for the running log of obstacles and decisions during development.

## License

POC / educational project.
