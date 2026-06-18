namespace TSnake.Settings;

/// <summary>
/// The app-side difficulty tier the player picks. It is a <i>name</i> for a bundle of tuning
/// numbers; the engine and loop never see it (plan §3, decision #4) — Settings translates it,
/// together with a level, into the plain-number configs those modules consume.
/// </summary>
public enum Difficulty
{
    Easy,
    Pro,
    Terminator,
}
