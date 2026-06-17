namespace TSnake.Input;

/// <summary>
/// The swappable keyboard seam. <see cref="ConsoleKeySource"/> is the default; if measured latency
/// at the fastest speeds ever demands it, a Win32 <c>ReadConsoleInput</c> P/Invoke implementation can
/// drop in here without touching mapping or buffering (plan §2.3). <see cref="ReadKey"/> blocks until
/// a key is available, mirroring <see cref="Console.ReadKey(bool)"/>.
/// </summary>
public interface IKeySource
{
    ConsoleKeyInfo ReadKey();
}
