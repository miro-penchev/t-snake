using TSnake.Persistence;
using TSnake.Rendering;

namespace TSnake.Screens;

/// <summary>
/// Renders the high-score table (plan 07 §2.5, decision #9: one combined list for the POC). Used
/// standalone from the menu via <see cref="Show"/>, and its <see cref="BuildRows"/> is reused by
/// <see cref="ResultsScreen"/> to show the table with a freshly-placed rank highlighted.
/// </summary>
public sealed class HighScoreView(TextUi ui, ScreenInput input)
{
    private const string Title = "HIGH SCORES";

    /// <summary>Draws the table and waits for Enter/Esc to return to the caller.</summary>
    public void Show(HighScoreTable scores, ITheme theme)
    {
        var lines = new List<TextUi.Line>(BuildRows(scores, HighScoreTable.NoPlace))
        {
            new(string.Empty),
            new("Enter / Esc to return"),
        };

        ui.Render(Title, lines, theme);

        while (input.ReadCommand() is not (NavCommand.Confirm or NavCommand.Back))
        {
            // ignore navigation keys; only confirm/back leaves the view
        }
    }

    /// <summary>
    /// Builds the header + one row per entry, monospaced into columns. The row whose 1-based rank
    /// equals <paramref name="highlightRank"/> is marked highlighted (a just-added score); pass
    /// <see cref="HighScoreTable.NoPlace"/> for no highlight.
    /// </summary>
    public static IReadOnlyList<TextUi.Line> BuildRows(HighScoreTable scores, int highlightRank)
    {
        var lines = new List<TextUi.Line>
        {
            new($"  # {"NAME",-16}{"SCORE",7}  {"DIFF",-10} LV"),
        };

        if (scores.Entries.Count == 0)
        {
            lines.Add(new TextUi.Line("(no scores yet)"));
            return lines;
        }

        int rank = 1;
        foreach (HighScoreEntry e in scores.Entries)
        {
            string row = $"{rank,2}. {Fit(e.Name, 16),-16}{e.Score,7}  {e.Difficulty,-10} {e.Level,2}";
            lines.Add(new TextUi.Line(row, rank == highlightRank));
            rank++;
        }

        return lines;
    }

    private static string Fit(string text, int width) =>
        text.Length <= width ? text : text[..width];
}
