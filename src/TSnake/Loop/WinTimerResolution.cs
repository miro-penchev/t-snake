using System.Runtime.InteropServices;

namespace TSnake.Loop;

/// <summary>
/// Raises the system timer resolution to ~1 ms for the lifetime of this object so the loop's
/// coarse sleeps land close to a millisecond instead of Windows' default ~15 ms granularity —
/// without which a fast tick (say 50 ms) visibly stutters (plan §2.4). The raise/restore is
/// paired here so it is exception-safe via <c>using</c>. A no-op off Windows.
/// </summary>
public sealed partial class WinTimerResolution : IDisposable
{
    private const uint TargetPeriodMs = 1;
    private const uint TimerrNoError = 0;

    private readonly bool _raised;

    public WinTimerResolution()
    {
        if (OperatingSystem.IsWindows())
        {
            _raised = TimeBeginPeriod(TargetPeriodMs) == TimerrNoError;
        }
    }

    public void Dispose()
    {
        if (_raised && OperatingSystem.IsWindows())
        {
            TimeEndPeriod(TargetPeriodMs);
        }
    }

    [LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static partial uint TimeBeginPeriod(uint uPeriod);

    [LibraryImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static partial uint TimeEndPeriod(uint uPeriod);
}
