using TSnake.Input;

namespace TSnake.Tests;

/// <summary>
/// A deterministic <see cref="IKeySource"/> for testing the whole map→buffer pipeline without a real
/// keyboard or timing — the input-side analogue of <c>FakeRandom</c>. It hands out a scripted key
/// sequence, then parks (blocking like a real idle keyboard) so the background reader thread doesn't spin.
/// </summary>
/// <remarks>
/// <see cref="Drained"/> is set only once the background thread asks for a key <em>past</em> the end of
/// the script. Because that read happens strictly after the previous key was written to the channel, a
/// test that waits on <see cref="Drained"/> is guaranteed every scripted key is already in the channel,
/// so a single <c>Poll()</c> sees them all — no sleeps, no races.
/// </remarks>
internal sealed class ScriptedKeySource(params ConsoleKeyInfo[] keys) : IKeySource, IDisposable
{
    private readonly Queue<ConsoleKeyInfo> _keys = new(keys);
    private readonly ManualResetEventSlim _drained = new(false);
    private readonly ManualResetEventSlim _park = new(false); // never set until Dispose -> blocks the reader

    public ManualResetEventSlim Drained => _drained;

    public ConsoleKeyInfo ReadKey()
    {
        lock (_keys)
        {
            if (_keys.Count > 0)
            {
                return _keys.Dequeue();
            }
        }

        _drained.Set();
        _park.Wait();
        return default; // unreached: the park wait only returns on Dispose, during teardown
    }

    // Release the parked reader thread so it can exit. The wait handles aren't disposed: the
    // background thread may still be unwinding through _park.Wait(), and disposing under it would race.
    public void Dispose() => _park.Set();
}
