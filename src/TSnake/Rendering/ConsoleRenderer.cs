using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// The concrete renderer: owns the <see cref="TerminalSession"/>, builds a centered
/// <see cref="BoardGeometry"/> for the current board, composes frames with
/// <see cref="FrameComposer"/>, and issues one batched write per frame.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TickResult"/> carries no score, so <see cref="Apply"/> can't read it directly.
/// We instead track the score locally: <see cref="Begin"/>/<see cref="Redraw"/> seed it from the
/// snapshot, and a tick that contains a <see cref="CellKind.Food"/> change (a newly spawned food
/// = the snake just ate) bumps it by <paramref name="pointsPerFood"/>. That keeps
/// <see cref="IRenderer"/> exactly as the plan specifies while still showing a live score.
/// </para>
/// </remarks>
public sealed class ConsoleRenderer(ITheme theme, string modeLabel, int pointsPerFood) : IRenderer, IDisposable
{
    private readonly TerminalSession _session = new();
    private readonly System.IO.TextWriter _out = Console.Out;

    private FrameComposer? _composer;
    private int _score;

    public void Begin(GameState initial)
    {
        BuildGeometry(initial);
        _score = initial.Score;
        Write(_composer!.ComposeFull(initial, modeLabel));
    }

    public void Apply(TickResult tick)
    {
        if (_composer is null)
        {
            return; // Begin/Redraw must run first.
        }

        bool ate = false;
        foreach (CellChange change in tick.Changes)
        {
            if (change.Kind == CellKind.Food)
            {
                ate = true;
                break;
            }
        }

        if (ate)
        {
            _score += pointsPerFood;
        }

        Write(_composer.ComposeDelta(tick, ate ? _score : null, modeLabel));
    }

    public void Redraw(GameState state)
    {
        BuildGeometry(state);
        _score = state.Score;
        Write(_composer!.ComposeFull(state, modeLabel));
    }

    public void ShowPaused()
    {
        if (_composer is not null)
        {
            Write(_composer.ComposePaused());
        }
    }

    public void Dispose() => _session.Dispose();

    private void BuildGeometry(GameState state)
    {
        var geometry = BoardGeometry.Centered(state.Width, state.Height, ConsoleCols(), ConsoleRows());
        _composer = new FrameComposer(geometry, theme);
    }

    private void Write(string frame)
    {
        _out.Write(frame);
        _out.Flush();
    }

    private static int ConsoleCols()
    {
        try { return Console.WindowWidth; }
        catch (IOException) { return 120; }
    }

    private static int ConsoleRows()
    {
        try { return Console.WindowHeight; }
        catch (IOException) { return 40; }
    }
}
