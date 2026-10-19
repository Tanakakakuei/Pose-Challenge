using System.Collections.Generic;

/// <summary>Cell overrides on a captured outline; one undo entry per brush stroke.</summary>
public sealed class CaptureMaskEdits
{
    private const int HistoryLimit = 50;
    private sbyte[,] cells; // 0: outline, 1: cube, -1: hole
    private sbyte[,] beforeStroke;
    private readonly List<sbyte[,]> undo = new List<sbyte[,]>();
    private readonly List<sbyte[,]> redo = new List<sbyte[,]>();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public int Count { get; private set; }

    public CaptureMaskEdits(int rows, int columns) => cells = new sbyte[rows, columns];

    public void BeginStroke() { EndStroke(); beforeStroke = (sbyte[,])cells.Clone(); }

    public bool Paint(int row, int column, bool solid)
    {
        if (row < 0 || column < 0 || row >= cells.GetLength(0) || column >= cells.GetLength(1)) return false;
        sbyte value = solid ? (sbyte)1 : (sbyte)-1;
        if (cells[row, column] == value) return false;
        if (beforeStroke == null) BeginStroke();
        if (cells[row, column] == 0) Count++;
        cells[row, column] = value;
        return true;
    }

    public void EndStroke()
    {
        if (beforeStroke == null) return;
        bool changed = false;
        for (int y = 0; y < cells.GetLength(0) && !changed; y++)
        for (int x = 0; x < cells.GetLength(1); x++)
            if (cells[y, x] != beforeStroke[y, x]) { changed = true; break; }
        if (changed)
        {
            undo.Add(beforeStroke);
            if (undo.Count > HistoryLimit) undo.RemoveAt(0);
            redo.Clear();
        }
        beforeStroke = null;
    }

    public bool Undo()
    {
        EndStroke();
        if (!CanUndo) return false;
        redo.Add(cells);
        Restore(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1);
        return true;
    }

    public bool Redo()
    {
        EndStroke();
        if (!CanRedo) return false;
        undo.Add(cells);
        Restore(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1);
        return true;
    }

    public bool Clear()
    {
        EndStroke();
        if (Count == 0) return false;
        BeginStroke();
        cells = new sbyte[cells.GetLength(0), cells.GetLength(1)]; Count = 0;
        EndStroke();
        return true;
    }

    private void Restore(sbyte[,] source)
    {
        // History entries must never be modified by a subsequent brush stroke.
        cells = (sbyte[,])source.Clone();
        Count = 0;
        foreach (sbyte cell in cells) if (cell != 0) Count++;
    }

    public bool[,] Apply(bool[,] outline)
    {
        var result = (bool[,])outline.Clone();
        for (int y = 0; y < cells.GetLength(0); y++)
        for (int x = 0; x < cells.GetLength(1); x++)
            if (cells[y, x] != 0) result[y, x] = cells[y, x] > 0;
        return result;
    }
}
