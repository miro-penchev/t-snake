namespace TSnake.Settings;

/// <summary>
/// A render <i>preference</i> the player stores. Settings deliberately does not reference the
/// renderer (decision #7): composition (<c>Program</c>) maps this onto a concrete
/// <c>ITheme</c>. Kept here because it is a persisted choice, not a rule.
/// </summary>
public enum RenderMode
{
    UnicodeColor,
    AsciiMonochrome,
}
