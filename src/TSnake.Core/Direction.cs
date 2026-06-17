namespace TSnake.Core;

/// <summary>Heading of the snake. Y increases downward (screen/grid convention).</summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}

public static class DirectionExtensions
{
    /// <summary>The unit step a heading adds to the head each tick.</summary>
    public static Point Delta(this Direction direction) => direction switch
    {
        Direction.Up => new Point(0, -1),
        Direction.Down => new Point(0, 1),
        Direction.Left => new Point(-1, 0),
        Direction.Right => new Point(1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    /// <summary>The 180° reversal of a heading (used by the reversal guard).</summary>
    public static Direction Opposite(this Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };
}
