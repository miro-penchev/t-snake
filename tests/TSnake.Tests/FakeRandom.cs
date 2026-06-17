using TSnake.Core;

namespace TSnake.Tests;

/// <summary>
/// A fully deterministic <see cref="IRandom"/> that returns a scripted sequence of values,
/// then 0 once the script is exhausted (always a valid index, since the engine only calls
/// <c>Next(n)</c> with n &gt; 0). Lets a test pin food/obstacle placement exactly.
/// </summary>
internal sealed class FakeRandom(params int[] values) : IRandom
{
    private readonly Queue<int> _values = new(values);

    public int Next(int maxExclusive) => _values.Count > 0 ? _values.Dequeue() : 0;
}
