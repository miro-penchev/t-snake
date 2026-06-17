using TSnake.Core;
using TSnake.Input;

namespace TSnake.Tests;

public class InputServiceTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);

    private static ConsoleKeyInfo Key(ConsoleKey key) =>
        new('\0', key, shift: false, alt: false, control: false);

    /// <summary>Builds a service over a scripted stream and polls once after every key is delivered.</summary>
    private static (InputService Service, ScriptedKeySource Source) PolledService(params ConsoleKey[] keys)
    {
        var source = new ScriptedKeySource([.. keys.Select(Key)]);
        var service = new InputService(source);
        Assert.True(source.Drained.Wait(DeliveryTimeout), "scripted keys were not delivered in time");
        service.Poll();
        return (service, source);
    }

    private static List<Direction> DrainTurns(InputService service)
    {
        var turns = new List<Direction>();
        while (service.TryNextTurn(out Direction d))
        {
            turns.Add(d);
        }

        return turns;
    }

    [Fact]
    public void MapsAndBuffersAQuickCorner()
    {
        var (service, source) = PolledService(ConsoleKey.DownArrow, ConsoleKey.RightArrow);
        using var srcScope = source;
        using var svcScope = service;

        Assert.Equal([Direction.Down, Direction.Right], DrainTurns(service));
    }

    [Fact]
    public void AppliesTheDepthCapAcrossThePipeline()
    {
        var (service, source) = PolledService(ConsoleKey.S, ConsoleKey.D, ConsoleKey.W);
        using var srcScope = source;
        using var svcScope = service;

        Assert.Equal([Direction.Down, Direction.Right], DrainTurns(service));
    }

    [Fact]
    public void IgnoresUnmappedKeys()
    {
        var (service, source) = PolledService(ConsoleKey.Enter, ConsoleKey.Q, ConsoleKey.A);
        using var srcScope = source;
        using var svcScope = service;

        Assert.Equal([Direction.Left], DrainTurns(service));
    }

    [Fact]
    public void ConsumePauseIsTrueOnceThenResets()
    {
        var (service, source) = PolledService(ConsoleKey.Spacebar);
        using var srcScope = source;
        using var svcScope = service;

        Assert.True(service.ConsumePause());
        Assert.False(service.ConsumePause());
    }

    [Fact]
    public void ConsumeQuitLatches()
    {
        var (service, source) = PolledService(ConsoleKey.Escape);
        using var srcScope = source;
        using var svcScope = service;

        Assert.True(service.ConsumeQuit());
        Assert.True(service.ConsumeQuit()); // quit is a one-way trip
    }

    [Fact]
    public void NoInputProducesNoTurnsOrFlags()
    {
        var (service, source) = PolledService();
        using var srcScope = source;
        using var svcScope = service;

        Assert.False(service.TryNextTurn(out _));
        Assert.False(service.ConsumePause());
        Assert.False(service.ConsumeQuit());
    }
}
