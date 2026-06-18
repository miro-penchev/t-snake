using System.Text;
using TSnake.Settings;

namespace TSnake.Persistence;

/// <summary>
/// One row of the high-score table (plan 06 §3): the spec's five fields and nothing more.
/// Composition assembles it from a finished game — the score from the <c>GameOutcome</c>, the
/// name/difficulty/level from <c>Preferences</c>, and the current timestamp — then hands it to
/// <see cref="HighScoreTable.Add"/>. Persistence never reads game state to build one itself.
/// </summary>
/// <param name="Name">Player name; sanitize untrusted input with <see cref="Sanitize"/> first.</param>
/// <param name="Score">Final score (the engine's already-multiplied number).</param>
/// <param name="Difficulty">Difficulty the run was played on — stored so a filtered view is possible later.</param>
/// <param name="Level">Level the run was played on (1-based).</param>
/// <param name="Date">When the score was achieved; ISO round-trippable on disk.</param>
public sealed record HighScoreEntry(
    string Name, int Score, Difficulty Difficulty, int Level, DateTimeOffset Date)
{
    /// <summary>Upper bound on a stored name, so one entry can't bloat the file or break the grid.</summary>
    public const int MaxNameLength = 24;

    /// <summary>
    /// Lightly cleans a player-supplied name on the way in (plan §2.4): strips control characters
    /// and newlines so a stray value can't corrupt the JSON file or break the renderer's grid,
    /// trims surrounding whitespace, and caps the length. Returns <see cref="string.Empty"/> for a
    /// null/blank input — the caller decides whether to substitute a placeholder, and a blank name
    /// is dropped on load.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (!char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        string cleaned = sb.ToString().Trim();
        return cleaned.Length <= MaxNameLength ? cleaned : cleaned[..MaxNameLength];
    }
}
