# Detailed Plan 02 — Rendering (Output)

The second per-module plan. Rendering is the first module that *consumes* the engine, so this is where the `GameState` / `TickResult` contract from plan 01 gets used in anger. By the end of this plan we can actually **watch** the engine run on screen — the first real wire-up.

It lives in the console app (`src/TSnake/Rendering/`), references `TSnake.Core` to read `GameState`, `TickResult`, `CellKind`, and `EndReason`, and touches the `Console` / terminal freely (unlike the engine).

---

## 1. What the renderer owns, and what it does not

**Owns:** turning engine state into terminal output. The board frame, the snake/food/obstacle glyphs, the HUD (score, level, mode), the death marker, the pause overlay, and all the terminal setup/teardown (VT mode, encoding, alternate screen, cursor visibility).

**Does NOT own:** game rules (it only *reads* engine state), reading the keyboard (Input plan), timing/when to draw (the loop decides), and menus/results *screens* (a later screens plan — though it will reuse this module's terminal session and theme).

---

## 2. The core design decisions

### 2.1 Paint only what changed; full redraw on demand

The engine gives the renderer **two** things, and each drives a different draw path:

- **`GameState` (full snapshot)** — the complete scene (board size, all snake cells, food, all obstacles, score, status). Everything needed to paint the screen from scratch, with nothing remembered by the renderer.
- **`TickResult.Changes` (per-tick deltas)** — the small list of cells that changed this tick.

The high-level plan floated a back-buffer/front-buffer diff. We don't need it, because we have both of the above directly from the engine. So:

- **Fast path (every tick):** paint exactly the cells in `TickResult.Changes`, plus a HUD update if the score changed, plus the death marker if `EndReason != None`. No persistent buffer in the renderer.
- **Full redraw (`Redraw(GameState)`):** rebuild the entire screen from a `GameState` snapshot. This is `O(width × height)` and only fires on occasional "the screen was disturbed" events, never per tick:
  - **Start of a game / new game after game-over** (this is what `Begin` does).
  - **Resume from pause** when a banner covered the board — the covered cells are repainted cleanly, so the renderer never has to remember what was under the overlay.
  - **Terminal resize** — geometry moved, everything repositions.
  - **Return from a menu/results screen** (later plan) and general corruption recovery.

Steady state therefore stays at the 2–4 cells that actually moved (the anti-flicker strategy), while the occasional full repaint covers every disturbed-screen case.

### 2.2 One batched write per frame

The rule we settled earlier: never `SetCursorPosition` + `Write` per cell. Build the whole frame — every cursor-move escape, color code, and glyph — into a single string, then issue **one** write to stdout. For Snake a tick is a few cells, so each frame is a tiny string.

### 2.3 Two terminal columns per board cell

A terminal character cell is about half as wide as it is tall, so drawing one char per board cell makes the board look vertically stretched. Rendering each board cell as **two columns wide** gives a roughly square cell and much better-looking motion. The cell width is fixed and shared across both themes so the coordinate math stays uniform: `col = originCol + x * 2`, `row = originRow + y`.

### 2.4 Themes are pure lookup tables; the machinery is shared

The two render modes differ *only* in the glyph/color table. The cursor-positioning, batching, and redraw logic are identical. So a theme is just: given a `CellKind` (and whether this is the collision cell), return a two-column string plus optional foreground/background color. Swapping themes never touches the draw code.

### 2.5 Separate "compose the string" from "write the string"

Rendering is I/O, but most of it can still be tested. Split it:

- **`FrameComposer` (pure, testable):** given geometry + theme + either a `GameState` (full frame) or a list of `CellChange` (delta), returns the exact ANSI string. No `Console` calls. Tests can assert the produced escape sequence for a known input.
- **Console writer (thin):** takes the composed string and writes it once. The only part that actually touches stdout.

---

## 3. Types this module defines

| Type | Kind | Purpose |
|------|------|---------|
| `TerminalSession` | `IDisposable` | sets up the terminal on construction (UTF-8, VT mode, alternate screen, hidden cursor) and **restores everything on dispose** — including on crash/Ctrl-C |
| `ITheme` | interface | `CellKind` (+ is-collision flag) → two-column glyph string + colors; also exposes `CellWidth` |
| `UnicodeColorTheme` | class | the smooth, colorful look (block/geometric glyphs, truecolor) |
| `AsciiMonochromeTheme` | class | the retro look (`@` head, no color) |
| `BoardGeometry` | readonly struct/record | pure mapping: board `(x, y)` → terminal `(row, col)`, plus frame and HUD placement. Unit-testable |
| `FrameComposer` | class (pure) | builds the ANSI string for a full frame or a set of changes |
| `IRenderer` / `ConsoleRenderer` | interface + class | the public surface the loop calls; owns the session, composer, and writer |

---

## 4. Terminal setup & teardown (`TerminalSession`)

This is the home of the high-level plan's "terminal state restoration" risk. All of it is explicit, and undone in reverse on dispose.

On construction:
- `Console.OutputEncoding = Encoding.UTF8` — required for block/box glyphs.
- **Enable virtual terminal processing.** On Windows this is a P/Invoke: `GetStdHandle(STD_OUTPUT_HANDLE)` → `GetConsoleMode` → `SetConsoleMode` with `ENABLE_VIRTUAL_TERMINAL_PROCESSING (0x0004)`. Windows Terminal supports ANSI, but the process console mode still needs the flag set to be safe.
- Enter the **alternate screen buffer** (`\x1b[?1049h`) so the game gets its own screen and the user's shell/scrollback is restored on exit.
- **Hide the cursor** (`\x1b[?25l`).
- Clear the screen.

On dispose (and via `Console.CancelKeyPress` + a `try/finally` around the loop so a crash can't leave a wrecked terminal):
- Show the cursor (`\x1b[?25h`).
- Leave the alternate screen buffer (`\x1b[?1049l`).
- Restore the original console mode.

A minimum-size check belongs here too: if the window is smaller than frame + board + HUD, show a "please enlarge the terminal" message and wait rather than drawing a broken board.

---

## 5. Glyphs & color (the two themes)

Default tables — these are aesthetic, so treat them as a starting point to confirm. Two columns per cell throughout.

| `CellKind` | Unicode + color | ASCII monochrome |
|------------|-----------------|------------------|
| `Empty` | `··` spaces, board bg | two spaces |
| `SnakeHead` | `██` bright green | `@ ` |
| `SnakeBody` | `██` green | `o ` |
| `Food` | `●●` red / apple-red | `* ` |
| `Obstacle` | `▓▓` gray | `# ` |
| collision marker | `✖✖` bright red | `X ` |

Notes:
- **Single-width glyphs only.** This is the double-width trap from earlier: many emoji and CJK characters occupy two cells and silently break alignment. Block elements (`█ ▓ ▒`), geometric shapes (`● ◆`), and box-drawing are all safe single-width choices, which is why they're preferred over emoji.
- Unicode mode uses **24-bit truecolor** SGR codes (`\x1b[38;2;r;g;bm` / `\x1b[48;2;r;g;bm`); Windows Terminal supports it. ASCII mode emits no color at all — just glyphs on the default palette.
- The board frame uses box-drawing characters in Unicode mode (`╔ ═ ╗ ║ ╝ ╚`) and `+ - |` in ASCII mode.

---

## 6. Draw operations (`IRenderer`)

```csharp
public interface IRenderer
{
    void Begin(GameState initial);   // one full draw: frame + HUD + whole board
    void Apply(TickResult tick);     // paint changed cells (+ HUD delta, + death marker)
    void Redraw(GameState state);    // full repaint: resume-from-pause, resize, recovery
    void ShowPaused();               // centered overlay (Easy mode only)
}
```

**`Begin`** composes the frame border, the HUD line, and every board cell from the initial `GameState`, in one batched write.

**`Apply`** is the per-tick fast path:
1. For each `CellChange`, map its `Point` to `(row, col)` and append the theme glyph at that position.
2. If the score changed, append a HUD update (fixed position — cheap).
3. If `tick.EndReason != None`, append the collision marker at `tick.CollisionCell`. (Any flash/animation *timing* is the loop's call; the renderer just provides the marked frame.)
4. One write.

**`Redraw`** is `Begin`'s logic against an arbitrary `GameState` — used whenever the screen may have been disturbed.

**`ShowPaused`** draws a centered box over the board; resuming calls `Redraw(state)` to cleanly restore the covered cells (no need to remember what was underneath).

Menus, the results/game-over screen, and high-score display are **out of scope** here — they get their own screens plan and will reuse `TerminalSession`, `ITheme`, and a shared "centered box" primitive.

---

## 7. How it wires to the engine (the milestone for this plan)

The real game loop is plan 05, but this plan ends with a **throwaway harness** so we can see the engine running:

```
var engine   = new GameEngine(config, new SeededRandom(seed));
using var ui = new ConsoleRenderer(theme);

ui.Begin(engine.State);
foreach (var dir in scriptedOrAutoDirections)
{
    engine.SetDirection(dir);
    var result = engine.Tick();
    ui.Apply(result);
    Thread.Sleep(120);                 // crude pacing — NOT the real loop
    if (engine.Status != GameStatus.Running) break;
}
```

No real keyboard input yet (that's a later plan); a scripted or auto-steering sequence is enough to watch the snake move, eat, grow, and crash on screen. That visual confirmation is the definition of done.

---

## 8. Testing plan

Rendering touches the console, but the pure parts carry most of the logic and *are* testable:

- **`BoardGeometry` mapping** — `(x, y)` → `(row, col)` is correct for corners and interior, accounting for frame origin and 2-wide cells.
- **Theme tables** — every `CellKind` maps to a non-empty two-column string; ASCII theme emits no color codes; Unicode theme emits well-formed SGR.
- **`FrameComposer`** — for a known `GameState`, the composed full-frame string contains the expected cursor moves and glyphs; for a known `CellChange` list, the delta string positions and paints exactly those cells and nothing else.

What stays manual: the actual *look* and smoothness, verified by eye on Windows Terminal (and the basis for any flicker/latency measurement noted in the high-level plan's risks).

---

## 9. Definition of done

- `TerminalSession` sets up and **reliably restores** the terminal, including on Ctrl-C and on exception.
- Both themes render the full board and incremental changes correctly.
- The throwaway harness runs a scripted/auto sequence and you can watch the snake move, eat, grow, and crash, with the collision cell marked — in both Unicode and ASCII modes.
- The pure-part tests (geometry, themes, composer) pass.
- No flicker on a normal-size board at a hand-driven tick rate (formal speed measurement is deferred to the tuning step).

---

## Decisions to confirm before the session

1. **2 columns per board cell**, shared across themes (vs. 1-wide, vs. per-theme width). Recommended for square aspect.
2. **24-bit truecolor** in Unicode mode (Windows Terminal supports it); ASCII mode is colorless.
3. **No renderer back-buffer** — paint `TickResult.Changes`, full `Redraw` from `GameState` when needed. (Refines the high-level plan's back/front-buffer note.)
4. **Compose/write split** (`FrameComposer` pure + thin writer) for testability.
5. **Scope:** in-game board + HUD + death marker + pause overlay + redraw primitive. Menus/results/high-score screens deferred to a later screens plan.
6. **Glyph tables** in §5 — confirm the aesthetic, especially the ASCII set (`@` head is per the spec; the rest are open).
7. **Too-small terminal at startup** → show an "enlarge" message and wait. Live-resize handling (poll size, `Redraw` on change) is optional hardening — in or out for now?

Once these are settled, the session is: build `TerminalSession`, the two themes, `BoardGeometry`, `FrameComposer`, and `ConsoleRenderer`; write the pure-part tests; then wire the throwaway harness and watch the engine run.
