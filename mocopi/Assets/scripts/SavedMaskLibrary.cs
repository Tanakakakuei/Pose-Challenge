using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Loads compatible masks once per round and draws shuffled bags without replacement.</summary>
public static class SavedMaskLibrary
{
    public static string BundledDirectory => Path.Combine(Application.streamingAssetsPath, "PoseMasks");

    /// <summary>Project poses travel with builds; locally captured poses are added without duplicate shapes.</summary>
    public static List<Entry> LoadDefault(int rows, int columns)
    {
        var entries = Load(BundledDirectory, rows, columns);
        var shapes = new HashSet<string>();
        foreach (var entry in entries) shapes.Add(ShapeKey(entry.Mask));
        foreach (var entry in Load(Application.persistentDataPath, rows, columns))
            if (shapes.Add(ShapeKey(entry.Mask))) entries.Add(entry);
        return entries;
    }

    private static string ShapeKey(bool[,] mask)
    {
        var key = new char[mask.Length];
        int index = 0;
        foreach (bool solid in mask) key[index++] = solid ? '1' : '0';
        return new string(key);
    }

    public sealed class Entry
    {
        public string Path { get; }
        public bool[,] Mask { get; }
        public Entry(string path, bool[,] mask) { Path = path; Mask = mask; }
    }

    public static List<Entry> Load(string directory, int rows, int columns)
    {
        var entries = new List<Entry>();
        if (!Directory.Exists(directory)) return entries;
        string[] paths;
        try { paths = Directory.GetFiles(directory, "*.json"); }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning($"SavedMaskLibrary: {exception.Message}");
            return entries;
        }
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (string path in paths)
        {
            if (!WallMaskData.TryReadSavedMask(path, out var mask) ||
                mask.GetLength(0) != rows || mask.GetLength(1) != columns) continue;
            bool hasHole = false, hasSolid = false;
            foreach (bool solid in mask) { hasHole |= !solid; hasSolid |= solid; }
            if (hasHole && hasSolid) entries.Add(new Entry(path, mask));
        }
        return entries;
    }

    public static List<Entry> SelectRound(IReadOnlyList<Entry> library, int count, System.Random random)
    {
        var result = new List<Entry>();
        if (library.Count == 0) return result;
        while (result.Count < count)
        {
            var bag = new List<Entry>(library);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                Entry swap = bag[i]; bag[i] = bag[j]; bag[j] = swap;
            }
            // When the library is small, use every mask before repeating; avoid adjacent repeats.
            if (bag.Count > 1 && result.Count > 0 && bag[0] == result[result.Count - 1])
            {
                Entry swap = bag[0]; bag[0] = bag[1]; bag[1] = swap;
            }
            foreach (Entry entry in bag)
            {
                result.Add(entry);
                if (result.Count >= count) break;
            }
        }
        return result;
    }
}
