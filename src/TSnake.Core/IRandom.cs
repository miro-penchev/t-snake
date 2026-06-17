namespace TSnake.Core;

/// <summary>
/// Abstraction over the random source so tests can inject a deterministic one.
/// Same seed + same inputs ⇒ identical run.
/// </summary>
public interface IRandom
{
    /// <summary>Returns a non-negative random integer less than <paramref name="maxExclusive"/>.</summary>
    int Next(int maxExclusive);
}

/// <summary>Production <see cref="IRandom"/> backed by a seeded <see cref="System.Random"/>.</summary>
public sealed class SeededRandom(int seed) : IRandom
{
    private readonly Random _random = new(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}
