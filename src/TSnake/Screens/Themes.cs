using TSnake.Rendering;
using TSnake.Settings;

namespace TSnake.Screens;

/// <summary>
/// The one place a render <i>preference</i> becomes a concrete <see cref="ITheme"/> (plan 07 §3,
/// plan 05 decision #7 — Settings never references Rendering). Shared by the menu, the game, and the
/// results screens so a single toggle re-skins everything, and used for live re-theming while the
/// player cycles the Render value in the menu.
/// </summary>
public static class Themes
{
    public static ITheme For(RenderMode mode) => mode switch
    {
        RenderMode.AsciiMonochrome => new AsciiMonochromeTheme(),
        _ => new UnicodeColorTheme(),
    };
}
