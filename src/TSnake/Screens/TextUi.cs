using System.Text;
using TSnake.Rendering;

namespace TSnake.Screens;

/// <summary>
/// Full-screen drawing primitives for the menus and result panels (plan 07 §2.2). Unlike the board
/// renderer — which diffs because only a few cells move per tick — screens never animate, so they
/// simply clear and redraw on each change: no buffers, no diff. It draws into the same
/// <see cref="TerminalSession"/> as the board and is handed the active <see cref="ITheme"/> per call,
/// so the menu, game, and results all wear the chosen skin and re-theme live when Render toggles.
/// </summary>
public sealed class TextUi(TerminalSession session)
{
    /// <summary>One line of a screen: its text and whether it is the highlighted (selected) row.</summary>
    public readonly record struct Line(string Text, bool Highlight = false);

    /// <summary>
    /// Clears and redraws a centered block: a title, a blank gap, then the body lines, each centered
    /// horizontally. The highlighted line is drawn in reverse video so the cue reads in both themes.
    /// </summary>
    public void Render(string title, IReadOnlyList<Line> lines, ITheme theme)
    {
        int width = session.Width;
        int height = session.Height;
        bool hasTitle = !string.IsNullOrEmpty(title);
        int blockRows = lines.Count + (hasTitle ? 2 : 0);
        int row = Math.Max(1, (height - blockRows) / 2 + 1);

        var sb = new StringBuilder();
        sb.Append(Ansi.ClearScreen);

        if (hasTitle)
        {
            AppendCentered(sb, row++, width, title, theme.HudColor, invert: false);
            row++; // a blank line between the title and the body
        }

        foreach (Line line in lines)
        {
            AppendCentered(sb, row++, width, line.Text, theme.HudColor, line.Highlight);
        }

        Write(sb.ToString());
    }

    /// <summary>
    /// Clears and draws a centered, theme-framed box around the given lines — the reusable primitive
    /// promised in plan 02 for the results panel, the name prompt, and a "too small" notice.
    /// </summary>
    public void RenderBox(IReadOnlyList<Line> lines, ITheme theme)
    {
        int width = session.Width;
        int height = session.Height;
        FrameStyle f = theme.Frame;
        Rgb? color = theme.HudColor;

        int longest = lines.Count == 0 ? 0 : lines.Max(l => l.Text.Length);
        int inner = longest + 2;            // one space of padding each side
        int boxWidth = inner + 2;           // plus the two vertical borders
        int boxHeight = lines.Count + 2;    // plus the top and bottom borders
        int startCol = Math.Max(1, (width - boxWidth) / 2 + 1);
        int startRow = Math.Max(1, (height - boxHeight) / 2 + 1);

        var sb = new StringBuilder();
        sb.Append(Ansi.ClearScreen);

        sb.Append(Ansi.MoveTo(startRow, startCol));
        AppendColored(sb, f.TopLeft + Repeat(f.Horizontal, inner) + f.TopRight, color);

        int row = startRow + 1;
        foreach (Line line in lines)
        {
            sb.Append(Ansi.MoveTo(row++, startCol));
            AppendColored(sb, f.Vertical, color);

            string cell = CenterPad(line.Text, inner);
            if (line.Highlight)
            {
                sb.Append(Ansi.Invert).Append(cell).Append(Ansi.Reset);
            }
            else
            {
                AppendColored(sb, cell, color);
            }

            AppendColored(sb, f.Vertical, color);
        }

        sb.Append(Ansi.MoveTo(row, startCol));
        AppendColored(sb, f.BottomLeft + Repeat(f.Horizontal, inner) + f.BottomRight, color);

        Write(sb.ToString());
    }

    private static void AppendCentered(StringBuilder sb, int row, int width, string text, Rgb? color, bool invert)
    {
        int col = Math.Max(1, (width - text.Length) / 2 + 1);
        sb.Append(Ansi.MoveTo(row, col));

        if (invert)
        {
            sb.Append(Ansi.Invert).Append(text).Append(Ansi.Reset);
        }
        else
        {
            AppendColored(sb, text, color);
        }
    }

    private static void AppendColored(StringBuilder sb, string text, Rgb? color)
    {
        if (color is { } c)
        {
            sb.Append(Ansi.Foreground(c)).Append(text).Append(Ansi.Reset);
        }
        else
        {
            sb.Append(text);
        }
    }

    private static string CenterPad(string text, int width)
    {
        if (text.Length >= width)
        {
            return text;
        }

        int left = (width - text.Length) / 2;
        return new string(' ', left) + text + new string(' ', width - text.Length - left);
    }

    private static string Repeat(string piece, int count)
    {
        var sb = new StringBuilder(piece.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(piece);
        }

        return sb.ToString();
    }

    private static void Write(string frame)
    {
        Console.Out.Write(frame);
        Console.Out.Flush();
    }
}
