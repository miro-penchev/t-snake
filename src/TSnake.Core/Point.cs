namespace TSnake.Core;

/// <summary>
/// A grid coordinate. A value type with structural equality so it works as a
/// <see cref="HashSet{T}"/> / <see cref="Dictionary{TKey,TValue}"/> key.
/// </summary>
public readonly record struct Point(int X, int Y);
