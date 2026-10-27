using UnityEngine;

/// <summary>Fits the complete cut-out inside a narrower wall without changing the saved source mask.</summary>
public static class WallMaskFitting
{
    public static bool[,] FitToWidth(bool[,] source, float authoredWidth, float visibleWidth)
    {
        if (source == null || authoredWidth <= 0f || visibleWidth <= 0f || visibleWidth >= authoredWidth) return source;
        int rows = source.GetLength(0), columns = source.GetLength(1);
        float cellWidth = authoredWidth / columns;
        float halfWidth = visibleWidth * 0.5f;
        // Keep a solid border at the sides and top; feet may open through the bottom edge.
        int firstColumn = Mathf.Clamp(Mathf.CeilToInt((authoredWidth * 0.5f - halfWidth) / cellWidth + 1f - 0.0001f), 0, columns - 1);
        int lastColumn = Mathf.Clamp(Mathf.FloorToInt((authoredWidth * 0.5f + halfWidth) / cellWidth - 1f + 0.0001f) - 1, 0, columns - 1);
        int firstRow = rows > 2 ? 1 : 0, lastRow = rows - 1;
        if (lastColumn < firstColumn) return source;

        int minX = columns, maxX = -1, minY = rows, maxY = -1;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            if (source[y, x]) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < 0 || (minX >= firstColumn && maxX <= lastColumn && minY >= firstRow && maxY <= lastRow)) return source;

        int width = maxX - minX + 1, height = maxY - minY + 1;
        float scale = Mathf.Min(1f, (float)(lastColumn - firstColumn + 1) / width, (float)(lastRow - firstRow + 1) / height);
        int targetWidth = Mathf.Max(1, Mathf.FloorToInt(width * scale + 0.0001f));
        int targetHeight = Mathf.Max(1, Mathf.FloorToInt(height * scale + 0.0001f));
        // Keep the old position when possible; translate only enough to fit inside the border.
        int originX = Mathf.Clamp(Mathf.RoundToInt(minX + (width - targetWidth) * 0.5f), firstColumn, lastColumn - targetWidth + 1);
        // Keep grounded poses on the floor even when fitting requires a smaller shape.
        int originY = maxY == rows - 1 ? rows - targetHeight
            : Mathf.Clamp(Mathf.RoundToInt(minY + (height - targetHeight) * 0.5f), firstRow, lastRow - targetHeight + 1);
        var fitted = new bool[rows, columns];
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++) fitted[y, x] = true;

        // Area sampling retains thin arms/legs when a large shape must be reduced.
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (source[y, x]) continue;
            int left = (x - minX) * targetWidth / width;
            int right = Mathf.Min(targetWidth - 1, Mathf.CeilToInt((float)(x - minX + 1) * targetWidth / width) - 1);
            int top = (y - minY) * targetHeight / height;
            int bottom = Mathf.Min(targetHeight - 1, Mathf.CeilToInt((float)(y - minY + 1) * targetHeight / height) - 1);
            for (int yy = top; yy <= bottom; yy++)
            for (int xx = left; xx <= right; xx++) fitted[originY + yy, originX + xx] = false;
        }
        return fitted;
    }
}