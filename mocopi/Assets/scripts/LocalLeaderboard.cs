using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Local top-ten boards, separated by game mode and number of walls.</summary>
[Serializable]
public sealed class LocalLeaderboard
{
    public const int Capacity = 10;
    public const int NameLimit = 12;

    [Serializable]
    public sealed class Entry
    {
        public string id;
        public string mode;
        public string name;
        public int score;
        public int contacts;
        public int totalWalls;
    }

    public List<Entry> entries = new List<Entry>();

    public List<Entry> GetBoard(string mode, int totalWalls)
    {
        return (entries ?? new List<Entry>()).Where(e => e != null && e.mode == mode &&
                e.totalWalls == totalWalls && ValidScore(e.score, e.contacts, e.totalWalls) &&
                !string.IsNullOrWhiteSpace(e.name))
            .OrderByDescending(e => e.score).ThenBy(e => e.contacts).Take(Capacity).ToList();
    }

    public int GetRank(string mode, int totalWalls, int score, int contacts)
    {
        if (!ValidScore(score, contacts, totalWalls)) return 0;
        // Identical results retain the earlier entry's position.
        int rank = 1 + GetBoard(mode, totalWalls).Count(e => e.score > score ||
            (e.score == score && e.contacts <= contacts));
        return rank <= Capacity ? rank : 0;
    }

    public Entry Add(string mode, int totalWalls, int score, int contacts, string name)
    {
        name = CleanName(name);
        if (name.Length == 0 || GetRank(mode, totalWalls, score, contacts) == 0) return null;
        var entry = new Entry { id = Guid.NewGuid().ToString("N"), mode = mode, totalWalls = totalWalls,
            score = score, contacts = contacts, name = name };
        if (entries == null) entries = new List<Entry>();
        entries.Add(entry);
        var retained = new HashSet<Entry>(GetBoard(mode, totalWalls));
        entries.RemoveAll(e => e == null || (e.mode == mode && e.totalWalls == totalWalls && !retained.Contains(e)));
        return entry;
    }

    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length <= NameLimit) return clean;
        int length = char.IsHighSurrogate(clean[NameLimit - 1]) ? NameLimit - 1 : NameLimit;
        return clean.Substring(0, length);
    }

    private static bool ValidScore(int score, int contacts, int totalWalls) =>
        totalWalls > 0 && score >= 0 && score <= totalWalls && contacts >= 0;
}
