using TSnake.Persistence;
using TSnake.Settings;

namespace TSnake.Tests;

public class HighScoreTableTests
{
    // A temp file path that does not yet exist; deleted on dispose along with its folder.
    private sealed class TempScores : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "t-snake-tests", Guid.NewGuid().ToString("N"));
        public string FilePath => Path.Combine(Dir, "highscores.json");

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Dir))
                {
                    Directory.Delete(Dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private static readonly DateTimeOffset Base = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static HighScoreEntry Entry(int score, int minutesLater = 0, string name = "P") =>
        new(name, score, Difficulty.Easy, Level: 1, Base.AddMinutes(minutesLater));

    // ---- Ordering (plan §6) ------------------------------------------------

    [Fact]
    public void AddKeepsListSortedByScoreDescending()
    {
        var table = new HighScoreTable();
        table.Add(Entry(50));
        table.Add(Entry(100));
        table.Add(Entry(75));

        Assert.Equal(new[] { 100, 75, 50 }, table.Entries.Select(e => e.Score));
    }

    [Fact]
    public void EqualScoresBreakTieByEarlierDateFirst()
    {
        var table = new HighScoreTable();
        HighScoreEntry later = Entry(100, minutesLater: 10, name: "Late");
        HighScoreEntry earlier = Entry(100, minutesLater: 0, name: "Early");

        table.Add(later);
        table.Add(earlier);

        Assert.Equal(new[] { "Early", "Late" }, table.Entries.Select(e => e.Name));
    }

    // ---- Cap (plan §6) -----------------------------------------------------

    [Fact]
    public void AddingBeyondMaxEntriesDropsTheLowestAndNeverExceedsTheCap()
    {
        var table = new HighScoreTable();
        for (int i = 1; i <= HighScoreTable.MaxEntries + 5; i++)
        {
            table.Add(Entry(i * 10, minutesLater: i));
        }

        Assert.Equal(HighScoreTable.MaxEntries, table.Entries.Count);
        // The 5 lowest (10..50) were dropped; the lowest survivor is 60.
        Assert.Equal(60, table.Entries[^1].Score);
        Assert.Equal(150, table.Entries[0].Score);
    }

    // ---- Qualifies (plan §6) -----------------------------------------------

    [Fact]
    public void QualifiesIsTrueWhileTableIsNotFull()
    {
        var table = new HighScoreTable();
        Assert.True(table.Qualifies(0));
    }

    [Fact]
    public void QualifiesComparesAgainstLowestWhenFull()
    {
        var table = new HighScoreTable();
        for (int i = 1; i <= HighScoreTable.MaxEntries; i++)
        {
            table.Add(Entry(i * 10, minutesLater: i));
        }

        // Lowest is 10.
        Assert.True(table.Qualifies(11));   // beats lowest
        Assert.False(table.Qualifies(10));  // ties lowest -> does not place
        Assert.False(table.Qualifies(5));   // below lowest
    }

    [Fact]
    public void QualifiesAgreesWithWhatAddDoes()
    {
        var table = new HighScoreTable();
        for (int i = 1; i <= HighScoreTable.MaxEntries; i++)
        {
            table.Add(Entry(i * 10, minutesLater: i));
        }

        // A non-qualifying score really fails to place; a qualifying one really places.
        Assert.False(table.Qualifies(10));
        Assert.Equal(HighScoreTable.NoPlace, table.Add(Entry(10, minutesLater: 100)));

        Assert.True(table.Qualifies(11));
        Assert.NotEqual(HighScoreTable.NoPlace, table.Add(Entry(11, minutesLater: 101)));
    }

    // ---- Rank (plan §6) ----------------------------------------------------

    [Fact]
    public void AddReturnsCorrectOneBasedRank()
    {
        var table = new HighScoreTable();
        Assert.Equal(1, table.Add(Entry(100)));     // first
        Assert.Equal(2, table.Add(Entry(50)));      // below
        Assert.Equal(1, table.Add(Entry(200)));     // new top, pushes others down
        Assert.Equal(2, table.Add(Entry(150)));     // slots between 200 and 100
    }

    [Fact]
    public void AddReturnsNoPlaceSentinelWhenScoreDoesNotPlace()
    {
        var table = new HighScoreTable();
        for (int i = 1; i <= HighScoreTable.MaxEntries; i++)
        {
            table.Add(Entry(i * 10, minutesLater: i));
        }

        Assert.Equal(HighScoreTable.NoPlace, table.Add(Entry(1, minutesLater: 100)));
        Assert.Equal(HighScoreTable.MaxEntries, table.Entries.Count);
    }

    // ---- Load resilience (plan §6) -----------------------------------------

    [Fact]
    public void MissingFileYieldsEmptyTable()
    {
        using var temp = new TempScores();
        HighScoreTable table = HighScoreTable.LoadOrEmpty(temp.FilePath);
        Assert.Empty(table.Entries);
    }

    [Fact]
    public void CorruptJsonYieldsEmptyTableWithoutThrowing()
    {
        using var temp = new TempScores();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, "{ this is not valid json ");

        HighScoreTable table = HighScoreTable.LoadOrEmpty(temp.FilePath);
        Assert.Empty(table.Entries);
    }

    [Fact]
    public void InvalidEntriesAreDroppedWhileValidOnesAreKept()
    {
        using var temp = new TempScores();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, """
            [
              { "name": "Good",      "score": 100, "difficulty": "Easy",    "level": 1, "date": "2026-01-01T12:00:00+00:00" },
              { "name": "Negative",  "score": -5,  "difficulty": "Easy",    "level": 1, "date": "2026-01-01T12:00:00+00:00" },
              { "name": "",          "score": 50,  "difficulty": "Easy",    "level": 1, "date": "2026-01-01T12:00:00+00:00" },
              { "name": "BadDiff",   "score": 50,  "difficulty": "Wizard",  "level": 1, "date": "2026-01-01T12:00:00+00:00" },
              { "name": "ZeroLevel", "score": 50,  "difficulty": "Easy",    "level": 0, "date": "2026-01-01T12:00:00+00:00" },
              { "name": "AlsoGood",  "score": 80,  "difficulty": "Pro",     "level": 2, "date": "2026-01-02T12:00:00+00:00" }
            ]
            """);

        HighScoreTable table = HighScoreTable.LoadOrEmpty(temp.FilePath);

        Assert.Equal(new[] { "Good", "AlsoGood" }, table.Entries.Select(e => e.Name));
    }

    // ---- Round-trip (plan §6) ----------------------------------------------

    [Fact]
    public void SaveThenLoadReproducesEntriesInOrder()
    {
        using var temp = new TempScores();
        var table = new HighScoreTable();
        table.Add(new HighScoreEntry("Ada", 300, Difficulty.Terminator, 3, Base));
        table.Add(new HighScoreEntry("Grace", 150, Difficulty.Pro, 2, Base.AddMinutes(5)));
        table.Add(new HighScoreEntry("Linus", 50, Difficulty.Easy, 1, Base.AddMinutes(10)));

        table.Save(temp.FilePath);
        HighScoreTable reloaded = HighScoreTable.LoadOrEmpty(temp.FilePath);

        Assert.Equal(table.Entries, reloaded.Entries);
    }

    [Fact]
    public void SaveDoesNotDestroyExistingFileAndWritesAtomically()
    {
        using var temp = new TempScores();
        var first = new HighScoreTable();
        first.Add(Entry(100));
        first.Save(temp.FilePath);

        var second = HighScoreTable.LoadOrEmpty(temp.FilePath);
        second.Add(Entry(200));
        second.Save(temp.FilePath); // exercises the File.Replace path (destination already exists)

        HighScoreTable reloaded = HighScoreTable.LoadOrEmpty(temp.FilePath);
        Assert.Equal(new[] { 200, 100 }, reloaded.Entries.Select(e => e.Score));
        Assert.False(File.Exists(temp.FilePath + ".tmp")); // no temp left behind
    }

    // ---- Sanitization (plan §6) --------------------------------------------

    [Fact]
    public void OverLongNamesAreCapped()
    {
        string raw = new('x', HighScoreEntry.MaxNameLength + 10);
        string clean = HighScoreEntry.Sanitize(raw);
        Assert.Equal(HighScoreEntry.MaxNameLength, clean.Length);
    }

    [Fact]
    public void ControlCharactersAndNewlinesAreStripped()
    {
        string clean = HighScoreEntry.Sanitize("A\r\nb\tcd");
        Assert.Equal("Abcd", clean);
    }

    [Fact]
    public void BlankOrNullNameSanitizesToEmpty()
    {
        Assert.Equal(string.Empty, HighScoreEntry.Sanitize(null));
        Assert.Equal(string.Empty, HighScoreEntry.Sanitize("   "));
    }
}
