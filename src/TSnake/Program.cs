// Throwaway harness for plan 02 (rendering). The real game loop and keyboard input are later
// plans; here the snake stays FROZEN until you press a key, so we can watch the renderer paint
// every cell change by hand. Pass "ascii" to use the monochrome theme.
//
//   Arrow keys : turn and step one tick     Space / Enter : step forward (no turn)
//   p          : pause overlay (any key resumes)            Esc / Q : quit
using TSnake.Core;
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
string modeLabel = ascii ? "ASCII (demo)" : "Unicode (demo)";

using var ui = new ConsoleRenderer(theme, modeLabel, pointsPerFood);
ui.Begin(engine.State);

Direction heading = Direction.Right;
bool quit = false;

while (!quit && engine.Status == GameStatus.Running)
{
    ConsoleKey key = Console.ReadKey(intercept: true).Key;

    switch (key)
    {
        case ConsoleKey.Escape:
        case ConsoleKey.Q:
            quit = true;
            continue;

        case ConsoleKey.UpArrow: heading = Direction.Up; break;
        case ConsoleKey.DownArrow: heading = Direction.Down; break;
        case ConsoleKey.LeftArrow: heading = Direction.Left; break;
        case ConsoleKey.RightArrow: heading = Direction.Right; break;

        case ConsoleKey.P:
            ui.ShowPaused();
            Console.ReadKey(intercept: true);
            ui.Redraw(engine.State); // restore the cells the overlay covered
            continue;

        case ConsoleKey.Spacebar:
        case ConsoleKey.Enter:
            break; // step forward in the current heading

        default:
            continue; // ignore other keys; stay frozen
    }

    engine.SetDirection(heading);
    ui.Apply(engine.Tick());
}

// Hold the final frame (with the collision marker, if any) until a key is pressed, so the
// game-over state is visible before the terminal is restored on dispose.
if (!quit)
{
    Console.ReadKey(intercept: true);
}
