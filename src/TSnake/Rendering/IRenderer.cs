using TSnake.Core;

namespace TSnake.Rendering;

/// <summary>The public surface the game loop calls. Plan §6.</summary>
public interface IRenderer
{
    /// <summary>One full draw of the initial scene: frame + HUD + the whole board.</summary>
    void Begin(GameState initial);

    /// <summary>Per-tick fast path: paint the changed cells, the HUD delta, and any death marker.</summary>
    void Apply(TickResult tick);

    /// <summary>Full repaint from a snapshot: resume-from-pause, resize, or corruption recovery.</summary>
    void Redraw(GameState state);

    /// <summary>Draw a centered pause overlay (resuming calls <see cref="Redraw"/> to restore it).</summary>
    void ShowPaused();
}
