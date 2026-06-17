namespace TSnake.Core;

/// <summary>One cell whose content changed during a tick. The renderer paints exactly these.</summary>
public readonly record struct CellChange(Point Cell, CellKind Kind);
