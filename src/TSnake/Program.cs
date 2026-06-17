// Throwaway harness for plan 03 (input). Combined with the plan-02 renderer, this is the first
// PLAYABLE build: a real InputService steers a live snake, with crude fixed Thread.Sleep pacing
// standing in for the proper fixed-timestep loop (still a later plan). Pass "ascii" for the
// monochrome theme.
//
//   WASD / arrows : steer        Space : pause/resume        Esc : quit
using TSnake.Core;
using TSnake.Input;
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

using var ui = new ConsoleRenderer(theme, modeLabel, pointsPerFood);
using var input = new InputService(new ConsoleKeySource());

ui.Begin(engine.State);

bool paused = false;
while (engine.Status == GameStatus.Running)
{
    input.Poll();

    if (input.ConsumeQuit())
    {
        break;
    }

    if (input.ConsumePause())
    {
        paused = !paused;
        if (paused)
        {
            ui.ShowPaused();
        }
        else
        {
            ui.Redraw(engine.State); // restore the cells the overlay covered
        }
    }

    if (paused)
    {
        Thread.Sleep(120); // stop ticking, but keep polling so we can see resume / quit
        continue;
    }

    if (input.TryNextTurn(out Direction dir))
    {
        engine.SetDirection(dir);
    }

    ui.Apply(engine.Tick());
    Thread.Sleep(120); // crude fixed pacing; speed-up is the loop's job (later)
}

// Hold the final frame (with the collision marker, if any) until the player presses Esc, so the
// game-over state is visible before the terminal is restored on dispose.
if (engine.Status != GameStatus.Running)
{
    while (!input.ConsumeQuit())
    {
        input.Poll();
        Thread.Sleep(50);
    }
}
