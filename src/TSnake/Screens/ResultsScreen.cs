using TSnake.Loop;
using TSnake.Persistence;
using TSnake.Rendering;
using TSnake.Settings;

namespace TSnake.Screens;

/// <summary>
/// The results / game-over flow (plan 07 §2.5): it turns a finished <see cref="GameOutcome"/> into a
/// headline, runs the qualifying high-score path from plan 06 (prompt → build entry → add → save),
/// shows the table with any new rank highlighted, and offers Play again / Main menu / Quit. The
/// abandoned-game case (the player quit with Esc) never reaches here, so quitting is never scored.
/// </summary>
public sealed class ResultsScreen(TextUi ui, ScreenInput input, NamePrompt namePrompt)
{
    private static readonly string[] Actions = ["Play again", "Main menu", "Quit"];

    /// <summary>
    /// The pure gate for the high-score prompt (plan 07 §2.5, §4): a run earns the prompt only when it
    /// ended in play — not an Esc quit — <i>and</i> the score actually places. An abandoned game is
    /// never scored. This is the decision <see cref="Show"/> makes before prompting.
    /// </summary>
    public static bool QualifiesForEntry(GameOutcome outcome, HighScoreTable scores) =>
        !outcome.Quit && scores.Qualifies(outcome.Score);

    /// <summary>
    /// Runs the results screen for <paramref name="outcome"/> and returns the player's footer choice.
    /// Mutates and saves <paramref name="scores"/> when the score qualifies.
    /// </summary>
    public ResultsChoice Show(GameOutcome outcome, Preferences prefs, HighScoreTable scores, ITheme theme)
    {
        int rank = HighScoreTable.NoPlace;

        if (QualifiesForEntry(outcome, scores))
        {
            // Pre-fill from the saved name; a cancel/blank prompt falls back to it (a placed score
            // still needs a name). Persistence sanitizes the final value.
            string name = namePrompt.Prompt(prefs.PlayerName, theme) ?? prefs.PlayerName;
            var entry = new HighScoreEntry(
                HighScoreEntry.Sanitize(name), outcome.Score, prefs.Difficulty, prefs.Level, DateTimeOffset.Now);
            rank = scores.Add(entry);
            scores.Save();
        }

        return RunFooter(outcome, prefs, scores, theme, rank);
    }

    private ResultsChoice RunFooter(GameOutcome outcome, Preferences prefs, HighScoreTable scores, ITheme theme, int rank)
    {
        int index = 0;

        while (true)
        {
            Draw(outcome, prefs, scores, theme, rank, index);

            switch (input.ReadCommand())
            {
                case NavCommand.Up:
                case NavCommand.Left:
                    index = (index - 1 + Actions.Length) % Actions.Length;
                    break;
                case NavCommand.Down:
                case NavCommand.Right:
                    index = (index + 1) % Actions.Length;
                    break;
                case NavCommand.Confirm:
                    return (ResultsChoice)index;
                case NavCommand.Back:
                    return ResultsChoice.MainMenu;
            }
        }
    }

    private void Draw(GameOutcome outcome, Preferences prefs, HighScoreTable scores, ITheme theme, int rank, int index)
    {
        var lines = new List<TextUi.Line>
        {
            new(EndMessages.Headline(outcome.EndReason)),
            new(string.Empty),
            new($"Score: {outcome.Score}"),
            new($"{prefs.Difficulty} · Level {prefs.Level}"),
        };

        if (rank != HighScoreTable.NoPlace)
        {
            lines.Add(new TextUi.Line($"New high score — rank {rank}!"));
        }

        lines.Add(new TextUi.Line(string.Empty));
        lines.AddRange(HighScoreView.BuildRows(scores, rank));
        lines.Add(new TextUi.Line(string.Empty));

        for (int i = 0; i < Actions.Length; i++)
        {
            lines.Add(new TextUi.Line(Actions[i], i == index));
        }

        ui.Render("RESULTS", lines, theme);
    }
}
