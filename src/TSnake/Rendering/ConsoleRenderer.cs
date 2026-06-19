using System.Threading;
using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>
/// The concrete renderer: draws into the shared <see cref="TerminalSession"/> owned by the
/// composition root, builds a centered <see cref="BoardGeometry"/> for the current board, composes
/// frames with <see cref="FrameComposer"/>, and issues one batched write per frame.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="TerminalSession"/> is injected and <i>not</i> owned: a single session wraps the
/// whole application (menu, game, results) so the terminal is restored exactly once on exit
/// (plan 07 §2.4). A fresh renderer is built per game session, but the session outlives it.
/// </para>
/// <para>
/// <see cref="TickResult"/> carries no score, so <see cref="Apply"/> can't read it directly.
/// We instead track the score locally: <see cref="Begin"/>/<see cref="Redraw"/> seed it from the
/// snapshot, and a tick that contains a <see cref="CellKind.Food"/> change (a newly spawned food
/// = the snake just ate) bumps it by <paramref name="pointsPerFood"/>. That keeps
/// <see cref="IRenderer"/> exactly as the plan specifies while still showing a live score.
/// </para>
/// </remarks>
public sealed class ConsoleRenderer(ITheme theme, TerminalSession session, string modeLabel, int pointsPerFood) : IRenderer
{
    // Death-blast pacing: one expanding ring every FrameMs, plus a few cooling frames past the edge
    // so even the far corners burn down to embers, then a brief hold on the scorched board.
    private const int BlastFrameMs = 38;
    private const int BlastCooldownRings = 4;
    private const int BlastHoldMs = 400;

    private readonly System.IO.TextWriter _out = Console.Out;

    private FrameComposer? _composer;
    private BoardGeometry _geometry;
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

    public void PlayDeathEffect(Point origin)
    {
        if (_composer is null)
        {
            return; // Begin/Redraw must run first to build the geometry.
        }

        int lastRing = _geometry.MaxRadiusFrom(origin) + BlastCooldownRings;
        for (int radius = 0; radius <= lastRing; radius++)
        {
            Write(_composer.ComposeExplosionFrame(origin, radius));
            Thread.Sleep(BlastFrameMs);
        }

        Thread.Sleep(BlastHoldMs); // let the scorched board linger a beat before the results screen
    }

    private void BuildGeometry(GameState state)
    {
        _geometry = BoardGeometry.Centered(state.Width, state.Height, session.Width, session.Height);
        _composer = new FrameComposer(_geometry, theme);
    }

    private void Write(string frame)
    {
        _out.Write(frame);
        _out.Flush();
    }
}
