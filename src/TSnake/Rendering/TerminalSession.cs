using System.Runtime.InteropServices;
using System.Text;

namespace TSnake.Rendering;

/// <summary>
/// Owns terminal setup and — critically — teardown. On construction it switches to UTF-8,
/// enables virtual-terminal processing, enters the alternate screen buffer, hides the cursor,
/// and clears. On <see cref="Dispose"/> it undoes all of that in reverse, and it also hooks
/// <see cref="Console.CancelKeyPress"/> so Ctrl-C can't leave a wrecked terminal (plan §4).
/// </summary>
public sealed partial class TerminalSession : IDisposable
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    private readonly Encoding _originalEncoding;
    private readonly uint _originalConsoleMode;
    private readonly bool _consoleModeRestorable;
    private readonly ConsoleCancelEventHandler _cancelHandler;
    private bool _disposed;

    public TerminalSession()
    {
        _originalEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;

        _consoleModeRestorable = TryEnableVirtualTerminal(out _originalConsoleMode);

        Console.Write(Ansi.EnterAltScreen);
        Console.Write(Ansi.HideCursor);
        Console.Write(Ansi.ClearScreen);
        Console.Out.Flush();

        // Restore on Ctrl-C, then let the default handler terminate the process.
        _cancelHandler = (_, _) => Dispose();
        Console.CancelKeyPress += _cancelHandler;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _cancelHandler;

        Console.Write(Ansi.ShowCursor);
        Console.Write(Ansi.LeaveAltScreen);
        Console.Out.Flush();

        if (_consoleModeRestorable && OperatingSystem.IsWindows())
        {
            SetConsoleMode(GetStdHandle(StdOutputHandle), _originalConsoleMode);
        }

        try
        {
            Console.OutputEncoding = _originalEncoding;
        }
        catch (IOException)
        {
            // Restoring encoding can fail if output was redirected mid-run; best effort.
        }
    }

    /// <summary>
    /// Blocks until the terminal is at least <paramref name="neededCols"/> × <paramref name="neededRows"/>,
    /// showing an "enlarge" prompt meanwhile. Returns false if the user pressed Esc/Q to give up,
    /// or true once the size is sufficient (or the size can't be read, e.g. redirected output).
    /// </summary>
    public static bool EnsureMinimumSize(int neededCols, int neededRows)
    {
        while (true)
        {
            int cols, rows;
            try
            {
                cols = Console.WindowWidth;
                rows = Console.WindowHeight;
            }
            catch (IOException)
            {
                return true; // Not a real interactive console; don't block.
            }

            if (cols >= neededCols && rows >= neededRows)
            {
                return true;
            }

            Console.Write(Ansi.ClearScreen);
            Console.Write(Ansi.MoveTo(1, 1));
            Console.Write($"Terminal too small: need {neededCols}x{neededRows}, have {cols}x{rows}. Enlarge it (Esc/Q to quit)...");
            Console.Out.Flush();

            if (Console.KeyAvailable)
            {
                ConsoleKey key = Console.ReadKey(intercept: true).Key;
                if (key is ConsoleKey.Escape or ConsoleKey.Q)
                {
                    return false;
                }
            }

            Thread.Sleep(150);
        }
    }

    private static bool TryEnableVirtualTerminal(out uint originalMode)
    {
        originalMode = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        IntPtr handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || !GetConsoleMode(handle, out originalMode))
        {
            return false;
        }

        return SetConsoleMode(handle, originalMode | EnableVirtualTerminalProcessing);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
