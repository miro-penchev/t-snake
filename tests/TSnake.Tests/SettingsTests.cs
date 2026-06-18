using TSnake.Core;
using TSnake.Loop;
using TSnake.Settings;

namespace TSnake.Tests;

public class SettingsTests
{
    // A temp file path that does not yet exist; deleted on dispose along with its folder.
    private sealed class TempSettings : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "t-snake-tests", Guid.NewGuid().ToString("N"));
        public string FilePath => Path.Combine(Dir, "settings.json");

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

    // ---- BuildSession merge (plan §6) --------------------------------------

    [Fact]
    public void EasyHasNoObstaclesIsPausableAndDoesNotSpeedUp()
    {
        SessionConfig session = GameSettings.CreateDefault().BuildSession(Difficulty.Easy, level: 1, seed: 0);

        Assert.False(session.Engine.ObstaclesEnabled);
        Assert.True(session.PauseAllowed);
        Assert.Equal(0.0, session.Speed.ShrinkRate);
    }

    [Fact]
    public void TerminatorEnablesObstaclesRampsSpeedAndForbidsPause()
    {
        SessionConfig easy = GameSettings.CreateDefault().BuildSession(Difficulty.Easy, level: 1, seed: 0);
        SessionConfig term = GameSettings.CreateDefault().BuildSession(Difficulty.Terminator, level: 1, seed: 0);

        Assert.True(term.Engine.ObstaclesEnabled);
        Assert.False(term.PauseAllowed);
        Assert.True(term.Engine.ObstacleSpawnRate > easy.Engine.ObstacleSpawnRate);
        Assert.True(term.Speed.ShrinkRate > easy.Speed.ShrinkRate);
    }

    [Fact]
    public void LevelDrivesBoardSizeBaseIntervalAndLevelMultiplier()
    {
        GameSettings settings = GameSettings.CreateDefault();
        SessionConfig l1 = settings.BuildSession(Difficulty.Easy, level: 1, seed: 0);
        SessionConfig l3 = settings.BuildSession(Difficulty.Easy, level: 3, seed: 0);

        Assert.True(l3.Engine.Width > l1.Engine.Width);
        Assert.True(l3.Engine.Height > l1.Engine.Height);
        Assert.NotEqual(l1.Speed.BaseIntervalMs, l3.Speed.BaseIntervalMs);
        Assert.True(l3.Engine.LevelMultiplier > l1.Engine.LevelMultiplier);
    }

    [Theory]
    [InlineData(Difficulty.Easy, 1)]
    [InlineData(Difficulty.Pro, 2)]
    [InlineData(Difficulty.Terminator, 3)]
    public void ScoreMultipliersAreDifficultyTimesLevel(Difficulty difficulty, int level)
    {
        SessionConfig session = GameSettings.CreateDefault().BuildSession(difficulty, level, seed: 0);

        // The product the renderer/score code applies must equal difficulty mult × level mult.
        int expectedDifficulty = difficulty switch
        {
            Difficulty.Easy => 1,
            Difficulty.Pro => 2,
            _ => 3,
        };
        Assert.Equal(expectedDifficulty, session.Engine.DifficultyMultiplier);
        Assert.Equal(level, session.Engine.LevelMultiplier);
    }

    [Fact]
    public void StartPositionIsTheBoardCentre()
    {
        GameSettings settings = GameSettings.CreateDefault();

        foreach (int level in new[] { 1, 2, 3 })
        {
            SessionConfig session = settings.BuildSession(Difficulty.Easy, level, seed: 0);
            var expected = new Point(session.Engine.Width / 2, session.Engine.Height / 2);
            Assert.Equal(expected, session.Engine.StartPosition);
        }
    }

    [Fact]
    public void SeedIsThePassedInValue()
    {
        SessionConfig session = GameSettings.CreateDefault().BuildSession(Difficulty.Pro, level: 2, seed: 12345);
        Assert.Equal(12345, session.Engine.Seed);
    }

    [Fact]
    public void SpeedDriverIsAlwaysFoodEaten()
    {
        SessionConfig session = GameSettings.CreateDefault().BuildSession(Difficulty.Pro, level: 2, seed: 0);
        Assert.Equal(SpeedDriver.FoodEaten, session.Speed.Driver);
    }

    // ---- Defaults / first run (plan §6) ------------------------------------

    [Fact]
    public void LoadOrDefaultWithNoFileYieldsDefaultsAndWritesTheFile()
    {
        using var temp = new TempSettings();

        GameSettings settings = GameSettings.LoadOrDefault(temp.FilePath);

        Assert.Equal(RenderMode.UnicodeColor, settings.Preferences.RenderMode);
        Assert.Equal(Difficulty.Easy, settings.Preferences.Difficulty);
        Assert.Equal(1, settings.Preferences.Level);
        Assert.True(File.Exists(temp.FilePath)); // first run writes a discoverable file
    }

    // ---- Overlay (plan §6) -------------------------------------------------

    [Fact]
    public void PartialFileOverridesOnlyItsFieldsAndKeepsTheRestDefault()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, """
            { "preferences": { "playerName": "Ada", "difficulty": "Terminator" } }
            """);

        GameSettings settings = GameSettings.LoadOrDefault(temp.FilePath);

        Assert.Equal("Ada", settings.Preferences.PlayerName);          // overridden
        Assert.Equal(Difficulty.Terminator, settings.Preferences.Difficulty); // overridden
        Assert.Equal(RenderMode.UnicodeColor, settings.Preferences.RenderMode); // default
        Assert.Equal(1, settings.Preferences.Level);                  // default
    }

    [Fact]
    public void TuningOverrideInFileIsHonoured()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, """
            { "levels": { "1": { "width": 30 } } }
            """);

        SessionConfig session = GameSettings.LoadOrDefault(temp.FilePath).BuildSession(Difficulty.Easy, level: 1, seed: 0);

        Assert.Equal(30, session.Engine.Width);
        Assert.Equal(16, session.Engine.Height); // untouched field keeps its default
    }

    // ---- Validation (plan §6) ----------------------------------------------

    [Fact]
    public void MalformedJsonFallsBackToDefaultsWithoutThrowing()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, "{ this is not valid json ");

        GameSettings settings = GameSettings.LoadOrDefault(temp.FilePath);

        Assert.Equal(Difficulty.Easy, settings.Preferences.Difficulty);
        Assert.Equal("Player", settings.Preferences.PlayerName);
    }

    [Fact]
    public void OutOfRangeValuesAreRejectedFieldByFieldBackToDefaults()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        // Negative width, spawn rate over 100, and a min interval above the (default) base.
        File.WriteAllText(temp.FilePath, """
            {
              "levels": { "1": { "width": -5, "minIntervalMs": 9999 } },
              "difficulties": { "Pro": { "obstacleSpawnRate": 250 } }
            }
            """);

        GameSettings settings = GameSettings.LoadOrDefault(temp.FilePath);
        SessionConfig session = settings.BuildSession(Difficulty.Pro, level: 1, seed: 0);

        Assert.Equal(24, session.Engine.Width);          // negative -> default
        Assert.Equal(70, session.Speed.MinIntervalMs);   // min above base -> default pair
        Assert.Equal(150, session.Speed.BaseIntervalMs);
        Assert.Equal(5, session.Engine.ObstacleSpawnRate); // 250 -> default
    }

    [Fact]
    public void LevelOutOfRangeInPreferencesFallsBackToDefault()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, """
            { "preferences": { "level": 99 } }
            """);

        GameSettings settings = GameSettings.LoadOrDefault(temp.FilePath);
        Assert.Equal(1, settings.Preferences.Level);
    }

    // ---- Round-trip (plan §6) ----------------------------------------------

    [Fact]
    public void SaveThenLoadReproducesPreferences()
    {
        using var temp = new TempSettings();
        Directory.CreateDirectory(temp.Dir);
        File.WriteAllText(temp.FilePath, """
            { "preferences": { "renderMode": "AsciiMonochrome", "difficulty": "Pro", "level": 3, "playerName": "Grace" } }
            """);

        GameSettings loaded = GameSettings.LoadOrDefault(temp.FilePath);
        loaded.Save(temp.FilePath);
        GameSettings reloaded = GameSettings.LoadOrDefault(temp.FilePath);

        Assert.Equal(loaded.Preferences, reloaded.Preferences);
        Assert.Equal(RenderMode.AsciiMonochrome, reloaded.Preferences.RenderMode);
        Assert.Equal(Difficulty.Pro, reloaded.Preferences.Difficulty);
        Assert.Equal(3, reloaded.Preferences.Level);
        Assert.Equal("Grace", reloaded.Preferences.PlayerName);
    }

    [Fact]
    public void DefaultSaveWritesNoTuningSectionsOnlyPreferences()
    {
        using var temp = new TempSettings();

        GameSettings.CreateDefault().Save(temp.FilePath);
        string json = File.ReadAllText(temp.FilePath);

        Assert.Contains("preferences", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("difficulties", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("levels", json, StringComparison.OrdinalIgnoreCase);
    }
}
