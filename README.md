# t-snake

T-Snake is a remake of the classic arcade Snake game, built to run entirely inside **Windows Terminal**. It is a tribute to the earliest versions of the game and a proof-of-concept project (not commercial) created for an AI-assisted development course.

## What it is

A keyboard-driven terminal Snake game written in **C# on .NET 10 (LTS)**. Classic rules apply: eat to grow and score, die if you bite yourself or hit an obstacle. The screen edges wrap around (snake reappears on the opposite side).

## Features (planned)

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

## Status

Early planning. See [`docs/HIGH_LEVEL_PLAN.md`](docs/HIGH_LEVEL_PLAN.md) for the architecture and roadmap, and [`docs/AI_DEV_LOG.md`](docs/AI_DEV_LOG.md) for the running log of obstacles encountered during development.

## License

POC / educational project.
