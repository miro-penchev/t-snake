using TSnake.Core;

namespace TSnake.Input;

/// <summary>
/// The buffering policy deferred from plan 01: a FIFO turn buffer capped at depth 2 (plan §2.2).
/// The loop dequeues one turn per tick; incoming turns enqueue up to the cap. Cap = 2 lets a single
/// quick corner (two 90° turns) survive while making runaway turning impossible — the buffer drains
/// in at most two ticks once you stop pressing.
/// </summary>
/// <remarks>
/// This is pure, single-threaded state: <see cref="InputService"/> only touches it from the loop
/// thread, so no locking is needed. Two rules apply against the <em>last buffered</em> turn (never the
/// engine's live heading — that guard stays in the engine, plan §2.2):
/// <list type="bullet">
/// <item>a turn equal to the last buffered turn is dropped (collapses OS key-repeat);</item>
/// <item>a turn that is the 180° opposite of the last buffered turn is dropped (keeps a queued chain
/// coherent — you can't buffer Up→Down).</item>
/// </list>
/// When the buffer is full the <em>incoming</em> turn is dropped, preserving a deliberate quick corner
/// over a later mash. When the buffer is empty no rule applies — the turn enqueues unchecked and the
/// engine rejects it next tick if it reverses the live heading.
/// </remarks>
public sealed class TurnBuffer
{
    private const int Capacity = 2;

    private readonly Queue<Direction> _turns = new();
    private Direction _tail; // the last enqueued turn; meaningful only while _turns is non-empty

    public int Count => _turns.Count;

    public void Enqueue(Direction turn)
    {
        if (_turns.Count >= Capacity)
        {
            return; // full: keep the two committed turns, drop the incoming one
        }

        if (_turns.Count > 0 && (turn == _tail || turn == _tail.Opposite()))
        {
            return; // collapse a no-op repeat, or reject an in-buffer reversal
        }

        _turns.Enqueue(turn);
        _tail = turn;
    }

    public bool TryDequeue(out Direction turn) => _turns.TryDequeue(out turn);
}
