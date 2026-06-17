using TSnake.Input;

namespace TSnake.Tests;

public class KeyMapTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0') =>
        new(ch, key, shift: false, alt: false, control: false);

    [Theory]
    [InlineData(ConsoleKey.W, InputCommand.TurnUp)]
    [InlineData(ConsoleKey.UpArrow, InputCommand.TurnUp)]
    [InlineData(ConsoleKey.S, InputCommand.TurnDown)]
    [InlineData(ConsoleKey.DownArrow, InputCommand.TurnDown)]
    [InlineData(ConsoleKey.A, InputCommand.TurnLeft)]
    [InlineData(ConsoleKey.LeftArrow, InputCommand.TurnLeft)]
    [InlineData(ConsoleKey.D, InputCommand.TurnRight)]
    [InlineData(ConsoleKey.RightArrow, InputCommand.TurnRight)]
    [InlineData(ConsoleKey.Spacebar, InputCommand.Pause)]
    [InlineData(ConsoleKey.Escape, InputCommand.Quit)]
    public void MapsEverySpecifiedKey(ConsoleKey key, InputCommand expected)
    {
        Assert.Equal(expected, KeyMap.Map(Key(key)));
    }

    [Theory]
    [InlineData('w')]
    [InlineData('W')]
    public void FoldsLetterCase(char ch)
    {
        // The console reports both 'w' and 'W' as ConsoleKey.W, so casing never reaches the map.
        Assert.Equal(InputCommand.TurnUp, KeyMap.Map(Key(ConsoleKey.W, ch)));
    }

    [Theory]
    [InlineData(ConsoleKey.Q)] // deliberately NOT a quit key (too close to WASD)
    [InlineData(ConsoleKey.Enter)]
    [InlineData(ConsoleKey.Tab)]
    [InlineData(ConsoleKey.X)]
    public void UnknownKeysMapToNone(ConsoleKey key)
    {
        Assert.Equal(InputCommand.None, KeyMap.Map(Key(key)));
    }
}
