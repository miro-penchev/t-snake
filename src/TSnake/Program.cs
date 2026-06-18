// Harness for plan 04 (game loop). The crude Thread.Sleep pacing of the input harness is gone:
// a real GameLoop now drives the session with Stopwatch timing, a SpeedProfile speed-up, and
// proper pause that freezes the clock. Real Settings/menus are still a later plan, so the
// difficulty numbers below are hand-picked. Pass "ascii" for the monochrome theme.
//
//   WASD / arrows : steer        Space : pause/resume        Esc : quit
using TSnake.Core;
using TSnake.Input;
using TSnake.Loop;
using TSnake.Rendering;

bool ascii = args.Any(a => a.Equals("ascii", StringComparison.OrdinalIgnoreCase));
ITheme theme = ascii ? new AsciiMonochromeTheme() : new UnicodeColorTheme();

var config = new GameConfig(
    Width: 24,
    Height: 16,
    BasePointsPerFood: 10,
    GrowthPerFood: 1,
    DifficultyMultiplier: 1,
    LevelMultiplier: 1,
    ObstaclesEnabled: true,
    ObstacleSpawnRate: 6,
    ObstacleLifetimeTicks: 40,
    MaxObstacles: 6,
    InitialSnakeLength: 4,
    StartPosition: new Point(6, 8),
    Seed: Environment.TickCount);

int pointsPerFood = config.BasePointsPerFood * config.DifficultyMultiplier * config.LevelMultiplier;

// Make sure the whole scene fits before we take over the screen.
if (!TerminalSession.EnsureMinimumSize(BoardGeometry.TotalCols(config.Width), BoardGeometry.TotalRows(config.Height)))
{
    return;
}

var engine = new GameEngine(config, new SeededRandom(config.Seed));
string modeLabel = ascii ? "ASCII (playable)" : "Unicode (playable)";

// Entry-level pace: a comfortable 150 ms start that shrinks gently with each food, floored so it
// stays playable. These are placeholder numbers — real per-difficulty/level values (and the
// "mode" the player picks) arrive with the Settings/menus plan.
var speed = new SpeedProfile(BaseIntervalMs: 150, MinIntervalMs: 60, ShrinkRate: 0.02, SpeedDriver.FoodEaten);

// Pause is Easy-only by design; the harness enables it so the loop's pause path is exercisable.
const bool pauseAllowed = true;

using var ui = new ConsoleRenderer(theme, modeLabel, pointsPerFood);
using var input = new InputService(new ConsoleKeySource());

var loop = new GameLoop(engine, ui, input, speed, pauseAllowed);
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
