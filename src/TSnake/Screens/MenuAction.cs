namespace TSnake.Screens;

/// <summary>
/// What a Confirm on the current menu row means (plan 07 §2.1). Selector rows yield
/// <see cref="None"/> (Confirm does nothing — Left/Right cycle them); the Name row opens text entry;
/// the three command rows leave the menu with that action.
/// </summary>
public enum MenuAction
{
    None,
    EditName,
    Play,
    HighScores,
    Quit,
}
