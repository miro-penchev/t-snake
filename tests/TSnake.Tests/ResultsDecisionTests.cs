using TSnake.Core;
using TSnake.Loop;
using TSnake.Persistence;
using TSnake.Screens;
using TSnake.Settings;

namespace TSnake.Tests;

public class ResultsDecisionTests
{
    private static GameOutcome Outcome(int score, bool quit) =>
        new(score, quit ? EndReason.None : EndReason.HitSelf,
            quit ? GameStatus.GameOver : GameStatus.GameOver, quit, Ticks: 10, Elapsed: TimeSpan.FromSeconds(1));

    private static HighScoreTable FullTableOf(int score)
    {
        var table = new HighScoreTable();
        for (int i = 0; i < HighScoreTable.MaxEntries; i++)
        {
            table.Add(new HighScoreEntry("P", score, Difficulty.Easy, Level: 1, new DateTimeOffset(2026, 1, 1, 0, 0, i, TimeSpan.Zero)));
        }

        return table;
    }

    [Fact]
    public void AnAbandonedGameIsNeverScoredEvenWithABigScore()
    {
        // An empty table would happily accept any score — but a quit-out must not be offered the prompt.
        var emptyTable = new HighScoreTable();
        Assert.False(ResultsScreen.QualifiesForEntry(Outcome(9999, quit: true), emptyTable));
    }

    [Fact]
    public void AFinishedGameThatPlacesQualifies()
    {
        var emptyTable = new HighScoreTable();
        Assert.True(ResultsScreen.QualifiesForEntry(Outcome(100, quit: false), emptyTable));
    }

    [Fact]
    public void AFinishedGameThatDoesNotPlaceDoesNotQualify()
    {
        // A full table of high scores; a low finished score should not be prompted.
        HighScoreTable full = FullTableOf(500);
        Assert.False(ResultsScreen.QualifiesForEntry(Outcome(10, quit: false), full));
    }

    [Fact]
    public void TheGateAgreesWithTheTablesOwnQualifyForPlayedGames()
    {
        HighScoreTable full = FullTableOf(500);
        // Mirrors HighScoreTable.Qualifies exactly when the game was actually played to its end.
        Assert.Equal(full.Qualifies(600), ResultsScreen.QualifiesForEntry(Outcome(600, quit: false), full));
        Assert.Equal(full.Qualifies(400), ResultsScreen.QualifiesForEntry(Outcome(400, quit: false), full));
    }
}
