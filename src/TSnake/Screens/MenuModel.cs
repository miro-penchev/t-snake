using TSnake.Settings;

namespace TSnake.Screens;

/// <summary>
/// The pure logic behind the main menu (plan 07 §2.1, §4): which row is highlighted, cycling each
/// selector through its allowed values, and mapping a Confirm to a <see cref="MenuAction"/>. It owns
/// no Console and no input — it transforms a <see cref="Preferences"/> as the player navigates, so the
/// drawing (<see cref="MainMenu"/>) and the wrapping/cycling rules can be tested apart.
/// </summary>
public sealed class MenuModel
{
    /// <summary>The menu's rows, top to bottom: three selectors, the name field, then three actions.</summary>
    public enum Row
    {
        Difficulty,
        Level,
        Render,
        Name,
        Play,
        HighScores,
        Quit,
    }

    private static readonly Row[] AllRows = Enum.GetValues<Row>();
    private static readonly Difficulty[] Difficulties = Enum.GetValues<Difficulty>();
    private static readonly RenderMode[] RenderModes = Enum.GetValues<RenderMode>();

    private readonly IReadOnlyList<int> _levels;

    public MenuModel(Preferences preferences, IReadOnlyList<int> availableLevels)
    {
        // A degenerate level list would make the Level selector un-cyclable; fall back to the current
        // value so the model is always coherent.
        _levels = availableLevels.Count > 0 ? availableLevels : new[] { preferences.Level };
        Preferences = preferences;
    }

    /// <summary>The live preferences, updated in place as selectors cycle (plan §2.1).</summary>
    public Preferences Preferences { get; private set; }

    /// <summary>Index of the highlighted row.</summary>
    public int SelectedIndex { get; private set; }

    /// <summary>The highlighted row.</summary>
    public Row Current => AllRows[SelectedIndex];

    /// <summary>The full row order, for the renderer.</summary>
    public static IReadOnlyList<Row> Rows => AllRows;

    /// <summary>Move the highlight up one row, wrapping at the top.</summary>
    public void MoveUp() => SelectedIndex = (SelectedIndex - 1 + AllRows.Length) % AllRows.Length;

    /// <summary>Move the highlight down one row, wrapping at the bottom.</summary>
    public void MoveDown() => SelectedIndex = (SelectedIndex + 1) % AllRows.Length;

    /// <summary>Cycle the current selector to its previous value (no-op on a non-selector row).</summary>
    public void CycleLeft() => Cycle(-1);

    /// <summary>Cycle the current selector to its next value (no-op on a non-selector row).</summary>
    public void CycleRight() => Cycle(+1);

    /// <summary>What a Confirm on the current row does.</summary>
    public MenuAction Activate() => Current switch
    {
        Row.Name => MenuAction.EditName,
        Row.Play => MenuAction.Play,
        Row.HighScores => MenuAction.HighScores,
        Row.Quit => MenuAction.Quit,
        _ => MenuAction.None,
    };

    /// <summary>Replace the player name (after the name-entry prompt commits).</summary>
    public void SetName(string name) => Preferences = Preferences with { PlayerName = name };

    private void Cycle(int direction)
    {
        Preferences = Current switch
        {
            Row.Difficulty => Preferences with { Difficulty = Step(Difficulties, Preferences.Difficulty, direction) },
            Row.Render => Preferences with { RenderMode = Step(RenderModes, Preferences.RenderMode, direction) },
            Row.Level => Preferences with { Level = Step(_levels, Preferences.Level, direction) },
            _ => Preferences, // Name and the action rows do not cycle.
        };
    }

    private static T Step<T>(IReadOnlyList<T> values, T current, int direction)
    {
        int i = IndexOf(values, current);
        int n = values.Count;
        return values[((i + direction) % n + n) % n];
    }

    private static int IndexOf<T>(IReadOnlyList<T> values, T current)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(values[i], current))
            {
                return i;
            }
        }

        return 0; // current outside the set (shouldn't happen): start cycling from the first value.
    }
}
