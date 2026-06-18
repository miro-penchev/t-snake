# Detailed Plan 07 — Screens, Menu & Composition

The final per-module plan. Everything so far produced a *playable core*; this plan gives it a front door and stitches the six modules into a complete application. It is two things at once: the **text UI** (main menu, results/game-over, high-score view) and the **composition root** — the top-level state machine that runs menu → game → results → menu.

It lives in the console app (`src/TSnake/Screens/` and `Program.cs`), and it's the one place that references *everything*: the engine, renderer, input, loop, settings, and persistence. This is also where threads deferred from earlier plans land: the centered-box primitive (plan 02), the raw-key channel for navigation (plan 03), player-name entry (plans 05 & 06), and the `EndReason` → message mapping (plan 01).

---

## 1. What this layer owns, and what it does not

**Owns:** the menu and screen UIs, navigating/selecting within them, capturing the player name, presenting results and high scores, and the application state machine that sequences screens and a game session.

**Does NOT own:** game rules, timing, board drawing, score computation, score storage, or config translation — it *drives* those modules and presents their results. It adds no new game logic; it's glue and presentation.

---

## 2. The core design decisions

### 2.1 One main menu with inline value-cycling

Rather than nested sub-menus, a single vertical menu mixes **value selectors** and **actions**:

```
            T - S N A K E

   Difficulty   ◄  Pro  ►
   Level        ◄  2    ►
   Render       ◄ Unicode ►
   Name           ALICE        (Enter to edit)

        ▶ Play
          High Scores
          Quit
```

Up/Down moves the highlight; Left/Right cycles a selector's value; Enter activates an action (or opens name entry). Changing a value updates `Settings.Preferences` immediately, and toggling **Render** re-themes the menu *live* so you see the look before playing. Preferences are saved (`Settings.Save`) when leaving the menu into a game. Fewer screens, and everything the player tunes is visible at once.

### 2.2 Screens redraw fully; the board diffs

The board renderer (plan 02) diffs because 2–4 cells change per tick. Screens are the opposite: nothing moves between keypresses, then a selection or value changes. So screens just **clear and redraw** on each change — no diffing, no buffers, simpler code, and menus aren't perf-sensitive. They share the same `TerminalSession` and `ITheme` as the board, so the menu, the game, and the results all wear the chosen Unicode/ASCII skin consistently. The centered-box primitive promised in plan 02 lives here and is reused for the pause overlay, results panel, name prompt, and a "terminal too small" message.

### 2.3 A separate, blocking input path for screens

Gameplay input is non-blocking with a turn buffer (`InputService`, plan 03). Screens are the opposite again: they *wait* for a keypress, then act. Both consume the **same** `KeyboardReader` channel (created once, shared), but through different adapters:

- **`ScreenInput`** — blocking reads from the channel mapped to navigation commands: `Up/Down/Left/Right`, `Confirm` (Enter/Space), `Back` (Esc), plus a text-entry mode (printable chars, Backspace, Enter, Esc) for the name.

Only one consumer is active at a time (menu *or* game), and the channel is **flushed on each transition** so a stray keypress (e.g. the Enter that started the game) doesn't leak into the next phase.

### 2.4 The application state machine lives in `AppController`, not `Main`

`Program.Main` is the thin composition root: it constructs the shared, long-lived services and wraps the whole run in the `TerminalSession` so the terminal is restored on *every* exit — normal, quit, or exception.

```
Main:
  using var term  = new TerminalSession();          // restores terminal on any exit
  var reader      = new KeyboardReader(new ConsoleKeySource());
  var settings    = Settings.LoadOrDefault();
  var scores      = HighScoreTable.LoadOrEmpty();
  new AppController(term, reader, settings, scores).Run();
```

`AppController.Run()` is the state machine:

```
loop:
    action = MainMenu.Show(settings, currentTheme)   // mutates prefs; returns Play | HighScores | Quit
    switch action:
        Quit:        return
        HighScores:  HighScoreView.Show(scores, currentTheme)
        Play:
            settings.Save()
            theme   = ThemeFor(prefs.RenderMode)
            seed    = Random.Shared.Next()
            session = settings.BuildSession(prefs.Difficulty, prefs.Level, seed)
            engine  = new GameEngine(session.Engine, new SeededRandom(seed))
            board   = new ConsoleRenderer(theme, term)
            input   = new InputService(reader)
            flush(reader)
            outcome = new GameLoop(engine, board, input, session.Speed, session.PauseAllowed).Run()
            flush(reader)
            if outcome.Status != Quit:
                Results.Show(outcome, prefs, scores, theme)   // §2.5
```

### 2.5 The results flow ties outcome → message → high score

The results screen consumes the loop's `GameOutcome` and the engine's `EndReason`:

- A headline from a pure **`EndReason` → message** map: `HitSelf` → "You bit yourself!", `HitObstacle` → "You hit an obstacle!", `BoardFull` → "You filled the board — you win!".
- The final score, difficulty, and level.
- Then the high-score flow from plan 06: if `scores.Qualifies(outcome.Score)`, pre-fill the name from `Preferences`, let the player confirm or edit it, build the `HighScoreEntry`, `Add` it (capturing the rank), and `Save`; then show the table with the new rank highlighted. If it doesn't qualify, just show the table.
- Footer actions: **Play again** (re-enter the game with the same prefs), **Main menu**, **Quit**.

A game abandoned with Esc (`outcome.Status == Quit`) skips results and the high-score check entirely and returns to the menu — you don't get scored for quitting.

---

## 3. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `ScreenInput` | class | blocking navigation/text-entry reads off the shared key channel |
| `TextUi` (or `ScreenRenderer`) | class | full-screen drawing primitives: title, centered box, menu list with highlight, labeled value, table — themed |
| `MenuModel` | class (pure) | selection index + value cycling + key→action mapping; no Console |
| `MainMenu` | class | draws the menu via `TextUi`, drives it via `ScreenInput` + `MenuModel` |
| `ResultsScreen` | class | the §2.5 outcome + high-score flow |
| `HighScoreView` | class | renders the table (optionally filtered) |
| `AppController` | class | the application state machine |
| `EndMessages` | static (pure) | `EndReason` → headline string |

`Program` provides `ThemeFor(RenderMode)` and the channel `flush` helper.

---

## 4. Testing plan

Screens are interaction-heavy, but the logic that can be wrong is extracted and pure:

- **`MenuModel`** — Up/Down wraps and bounds correctly; Left/Right cycles each selector through its allowed values (and wraps); the right key produces the right action; value changes are reflected in `Preferences`.
- **`EndMessages`** — every `EndReason` (except `None`) maps to a headline.
- **Qualify/rank decision** — the results flow asks `Qualifies` and only then prompts (consistent with plan 06's table behaviour); the abandoned-game path skips scoring.
- **Name entry** — length cap and Backspace behaviour at the model level (final sanitization is Persistence's, plan 06).

What stays manual: the actual look of each screen in both themes, navigation feel, live re-theming, the flush-on-transition behaviour, and that the terminal restores on every exit path.

---

## 5. Definition of done — and the whole game

This plan closes the project. When it's done:

- Launching shows the main menu; you set difficulty, level, render mode, and name, and the choices persist across runs.
- **Play** runs a full session (engine + renderer + input + loop + the chosen config) with correct speed-up, pause (Easy), and quit.
- On death/win, the results screen shows the right message and score, runs the high-score flow (prompt only when qualifying, new rank highlighted), and offers play-again / menu / quit.
- The high-score view is reachable from the menu and reflects saved scores.
- Both render themes skin every screen consistently; the terminal is always restored on exit.
- The pure tests (`MenuModel`, `EndMessages`, results decision, name entry) pass, alongside all earlier modules' tests.

After this, the only remaining work is **non-module**: balance/tuning and latency/flicker measurement on the target setup (high-level roadmap step 7), and writing the **AI dev-log summary** for the exam from the running log.

---

## Decisions to confirm before the session

1. **Single main menu** with inline value-cycling + Play/High Scores/Quit (vs. nested menus). Recommended.
2. **Name is a preference**, editable in the menu (default e.g. "Player"); the results screen pre-fills it and lets you edit before saving when a score qualifies.
3. **Screens redraw fully on change**, share `TerminalSession` + `ITheme` with the board, re-theme live when render mode toggles.
4. **`ScreenInput` blocking path** off the shared `KeyboardReader` channel; navigation = arrows + WASD + Enter/Space (confirm) + Esc (back); **flush the channel on phase transitions**.
5. **`EndReason` → message** map per §2.5; a **quit-out skips results and scoring** and returns to the menu.
6. **State machine in `AppController`**; `Program.Main` is the composition root and wraps the run in `TerminalSession` for guaranteed restore.
7. **Save timing:** `Settings.Save` when leaving the menu into a game; `HighScoreTable.Save` after a qualifying `Add`.
8. **Testability:** pure `MenuModel` + `EndMessages` + results decision; drawing and key-reading verified manually.
9. **High-score view:** single combined list for the POC; per-difficulty filtering is an optional extra, deferred.

Once settled, the session is: build `TextUi`, `ScreenInput`, `MenuModel`, `MainMenu`, `ResultsScreen`, `HighScoreView`, `EndMessages`, and `AppController`; wire `Program.Main`; write the pure tests; then play the finished game start to finish — menu, session, results, high scores — in both themes.
