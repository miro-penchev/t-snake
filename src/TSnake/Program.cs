// Composition for plan 05 (settings). The hand-picked difficulty numbers are gone: a real
// Settings layer now loads the player's preferences (writing a default settings.json on first
// run), translates the chosen difficulty/level into the engine + loop configs, and a fresh
// random seed makes each run differ. The menu that lets the player change those preferences
// interactively is a later plan; for now they come from the file.
//
//   WASD / arrows : steer        Space : pause/resume (Easy only)        Esc : quit
using TSnake.Core;
using TSnake.Input;
using TSnake.Loop;
using TSnake.Rendering;
using TSnake.Settings;

GameSettings settings = GameSettings.LoadOrDefault();
Preferences prefs = settings.Preferences;

// Per-run seed (plan §2.4): each game differs. Inject a fixed value here for a reproducible run.
int seed = Random.Shared.Next();
SessionConfig session = settings.BuildSession(prefs.Difficulty, prefs.Level, seed);
GameConfig config = session.Engine;

// Make sure the whole scene fits before we take over the screen.
if (!TerminalSession.EnsureMinimumSize(BoardGeometry.TotalCols(config.Width), BoardGeometry.TotalRows(config.Height)))
{
    return;
}

ITheme theme = ThemeFor(prefs.RenderMode);
string modeLabel = $"{prefs.Difficulty} · L{prefs.Level} · {prefs.RenderMode}";
int pointsPerFood = config.BasePointsPerFood * config.DifficultyMultiplier * config.LevelMultiplier;

var engine = new GameEngine(config, new SeededRandom(seed));

using var ui = new ConsoleRenderer(theme, modeLabel, pointsPerFood);
using var input = new InputService(new ConsoleKeySource());

var loop = new GameLoop(engine, ui, input, session.Speed, session.PauseAllowed);
GameOutcome outcome = loop.Run();

// On death / board-full, hold the final frame (with the collision marker, if any) until the player
// presses Esc, so the end state is visible before the terminal is restored on dispose. A quit
// already pressed Esc, so there is nothing to wait for.
if (!outcome.Quit)
{
    while (!input.ConsumeQuit())
    {
        input.Poll();
        Thread.Sleep(50);
    }
}

// Settings holds a render-mode *preference*; composition is where it becomes a concrete theme
// (plan decision #7 — Settings never references Rendering).
static ITheme ThemeFor(RenderMode mode) => mode switch
{
    RenderMode.AsciiMonochrome => new AsciiMonochromeTheme(),
    _ => new UnicodeColorTheme(),
};
