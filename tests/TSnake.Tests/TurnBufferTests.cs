using TSnake.Core;
using TSnake.Input;

namespace TSnake.Tests;

public class TurnBufferTests
{
    private static List<Direction> DrainAll(TurnBuffer buffer)
    {
        var dequeued = new List<Direction>();
        while (buffer.TryDequeue(out Direction d))
        {
            dequeued.Add(d);
        }

        return dequeued;
    }

    [Fact]
    public void EmptyBufferDequeuesNothing()
    {
        var buffer = new TurnBuffer();
        Assert.False(buffer.TryDequeue(out _));
    }

    [Fact]
    public void QuickCornerSurvives()
    {
        // §2.2 worked example: heading Right, player taps Down then Right between two ticks.
        var buffer = new TurnBuffer();
        buffer.Enqueue(Direction.Down);
        buffer.Enqueue(Direction.Right);

        Assert.Equal([Direction.Down, Direction.Right], DrainAll(buffer));
    }

    [Fact]
    public void ThirdTurnIsDroppedWhileFull()
    {
        var buffer = new TurnBuffer();
        buffer.Enqueue(Direction.Down);
        buffer.Enqueue(Direction.Right);
        buffer.Enqueue(Direction.Up); // buffer full (cap 2) -> incoming dropped, even though it's legal

        Assert.Equal([Direction.Down, Direction.Right], DrainAll(buffer));
    }

    [Fact]
    public void InBufferReversalIsRejected()
    {
        var buffer = new TurnBuffer();
        buffer.Enqueue(Direction.Up);
        buffer.Enqueue(Direction.Down); // 180° opposite of the last buffered turn -> dropped

        Assert.Equal([Direction.Up], DrainAll(buffer));
    }

    [Fact]
    public void DuplicateTurnIsCollapsed()
    {
        var buffer = new TurnBuffer();
        buffer.Enqueue(Direction.Right);
        buffer.Enqueue(Direction.Right); // equal to last buffered (held key / OS repeat) -> dropped

        Assert.Equal([Direction.Right], DrainAll(buffer));
    }

    [Fact]
    public void EmptyBufferEnqueuesUnchecked_NoHeadingGuardHere()
    {
        // The first turn against the live heading is the engine's job, not the buffer's: with an empty
        // buffer there is no previous buffered turn, so Down enqueues, drains, then Up enqueues freely.
        var buffer = new TurnBuffer();

        buffer.Enqueue(Direction.Down);
        Assert.True(buffer.TryDequeue(out Direction first));
        Assert.Equal(Direction.Down, first);

        buffer.Enqueue(Direction.Up); // would reverse a *buffered* Down, but the buffer is empty now
        Assert.Equal([Direction.Up], DrainAll(buffer));
    }

    [Fact]
    public void DrainsWithinTwoTicksAfterAMash()
    {
        // Runaway turning is impossible: many presses, at most two survive.
        var buffer = new TurnBuffer();
        buffer.Enqueue(Direction.Down);
        buffer.Enqueue(Direction.Right);
        buffer.Enqueue(Direction.Down);
        buffer.Enqueue(Direction.Left);
        buffer.Enqueue(Direction.Up);

        Assert.Equal(2, DrainAll(buffer).Count);
    }
}
