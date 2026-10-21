using System;
using System.Linq;

/// <summary>Managed-only checks; can run without launching the Unity editor.</summary>
public static class LocalLeaderboardValidation
{
    public static void Run()
    {
        var board = new LocalLeaderboard();
        Check(board.GetRank("Single", 10, 8, 30) == 1, "First valid result qualifies.");
        var earlier = board.Add("Single", 10, 8, 30, "  あおい  ");
        Check(earlier.name == "あおい", "Japanese names are retained and trimmed.");
        var lowerContacts = board.Add("Single", 10, 8, 10, "みどり");
        var higherScore = board.Add("Single", 10, 9, 50, "むらさき");
        var equal = board.Add("Single", 10, 8, 30, "同点");
        Check(board.GetBoard("Single", 10).Select(e => e.id).SequenceEqual(new[] {
            higherScore.id, lowerContacts.id, earlier.id, equal.id }), "Score, contacts, then earlier entry determine order.");
        Check(board.GetRank("Single", 10, 8, 30) == 5, "Exact ties are placed after existing records.");
        Check(board.GetBoard("Coop", 20).Count == 0 && board.GetBoard("Single", 5).Count == 0,
            "Modes and round lengths are separate.");
        var team = board.Add("Coop", 20, 19, 40, "チーム");
        var versus = board.Add("Versus", 10, 10, 0, "対戦");
        var shorter = board.Add("Single", 5, 5, 0, "5問");
        for (int i = 0; i < 6; i++) board.Add("Single", 10, 1, 100 + i, "記録" + i);
        Check(board.GetBoard("Single", 10).Count == 10, "Board fills to ten records.");
        Check(board.GetRank("Single", 10, 1, 105) == 0 && board.Add("Single", 10, 0, 0, "圏外") == null,
            "A non-qualifier and exact tie at tenth place cannot displace a record.");
        string lastId = board.GetBoard("Single", 10).Last().id;
        var best = board.Add("Single", 10, 10, 0, "優勝");
        Check(board.GetBoard("Single", 10).First() == best && board.GetBoard("Single", 10).Count == 10 &&
            !board.entries.Any(e => e.id == lastId), "A new high score replaces only the lowest record.");
        Check(board.GetBoard("Coop", 20).Single() == team && board.GetBoard("Versus", 10).Single() == versus &&
            board.GetBoard("Single", 5).Single() == shorter, "Trimming does not remove other boards.");
        int count = board.entries.Count;
        Check(board.Add("Single", 10, 10, 0, " \n\t ") == null && board.entries.Count == count, "Empty names do not save.");
        Check(LocalLeaderboard.CleanName("123456789012345") == "123456789012", "Name length is capped.");
        Check(LocalLeaderboard.CleanName("あ\nお\tい") == "あおい", "Control characters cannot insert extra rows.");
        Check(LocalLeaderboard.CleanName("12345678901\U0001F600long") == "12345678901", "Truncation does not split a surrogate pair.");
        Check(board.GetRank("Single", 10, 11, 0) == 0 && board.GetRank("Single", 0, 0, 0) == 0 &&
            board.GetRank("Single", 10, 10, -1) == 0, "Invalid results cannot qualify.");
        board.entries.Add(null);
        board.entries.Add(new LocalLeaderboard.Entry { mode = "Single", totalWalls = 10, score = 999, name = "invalid" });
        Check(board.GetBoard("Single", 10).Count == 10 && board.GetBoard("Single", 10).First() == best,
            "Invalid loaded entries are ignored.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
