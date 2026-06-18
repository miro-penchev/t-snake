using TSnake.Core;
using TSnake.Rendering;

namespace TSnake.Tests;

public class ThemeTests
{
    public static readonly TheoryData<CellKind> AllCellKinds =
    [
        CellKind.Empty, CellKind.SnakeHead, CellKind.SnakeBody, CellKind.Food, CellKind.Obstacle,
    ];

    [Theory]
    [MemberData(nameof(AllCellKinds))]
    public void UnicodeThemeGivesEveryKindATwoColumnGlyph(CellKind kind)
    {
        GlyphCell cell = new UnicodeColorTheme().Cell(kind, isCollision: false);
        Assert.Equal(2, cell.Glyph.Length);
    }

    [Theory]
    [MemberData(nameof(AllCellKinds))]
    public void AsciiThemeGivesEveryKindATwoColumnGlyph(CellKind kind)
    {
        GlyphCell cell = new AsciiMonochromeTheme().Cell(kind, isCollision: false);
        Assert.Equal(2, cell.Glyph.Length);
    }

    [Fact]
    public void BothThemesUseATwoColumnCell()
    {
        Assert.Equal(2, new UnicodeColorTheme().CellWidth);
        Assert.Equal(2, new AsciiMonochromeTheme().CellWidth);
        Assert.Equal(2, BoardGeometry.CellWidth);
    }

    [Theory]
    [MemberData(nameof(AllCellKinds))]
    public void AsciiThemeEmitsNoColor(CellKind kind)
    {
        var theme = new AsciiMonochromeTheme();
        GlyphCell cell = theme.Cell(kind, isCollision: false);

        Assert.Null(cell.Foreground);
        Assert.Null(cell.Background);
        Assert.Null(theme.HudColor);
    }

    [Theory]
    [MemberData(nameof(AllCellKinds))]
    public void UnicodeThemeColorsEveryCell(CellKind kind)
    {
        GlyphCell cell = new UnicodeColorTheme().Cell(kind, isCollision: false);

        Assert.NotNull(cell.Foreground);
        Assert.NotNull(cell.Background);
    }

    [Fact]
    public void CollisionGlyphOverridesTheCellKindInBothThemes()
    {
        Assert.Equal("✖ ", new UnicodeColorTheme().Cell(CellKind.SnakeBody, isCollision: true).Glyph);
        Assert.Equal("X ", new AsciiMonochromeTheme().Cell(CellKind.SnakeBody, isCollision: true).Glyph);
    }
}
