# Detailed Plan 05 — Settings (Config Translation & Preferences)

The fifth per-module plan. Settings is the piece both the engine and the loop quietly depend on: it's where a human-facing choice — "Pro, level 2, Unicode" — becomes the plain-number records those modules consume. It also holds the player's preferences and persists them. It is the home of the high-level plan's third pillar: *difficulty and levels are data, not hard-coded branches*.

It lives in the console app (`src/TSnake/Settings/`), references `TSnake.Core` (for `GameConfig`, `Point`) and the loop's `SpeedProfile`, and deliberately does **not** reference the renderer (it holds a render-mode *preference*; composition maps that to a theme).

---

## 1. What settings owns, and what it does not

**Owns:** the tuning data for every difficulty and level, the rule that combines them into a runnable session config, the user's preferences (render mode, chosen difficulty/level, player name), and loading/saving that to a JSON file with defaults.

**Does NOT own:** the game rules or timing (it just produces the configs those modules read), the *menu UI* that lets a player choose (Screens plan), the high-score table (Persistence plan), and the difficulty *behaviour* itself — it only emits the numbers; the engine and loop act on them.

---

## 2. The core design decisions

### 2.1 Difficulty and level are separate data, combined by one explicit rule

Instead of one flat table with an entry per (difficulty, level), the tuning splits the way the domain does:

- **`DifficultyProfile`** — what *difficulty* governs: the score multiplier, whether pause is allowed, whether obstacles are enabled, their spawn rate / lifetime / max, and the speed-up **shrink rate**.
- **`LevelProfile`** — what *level* governs: board width/height, base & minimum tick interval, initial snake length, base points per food, growth per food, and the level multiplier.

Three of each (Easy/Pro/Terminator; levels 1–3), combined by a single, surprise-free rule:

| Output field | Source |
|---|---|
| board W/H, base points, growth, initial length | `LevelProfile` |
| base & min tick interval | `LevelProfile` |
| obstacles enabled, spawn rate, lifetime, max | `DifficultyProfile` |
| speed-up shrink rate | `DifficultyProfile` |
| score = base × **difficulty mult** × **level mult** | both multipliers |
| pause allowed | `DifficultyProfile` |
| start position | computed (board centre) |
| seed | passed in per session (§2.4) |

This is DRYer than 9 fat rows (Easy's "pause = true" is stated once, not three times) and matches "levels layer on top of difficulty." The merge is the testable heart of the module:

```csharp
public SessionConfig BuildSession(Difficulty difficulty, int level, int seed);
// returns: record SessionConfig(GameConfig Engine, SpeedProfile Speed, bool PauseAllowed);
```

`SessionConfig` is exactly the three things composition needs: the engine's config, the loop's `SpeedProfile` (with `Driver = FoodEaten`, fixed), and the `pauseAllowed` flag.

### 2.2 Defaults in code; the file overlays, and never crashes the game

- A **complete set of defaults** is defined in code, so the game runs with no file at all.
- On load, if `settings.json` exists it's read and **overlaid** onto the defaults — a partial or hand-edited file overrides only the fields it specifies; anything missing falls back to a default.
- **Bad input never crashes.** Malformed JSON, out-of-range numbers (negative interval, board smaller than the snake, spawn rate outside 0–100, min interval above base) are validated and rejected field-by-field back to the default. A typo in the config file degrades gracefully; it never takes the game down.

### 2.3 Persist preferences, not the whole balance table

The file's job day-to-day is to remember **preferences**: render mode, the last-played difficulty/level, and the player name. The tuning table stays code-defined. A tinkerer *can* still override tuning via an optional `difficulties` / `levels` section in the same file (honouring the data-driven pillar), but a normal player only ever writes the small `preferences` block.

This sidesteps a trap: if we persisted the full tuning table, a later change to the default balance in code would be silently overridden by every existing user's frozen copy. Persisting only preferences keeps balance updates effective while still letting power users override on purpose.

```csharp
public sealed record Preferences(RenderMode RenderMode, Difficulty Difficulty, int Level, string PlayerName);
```

### 2.4 Seed is per-run, not a stored setting

`GameConfig.Seed` makes a run reproducible, but for normal play each game should differ — so composition generates a **fresh random seed per session** and passes it to `BuildSession`. It isn't a persisted preference. (A fixed seed can be injected for reproducible/debug/competitive runs, which dovetails with the engine's determinism from plan 01.)

---

## 3. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `Difficulty` | enum (`Easy`, `Pro`, `Terminator`) | app-side name; the engine/loop never see it |
| `RenderMode` | enum (`UnicodeColor`, `AsciiMonochrome`) | a preference; composition maps it to an `ITheme` |
| `DifficultyProfile` | record (pure) | the difficulty-governed numbers (§2.1) |
| `LevelProfile` | record (pure) | the level-governed numbers (§2.1) |
| `Preferences` | record | render mode, difficulty, level, player name |
| `SessionConfig` | record | `GameConfig` + `SpeedProfile` + `PauseAllowed` |
| `Settings` | class | holds preferences + tuning; `BuildSession`, `LoadOrDefault`, `Save` |

Levels are referenced as `int` 1–3 (simple, easy to range-check) rather than an enum, since they're naturally ordinal and the count may flex.

---

## 4. File location & format

- **Path:** `%APPDATA%\t-snake\settings.json` via `Environment.GetFolderPath(SpecialFolder.ApplicationData)`; the folder is created if missing. (Persistence, plan 06, shares this folder.)
- **Serialization:** `System.Text.Json`. Records deserialize via STJ's parameterized-constructor support; `enum`s are written as strings (`JsonStringEnumConverter`) so the file is human-readable and editable.
- **Path is injectable.** `Settings.LoadOrDefault(path)` / `Save(path)` take the location (defaulting to the AppData path) so tests can point at a temp directory. No hidden global state.
- On first run with no file, the game uses defaults and writes a `settings.json` so the player can discover and edit it.

---

## 5. Public surface

```csharp
public sealed class Settings
{
    public Preferences Preferences { get; private set; }

    public static Settings LoadOrDefault(string? path = null);  // read + overlay + validate; defaults on any failure
    public void Save(string? path = null);                      // persist preferences (+ any overrides)

    public SessionConfig BuildSession(Difficulty difficulty, int level, int seed);
}
```

Composition (`Program`) ties it together — this is now nearly the whole app wiring:

```
var settings = Settings.LoadOrDefault();
var p        = settings.Preferences;
var seed     = Random.Shared.Next();
var session  = settings.BuildSession(p.Difficulty, p.Level, seed);

var engine   = new GameEngine(session.Engine, new SeededRandom(seed));
var theme    = ThemeFor(p.RenderMode);          // Program maps RenderMode -> ITheme
using var ui    = new ConsoleRenderer(theme);
using var input = new InputService(new ConsoleKeySource());

var loop     = new GameLoop(engine, ui, input, session.Speed, session.PauseAllowed);
var outcome  = loop.Run();
```

The only things still missing after this are the **menu** that sets `p` interactively, the **results screen**, and **high-score saving** — all later plans.

---

## 6. Testing plan

Settings is almost entirely pure, so it tests cleanly with no Console and a temp file path:

- **`BuildSession` merge** — for representative combos: Easy → `ObstaclesEnabled = false`, `PauseAllowed = true`, shrink rate 0; Terminator → obstacles on, higher spawn rate, steeper shrink, `PauseAllowed = false`; level drives board size / base interval / level multiplier.
- **Multiplier composition** — score multiplier is difficulty × level for several pairs.
- **Start position** — computed as the board centre for each level's dimensions.
- **Defaults** — `LoadOrDefault` with no file yields the full default set.
- **Overlay** — a partial file overrides only its fields; the rest stay default.
- **Validation** — malformed JSON and out-of-range values fall back to defaults and never throw.
- **Round-trip** — `Save` then `LoadOrDefault` reproduces the same `Preferences`.

What stays manual: confirming that editing `settings.json` by hand visibly changes the game, and that the AppData file is created on first run.

---

## 7. Definition of done

- With no config file, the game runs on baked-in defaults and writes a readable `settings.json`.
- The chosen difficulty/level/render-mode actually take effect end to end: Easy is calm, pausable, obstacle-free; Terminator is fast and obstacle-heavy; switching render mode swaps the theme; editing the file changes the game on next launch.
- A corrupt or nonsensical config degrades to defaults without crashing.
- All the §6 pure tests pass.

---

## Decisions — confirmed

1. ✅ **Structured tuning** — 3 `DifficultyProfile` + 3 `LevelProfile`, combined by the §2.1 rule (not a flat 9-entry table).
2. ✅ **Persist preferences only**; tuning stays code-defaults, optionally overridable via the file. (Whole-table persistence rejected for the frozen-balance trap.)
3. ✅ **`%APPDATA%\t-snake\settings.json`**, `System.Text.Json`, string-valued enums, **injectable path** for tests, folder auto-created, default file written on first run.
4. ✅ **`Difficulty`/`RenderMode` enums live in the app**; engine/loop stay name-agnostic. **Levels as `int` 1–3.**
5. ✅ **Seed is per-run** (fresh random from composition), not a stored setting; fixed seed injectable for repro.
6. ✅ **Start position = board centre**, computed (not a tuning field).
7. ✅ **Render mode is a preference**; `Program` maps it to the theme — Settings does not reference Rendering.
8. ✅ **Invalid config → defaults, never crash** (field-by-field validation).
9. ✅ **Scope:** Settings produces config + holds preferences; the menu UI that lets the player *choose* them is the Screens plan, and player-name entry happens there too.

Once settled, the session is: define `Difficulty`/`RenderMode`, `DifficultyProfile`/`LevelProfile` with full defaults, `Preferences`, `SessionConfig`, and `Settings` (`BuildSession`/`LoadOrDefault`/`Save`); write the pure tests; then wire `Program` to drive a configured session from the file.
