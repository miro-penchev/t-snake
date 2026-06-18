using TSnake.Core;

namespace TSnake.Screens;

/// <summary>
/// The pure <see cref="EndReason"/> → headline map for the results screen (plan 07 §2.5). Kept
/// separate and Console-free so every reason's wording is unit-testable. <see cref="EndReason.None"/>
/// is the quit/abandon case, which never reaches results, so it has no headline of its own.
/// </summary>
public static class EndMessages
{
    public static string Headline(EndReason reason) => reason switch
    {
        EndReason.HitSelf => "You bit yourself!",
        EndReason.HitObstacle => "You hit an obstacle!",
        EndReason.BoardFull => "You filled the board — you win!",
        _ => "Game over.",
    };
}
