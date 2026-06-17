namespace TSnake.Input;

/// <summary>
/// The default <see cref="IKeySource"/>, wrapping <see cref="Console.ReadKey(bool)"/> with
/// <c>intercept: true</c> so keystrokes never echo onto our rendered screen (plan §2.1).
/// </summary>
public sealed class ConsoleKeySource : IKeySource
{
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);
}
