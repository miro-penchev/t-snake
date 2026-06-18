using TSnake.Core;

namespace TSnake.Input;

/// <summary>
/// The loop-facing surface (plan §5). Constructed with an <see cref="IKeySource"/>, it starts the
/// background <see cref="KeyboardReader"/> and, each tick, <see cref="Poll"/> drains the channel,
/// maps every keystroke with <see cref="KeyMap"/>, feeds turns into the <see cref="TurnBuffer"/>, and
/// latches pause/quit. The loop then pulls at most one turn via <see cref="TryNextTurn"/> and reads the
/// flags. All of this runs on the single loop thread, so the only thread-safe handoff is the channel.
/// </summary>
public sealed class InputService : IDisposable
{
    private readonly KeyboardReader _reader;
    private readonly bool _ownsReader;
    private readonly TurnBuffer _buffer = new();

    private bool _pauseRequested;
    private bool _quitRequested;

    /// <summary>Starts and owns a background reader over <paramref name="source"/>.</summary>
    public InputService(IKeySource source)
    {
        _reader = new KeyboardReader(source);
        _ownsReader = true;
    }

    /// <summary>
    /// Consumes the <i>shared</i>, app-lifetime <see cref="KeyboardReader"/> (plan 07 §2.3): the menu
    /// and the game read the same channel through different adapters, one at a time. The reader is
    /// owned by the composition root, so this service must not dispose it.
    /// </summary>
    public InputService(KeyboardReader reader)
    {
        _reader = reader;
        _ownsReader = false;
    }

    /// <summary>Called once per tick: drain the channel, map keys, fill the buffer and the flags.</summary>
    public void Poll()
    {
        while (_reader.Reader.TryRead(out ConsoleKeyInfo key))
        {
            switch (KeyMap.Map(key))
            {
                case InputCommand.TurnUp: _buffer.Enqueue(Direction.Up); break;
                case InputCommand.TurnDown: _buffer.Enqueue(Direction.Down); break;
                case InputCommand.TurnLeft: _buffer.Enqueue(Direction.Left); break;
                case InputCommand.TurnRight: _buffer.Enqueue(Direction.Right); break;
                case InputCommand.Pause: _pauseRequested = true; break;
                case InputCommand.Quit: _quitRequested = true; break;
                case InputCommand.None: break;
            }
        }
    }

    /// <summary>Dequeue at most one buffered turn for this tick.</summary>
    public bool TryNextTurn(out Direction d) => _buffer.TryDequeue(out d);

    /// <summary>True once if a pause was requested since the last poll; resets on read.</summary>
    public bool ConsumePause()
    {
        if (!_pauseRequested)
        {
            return false;
        }

        _pauseRequested = false;
        return true;
    }

    /// <summary>True once quit has been requested. Latches — quit is a one-way trip.</summary>
    public bool ConsumeQuit() => _quitRequested;

    public void Dispose()
    {
        if (_ownsReader)
        {
            _reader.Dispose();
        }
    }
}
