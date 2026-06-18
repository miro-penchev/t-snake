using TSnake.Core;
using TSnake.Input;
using TSnake.Loop;
using TSnake.Persistence;
using TSnake.Rendering;
using TSnake.Settings;

namespace TSnake.Screens;

/// <summary>
/// The application state machine (plan 07 §2.4): it sequences menu → game → results → menu and owns
/// the wiring between the six modules. It is handed the long-lived, shared services by the
/// composition root (<c>Program.Main</c>) and never disposes them — the single
/// <see cref="TerminalSession"/> and <see cref="KeyboardReader"/> live for the whole run so the
/// terminal restores exactly once and one keyboard channel feeds both the menu and the game.
/// </summary>
public sealed class AppController
{
    private readonly TerminalSession _term;
    private readonly KeyboardReader _reader;
    private readonly GameSettings _settings;
    private readonly HighScoreTable _scores;

    private readonly TextUi _ui;
    private readonly ScreenInput _screenInput;
    private readonly MainMenu _menu;
    private readonly HighScoreView _highScores;
    private readonly ResultsScreen _results;

    public AppController(TerminalSession term, KeyboardReader reader, GameSettings settings, HighScoreTable scores)
    {
        _term = term;
        _reader = reader;
        _settings = settings;
        _scores = scores;

        _ui = new TextUi(term);
        _screenInput = new ScreenInput(reader);
        var namePrompt = new NamePrompt(_ui, _screenInput);
        _menu = new MainMenu(_ui, _screenInput, namePrompt, settings);
        _highScores = new HighScoreView(_ui, _screenInput);
        _results = new ResultsScreen(_ui, _screenInput, namePrompt);
    }

    /// <summary>Runs the whole game — menu, sessions, results — until the player quits.</summary>
    public void Run()
    {
        while (true)
        {
            _reader.Flush(); // drop any key left over from the previous phase before the menu reads

            switch (_menu.Show())
            {
                case MenuAction.Quit:
                    return;
                case MenuAction.HighScores:
                    _highScores.Show(_scores, CurrentTheme);
                    break;
                case MenuAction.Play:
                    if (!PlaySessions())
                    {
                        return; // the player chose Quit from the results screen
                    }

                    break;
            }
        }
    }

    private ITheme CurrentTheme => Themes.For(_settings.Preferences.RenderMode);

    /// <summary>
    /// Runs game → results, looping while the player picks Play again. Returns false only when they
    /// choose Quit (which ends the app), true to return to the menu.
    /// </summary>
    private bool PlaySessions()
    {
        _settings.Save(); // persist the menu's choices on the way into a game (plan §2.4)

        while (true)
        {
            GameOutcome outcome = PlayOne();
            if (outcome.Quit)
            {
                return true; // abandoned with Esc: no results, no scoring — back to the menu (§2.5)
            }

            switch (_results.Show(outcome, _settings.Preferences, _scores, CurrentTheme))
            {
                case ResultsChoice.PlayAgain:
                    continue;
                case ResultsChoice.Quit:
                    return false;
                default:
                    return true; // Main menu
            }
        }
    }

    private GameOutcome PlayOne()
    {
        Preferences prefs = _settings.Preferences;
        ITheme theme = Themes.For(prefs.RenderMode);
        int seed = Random.Shared.Next();
        SessionConfig session = _settings.BuildSession(prefs.Difficulty, prefs.Level, seed);
        GameConfig config = session.Engine;

        if (!EnsureSize(BoardGeometry.TotalCols(config.Width), BoardGeometry.TotalRows(config.Height), theme))
        {
            // Player gave up enlarging the terminal: treat as an abandoned session.
            return new GameOutcome(0, EndReason.None, GameStatus.GameOver, Quit: true, 0, TimeSpan.Zero);
        }

        string modeLabel = $"{prefs.Difficulty} · L{prefs.Level} · {prefs.RenderMode}";
        int pointsPerFood = config.BasePointsPerFood * config.DifficultyMultiplier * config.LevelMultiplier;

        var engine = new GameEngine(config, new SeededRandom(seed));
        var board = new ConsoleRenderer(theme, _term, modeLabel, pointsPerFood);
        var input = new InputService(_reader);

        _reader.Flush(); // the Enter/Space that launched the game must not leak into the first tick
        GameOutcome outcome = new GameLoop(engine, board, input, session.Speed, session.PauseAllowed).Run();
        _reader.Flush(); // the Esc that ended a quit, or any post-death key, must not leak onward
        return outcome;
    }

    /// <summary>
    /// Blocks (via the shared channel, never a direct Console read) until the terminal is at least
    /// <paramref name="cols"/> × <paramref name="rows"/>, showing a themed notice. Returns false if
    /// the player pressed Esc to give up. Re-checks the size after each keypress.
    /// </summary>
    private bool EnsureSize(int cols, int rows, ITheme theme)
    {
        while (_term.Width < cols || _term.Height < rows)
        {
            _ui.RenderBox(
                [
                    new TextUi.Line("Terminal too small"),
                    new TextUi.Line(string.Empty),
                    new TextUi.Line($"Need {cols} x {rows} — have {_term.Width} x {_term.Height}"),
                    new TextUi.Line("Enlarge, then press any key (Esc to cancel)"),
                ],
                theme);

            if (_screenInput.ReadCommand() == NavCommand.Back)
            {
                return false;
            }
        }

        return true;
    }
}
