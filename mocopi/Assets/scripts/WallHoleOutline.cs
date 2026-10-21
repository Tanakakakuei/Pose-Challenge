using UnityEngine;

/// <summary>Marks only the cell edges that border an authored hole, excluding the wall perimeter.</summary>
public static class WallHoleOutline
{
    private static MaterialPropertyBlock properties;

    // Mask rows run from top to bottom; cube-local Y runs from bottom to top.
    public static Vector4 GetEdges(bool[,] mask, int row, int column)
    {
        if (mask == null || row < 0 || column < 0 || row >= mask.GetLength(0) ||
            column >= mask.GetLength(1) || !mask[row, column]) return Vector4.zero;

        return new Vector4(
            column > 0 && !mask[row, column - 1] ? 1 : 0,
            column + 1 < mask.GetLength(1) && !mask[row, column + 1] ? 1 : 0,
            row + 1 < mask.GetLength(0) && !mask[row + 1, column] ? 1 : 0,
            row > 0 && !mask[row - 1, column] ? 1 : 0);
    }

    public static void Apply(WallCube cube, bool[,] mask, int row, int column)
    {
        SetEdges(cube, GetEdges(mask, row, column));
    }

    public static void Clear(WallCube cube) => SetEdges(cube, Vector4.zero);

    private static void SetEdges(WallCube cube, Vector4 edges)
    {
        if (!cube.TryGetComponent<Renderer>(out var renderer)) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        properties.Clear();
        renderer.GetPropertyBlock(properties);
        properties.SetVector("_HoleEdges", edges);
        renderer.SetPropertyBlock(properties);
    }
}
