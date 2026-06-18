using TSnake.Rendering;
using TSnake.Settings;

namespace TSnake.Screens;

/// <summary>
/// The main menu screen (plan 07 §2.1): a single vertical list mixing value selectors
/// (Difficulty / Level / Render) and actions (Name / Play / High Scores / Quit). It draws via
/// <see cref="TextUi"/>, navigates via <see cref="ScreenInput"/>, and holds the logic in the pure
/// <see cref="MenuModel"/>. Cycling a value updates the live <see cref="GameSettings.Preferences"/>
/// immediately, and toggling Render re-themes the menu on the spot so the look is visible before play.
/// Leaving into a game writes the chosen preferences back to <paramref name="settings"/>.
/// </summary>
public sealed class MainMenu(TextUi ui, ScreenInput input, NamePrompt namePrompt, GameSettings settings)
{
    private const string Title = "T - S N A K E";

    /// <summary>
    /// Runs the menu until the player picks Play, High Scores, or Quit, returning that action with
    /// the live preferences already committed to <see cref="GameSettings.Preferences"/>.
    /// </summary>
    public MenuAction Show()
    {
        var model = new MenuModel(settings.Preferences, settings.AvailableLevels);

        while (true)
        {
            ITheme theme = Themes.For(model.Preferences.RenderMode);
            Draw(model, theme);

            switch (input.ReadCommand())
            {
                case NavCommand.Up:
                    model.MoveUp();
                    break;
                case NavCommand.Down:
                    model.MoveDown();
                    break;
                case NavCommand.Left:
                    model.CycleLeft();
                    break;
                case NavCommand.Right:
                    model.CycleRight();
                    break;
                case NavCommand.Confirm:
                    MenuAction action = model.Activate();
                    if (action == MenuAction.EditName)
                    {
                        EditName(model, theme);
                    }
                    else if (action != MenuAction.None)
                    {
                        settings.Preferences = model.Preferences;
                        return action;
                    }

                    break;
                case NavCommand.Back:
                    // No parent screen above the main menu, so Esc is a clean way out of the app.
                    settings.Preferences = model.Preferences;
                    return MenuAction.Quit;
            }
        }
    }

    private void Draw(MenuModel model, ITheme theme)
    {
        Preferences p = model.Preferences;
        bool ascii = p.RenderMode == RenderMode.AsciiMonochrome;
        string left = ascii ? "<" : "◄";
        string right = ascii ? ">" : "►";

        var lines = new List<TextUi.Line>
        {
            Selector("Difficulty", p.Difficulty.ToString(), left, right, model.Current == MenuModel.Row.Difficulty),
            Selector("Level", p.Level.ToString(), left, right, model.Current == MenuModel.Row.Level),
            Selector("Render", RenderLabel(p.RenderMode), left, right, model.Current == MenuModel.Row.Render),
            NameRow(p.PlayerName, model.Current == MenuModel.Row.Name),
            new TextUi.Line(string.Empty),
            Action("Play", model.Current == MenuModel.Row.Play),
            Action("High Scores", model.Current == MenuModel.Row.HighScores),
            Action("Quit", model.Current == MenuModel.Row.Quit),
        };

        ui.Render(Title, lines, theme);
    }

    private void EditName(MenuModel model, ITheme theme)
    {
        string? name = namePrompt.Prompt(model.Preferences.PlayerName, theme);
        if (name is not null)
        {
            model.SetName(name);
        }
    }

    private static TextUi.Line Selector(string label, string value, string left, string right, bool selected) =>
        new($"{label,-11}{left} {value,-10} {right}", selected);

    private static TextUi.Line NameRow(string name, bool selected)
    {
        string text = $"{"Name",-11}{name}";
        if (selected)
        {
            text += "   (Enter to edit)";
        }

        return new TextUi.Line(text, selected);
    }

    private static TextUi.Line Action(string label, bool selected) => new(label, selected);

    private static string RenderLabel(RenderMode mode) =>
        mode == RenderMode.AsciiMonochrome ? "ASCII" : "Unicode";
}
