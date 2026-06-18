using TSnake.Screens;
using TSnake.Settings;

namespace TSnake.Tests;

public class MenuModelTests
{
    private static readonly int[] Levels = [1, 2, 3];

    private static Preferences Prefs(
        RenderMode render = RenderMode.UnicodeColor,
        Difficulty difficulty = Difficulty.Easy,
        int level = 1,
        string name = "Player") => new(render, difficulty, level, name);

    private static MenuModel NewModel(Preferences? prefs = null) =>
        new(prefs ?? Prefs(), Levels);

    // ---- Up/Down navigation (plan §4) --------------------------------------

    [Fact]
    public void StartsOnTheFirstRow()
    {
        Assert.Equal(MenuModel.Row.Difficulty, NewModel().Current);
    }

    [Fact]
    public void MoveDownAdvancesThroughTheRows()
    {
        MenuModel m = NewModel();
        m.MoveDown();
        Assert.Equal(MenuModel.Row.Level, m.Current);
        m.MoveDown();
        Assert.Equal(MenuModel.Row.Render, m.Current);
    }

    [Fact]
    public void MoveUpFromTheTopWrapsToTheBottom()
    {
        MenuModel m = NewModel();
        m.MoveUp();
        Assert.Equal(MenuModel.Row.Quit, m.Current);
    }

    [Fact]
    public void MoveDownFromTheBottomWrapsToTheTop()
    {
        MenuModel m = NewModel();
        for (int i = 0; i < MenuModel.Rows.Count - 1; i++)
        {
            m.MoveDown();
        }

        Assert.Equal(MenuModel.Row.Quit, m.Current);
        m.MoveDown();
        Assert.Equal(MenuModel.Row.Difficulty, m.Current);
    }

    // ---- Value cycling (plan §4) -------------------------------------------

    [Fact]
    public void CycleRightAdvancesDifficultyAndWraps()
    {
        MenuModel m = NewModel(Prefs(difficulty: Difficulty.Easy));

        m.CycleRight();
        Assert.Equal(Difficulty.Pro, m.Preferences.Difficulty);
        m.CycleRight();
        Assert.Equal(Difficulty.Terminator, m.Preferences.Difficulty);
        m.CycleRight();
        Assert.Equal(Difficulty.Easy, m.Preferences.Difficulty); // wraps
    }

    [Fact]
    public void CycleLeftFromTheFirstDifficultyWrapsToTheLast()
    {
        MenuModel m = NewModel(Prefs(difficulty: Difficulty.Easy));
        m.CycleLeft();
        Assert.Equal(Difficulty.Terminator, m.Preferences.Difficulty);
    }

    [Fact]
    public void CyclesLevelThroughTheAvailableValues()
    {
        MenuModel m = NewModel(Prefs(level: 1));
        m.MoveDown(); // Level row

        m.CycleRight();
        Assert.Equal(2, m.Preferences.Level);
        m.CycleRight();
        Assert.Equal(3, m.Preferences.Level);
        m.CycleRight();
        Assert.Equal(1, m.Preferences.Level); // wraps
    }

    [Fact]
    public void CyclesRenderModeAndLeavesItOnPreferences()
    {
        MenuModel m = NewModel(Prefs(render: RenderMode.UnicodeColor));
        m.MoveDown();
        m.MoveDown(); // Render row

        m.CycleRight();
        Assert.Equal(RenderMode.AsciiMonochrome, m.Preferences.RenderMode);
        m.CycleRight();
        Assert.Equal(RenderMode.UnicodeColor, m.Preferences.RenderMode); // two modes, wraps
    }

    [Fact]
    public void CyclingDoesNotDisturbOtherPreferenceFields()
    {
        MenuModel m = NewModel(Prefs(difficulty: Difficulty.Easy, level: 2, name: "Ada"));
        m.CycleRight(); // Difficulty -> Pro

        Assert.Equal(Difficulty.Pro, m.Preferences.Difficulty);
        Assert.Equal(2, m.Preferences.Level);
        Assert.Equal("Ada", m.Preferences.PlayerName);
        Assert.Equal(RenderMode.UnicodeColor, m.Preferences.RenderMode);
    }

    [Fact]
    public void CyclingAnActionRowDoesNothing()
    {
        MenuModel m = NewModel(Prefs(difficulty: Difficulty.Easy, level: 1));
        while (m.Current != MenuModel.Row.Play)
        {
            m.MoveDown();
        }

        m.CycleLeft();
        m.CycleRight();
        Assert.Equal(Difficulty.Easy, m.Preferences.Difficulty);
        Assert.Equal(1, m.Preferences.Level);
    }

    // ---- Activation: the right row -> the right action (plan §4) ------------

    [Theory]
    [InlineData(MenuModel.Row.Name, MenuAction.EditName)]
    [InlineData(MenuModel.Row.Play, MenuAction.Play)]
    [InlineData(MenuModel.Row.HighScores, MenuAction.HighScores)]
    [InlineData(MenuModel.Row.Quit, MenuAction.Quit)]
    public void ActivateMapsActionRowsToActions(MenuModel.Row row, MenuAction expected)
    {
        MenuModel m = NewModel();
        while (m.Current != row)
        {
            m.MoveDown();
        }

        Assert.Equal(expected, m.Activate());
    }

    [Theory]
    [InlineData(MenuModel.Row.Difficulty)]
    [InlineData(MenuModel.Row.Level)]
    [InlineData(MenuModel.Row.Render)]
    public void ActivateOnASelectorIsANoOpAction(MenuModel.Row row)
    {
        MenuModel m = NewModel();
        while (m.Current != row)
        {
            m.MoveDown();
        }

        Assert.Equal(MenuAction.None, m.Activate());
    }

    // ---- Name update (plan §2.2) -------------------------------------------

    [Fact]
    public void SetNameUpdatesPreferences()
    {
        MenuModel m = NewModel(Prefs(name: "Player"));
        m.SetName("Grace");
        Assert.Equal("Grace", m.Preferences.PlayerName);
    }
}
