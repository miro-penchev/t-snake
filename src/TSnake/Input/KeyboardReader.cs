using System.Threading.Channels;

namespace TSnake.Input;

/// <summary>
/// Owns the long-lived background thread that turns the blocking keyboard into a non-blocking stream
/// (plan §2.1). The thread sits in <see cref="IKeySource.ReadKey"/> and pushes each raw keystroke into
/// an unbounded channel; the loop drains the channel once per tick. The channel is pure transport — all
/// mapping and buffering happens consumer-side on a single thread, so gameplay state needs no locks.
/// </summary>
/// <remarks>
/// The thread is <see cref="Thread.IsBackground"/> and runs for the whole app lifetime. There is no
/// portable way to cancel a <see cref="Console.ReadKey(bool)"/> that is already blocked, so <see cref="Dispose"/>
/// does not join it; the process tears it down on exit (plan §2.1). The channel cap is deliberately
/// unbounded — the depth-2 gameplay cap lives in <see cref="TurnBuffer"/>, not here.
/// </remarks>
public sealed class KeyboardReader : IDisposable
{
    private readonly IKeySource _source;
    private readonly Channel<ConsoleKeyInfo> _channel;
    private readonly Thread _thread;
    private volatile bool _disposed;

    public KeyboardReader(IKeySource source)
    {
        _source = source;
        _channel = Channel.CreateUnbounded<ConsoleKeyInfo>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        _thread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "TSnake.KeyboardReader",
        };
        _thread.Start();
    }

    /// <summary>The consumer side, drained by <see cref="InputService.Poll"/> once per tick.</summary>
    public ChannelReader<ConsoleKeyInfo> Reader => _channel.Reader;

    /// <summary>
    /// Discards every keystroke currently queued. Screens call this on each phase transition so a
    /// stray key (e.g. the Enter that launched the game) can't leak into the next consumer (plan
    /// 07 §2.3). Safe to call on the single consumer thread between phases.
    /// </summary>
    public void Flush()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Completing the channel lets a (rare) non-blocking source's loop exit; a real blocked
        // Console.ReadKey is left to die with the process.
        _channel.Writer.TryComplete();
    }

    private void ReadLoop()
    {
        while (!_disposed)
        {
            ConsoleKeyInfo key;
            try
            {
                key = _source.ReadKey();
            }
            catch (InvalidOperationException)
            {
                break; // console input redirected/closed — nothing more to read
            }

            if (!_channel.Writer.TryWrite(key))
            {
                break; // channel completed (disposed)
            }
        }

        _channel.Writer.TryComplete();
    }
}
