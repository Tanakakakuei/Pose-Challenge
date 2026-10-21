using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Result-only leaderboard and optional keyboard name entry for qualifying results.</summary>
public sealed class ResultLeaderboardPanel : MonoBehaviour
{
    private const string SaveKey = "PosePass.LocalLeaderboard.v1";
    private static ResultLeaderboardPanel activePanel;
    private static int consumedPointerFrame = -1;
    private static readonly Color Accent = new Color(0.43f, 0.95f, 0.83f);
    private MocopiLocalGameLoopController controller;
    private LocalLeaderboard leaderboard;
    private readonly Queue<Candidate> candidates = new Queue<Candidate>();
    private readonly HashSet<string> newEntries = new HashSet<string>();
    private Candidate current;
    private string mode;
    private int totalWalls;
    private Font font;
    private GameObject card, entryArea, navigation, sidebarBackground;
    private Text modeLabel, status, emptyLabel;
    private InputField nameInput;
    private readonly List<Text[]> rows = new List<Text[]>();
    private bool resultOpen;

    private sealed class Candidate
    {
        public string label;
        public int score, contacts;
    }

    public bool IsEnteringName => current != null && resultOpen;

    public static bool BlocksWorldPointer(Vector2 position)
    {
        if (consumedPointerFrame == Time.frameCount) return true;
        if (activePanel == null) return false;
        return Contains(activePanel.card, position) || Contains(activePanel.navigation, position);
    }

    private static bool Contains(GameObject target, Vector2 position) => target != null && target.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)target.transform, position);

    private void OnDestroy() { if (activePanel == this) activePanel = null; }

    public void Initialize(MocopiLocalGameLoopController gameController, Transform canvas, Font labelFont)
    {
        controller = gameController;
        activePanel = this;
        font = labelFont;
        try
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            leaderboard = string.IsNullOrEmpty(json) ? new LocalLeaderboard() : JsonUtility.FromJson<LocalLeaderboard>(json);
        }
        catch (Exception error)
        {
            Debug.LogWarning("Leaderboard could not be loaded: " + error.Message);
        }
        if (leaderboard == null) leaderboard = new LocalLeaderboard();

        if (!canvas.TryGetComponent<GraphicRaycaster>(out _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("Result UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }

        var sidebar = Rect("ResultSidebar", canvas, new Vector2(0.63f, 0), Vector2.one);
        sidebarBackground = sidebar.gameObject;
        Fill(sidebar, new Color(0.015f, 0.035f, 0.06f, 1));
        var panel = Rect("ResultLeaderboard", canvas, new Vector2(0.65f, 0.13f), new Vector2(0.98f, 0.84f));
        card = panel.gameObject;
        Fill(panel, new Color(0.025f, 0.065f, 0.105f, 0.98f));
        var stripe = Rect("Accent", panel, new Vector2(0, 0.992f), Vector2.one);
        Fill(stripe, Accent);
        Label("Title", panel, "ランキング  TOP 10", 27, new Vector2(0.05f, 0.89f), new Vector2(0.95f, 0.98f));
        modeLabel = Label("Mode", panel, "", 16, new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.89f));
        Label("Rule", panel, "得点順 / 同点は接触が少ない順", 14, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.83f));
        AddRow(panel, 0.725f, new[] { "順位", "名前", "得点", "接触" }, 15);
        for (int i = 0; i < LocalLeaderboard.Capacity; i++)
            rows.Add(AddRow(panel, 0.68f - i * 0.043f, new[] { (i + 1).ToString("00"), "—", "—", "—" }, 18));
        emptyLabel = Label("Empty", panel, "まだ記録がありません", 20, new Vector2(0.08f, 0.49f), new Vector2(0.92f, 0.60f));

        var entry = Rect("NameEntry", panel, new Vector2(0.05f, 0.035f), new Vector2(0.95f, 0.275f));
        entryArea = entry.gameObject;
        status = Label("Status", panel, "", 18, new Vector2(0.05f, 0.215f), new Vector2(0.95f, 0.285f));
        var field = Rect("Name", entry, new Vector2(0, 0.35f), new Vector2(1, 0.70f));
        var fieldImage = Fill(field, new Color(0.10f, 0.19f, 0.27f));
        nameInput = field.gameObject.AddComponent<InputField>();
        nameInput.targetGraphic = fieldImage;
        nameInput.textComponent = Label("Text", field, "", 21, new Vector2(0.04f, 0), new Vector2(0.96f, 1));
        nameInput.textComponent.resizeTextForBestFit = false;
        nameInput.textComponent.supportRichText = false;
        var placeholder = Label("Placeholder", field, "名前を入力（12文字まで）", 19, new Vector2(0.04f, 0), new Vector2(0.96f, 1));
        placeholder.color = new Color(0.62f, 0.72f, 0.80f);
        nameInput.placeholder = placeholder;
        nameInput.characterLimit = LocalLeaderboard.NameLimit;
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.contentType = InputField.ContentType.Standard;
        nameInput.customCaretColor = true;
        nameInput.caretColor = Accent;
        // Explicit registration avoids treating the Enter used for Japanese IME conversion as submit.
        Button("Register", entry, "登録", new Vector2(0, 0), new Vector2(0.48f, 0.27f), Submit);
        Button("Skip", entry, "登録しない", new Vector2(0.52f, 0), new Vector2(1, 0.27f), Skip);

        var actions = Rect("ResultActions", canvas, new Vector2(0.06f, 0.15f), new Vector2(0.59f, 0.23f));
        navigation = actions.gameObject;
        Button("Retry", actions, "もう一度！", Vector2.zero, new Vector2(0.47f, 1), controller.Retry);
        Button("Menu", actions, "選択に戻る", new Vector2(0.53f, 0), Vector2.one, controller.ReturnToAvatarSelection);
        card.SetActive(false);
        sidebarBackground.SetActive(false);
        navigation.SetActive(false);
    }

    public void SetState(MocopiLocalGameLoopController.GameLoopState state)
    {
        bool show = state == MocopiLocalGameLoopController.GameLoopState.Result;
        card.SetActive(show);
        sidebarBackground.SetActive(show);
        if (!show)
        {
            resultOpen = false;
            current = null;
            candidates.Clear();
            nameInput.DeactivateInputField();
            navigation.SetActive(false);
            return;
        }
        if (resultOpen) return; // Duplicate state notifications must not register a round twice.
        resultOpen = true;
        mode = controller.CurrentMode.ToString();
        newEntries.Clear();
        var first = controller.Players[0].LastResult;
        totalWalls = first.TotalWalls;
        if (controller.CurrentMode == MocopiLocalGameLoopController.GameMode.Coop)
        {
            var second = controller.Players[1].LastResult;
            totalWalls += second.TotalWalls;
            candidates.Enqueue(new Candidate { label = "チーム", score = first.Score + second.Score,
                contacts = first.TotalContacts + second.TotalContacts });
        }
        else
        {
            // Process stronger results first so a lower result is never prompted then displaced by this round.
            foreach (var candidate in controller.Players.Take(controller.ActivePlayerCount)
                .Select((p, i) => new Candidate { label = (i + 1) + "P", score = p.LastResult.Score, contacts = p.LastResult.TotalContacts })
                .OrderByDescending(c => c.score).ThenBy(c => c.contacts)) candidates.Enqueue(candidate);
        }
        string label = controller.CurrentMode == MocopiLocalGameLoopController.GameMode.Single ? "1人プレイ" :
            controller.CurrentMode == MocopiLocalGameLoopController.GameMode.Coop ? "協力プレイ・チーム記録" : "対戦プレイ・個人記録";
        modeLabel.text = $"{label} / {totalWalls}問 / このPC";
        RefreshRows();
        NextCandidate();
    }

    private void NextCandidate()
    {
        current = null;
        while (candidates.Count > 0)
        {
            var candidate = candidates.Dequeue();
            int rank = leaderboard.GetRank(mode, totalWalls, candidate.score, candidate.contacts);
            if (rank == 0) continue;
            current = candidate;
            status.text = $"{candidate.label}  {rank}位！ 名前を入力して「登録」";
            break;
        }
        entryArea.SetActive(current != null);
        navigation.SetActive(current == null);
        if (current == null)
        {
            nameInput.DeactivateInputField();
            status.text = newEntries.Count > 0 ? "記録を保存しました！" : "次の挑戦もお待ちしています！";
            return;
        }
        nameInput.text = string.Empty;
        nameInput.Select();
        nameInput.ActivateInputField();
    }

    private void Submit()
    {
        if (!IsEnteringName) return;
        string name = LocalLeaderboard.CleanName(nameInput.text);
        if (name.Length == 0) { status.text = "名前を入力してください（12文字まで）"; nameInput.ActivateInputField(); return; }
        string oldJson = JsonUtility.ToJson(leaderboard);
        var updated = JsonUtility.FromJson<LocalLeaderboard>(oldJson);
        var entry = updated.Add(mode, totalWalls, current.score, current.contacts, name);
        if (entry == null) { NextCandidate(); return; }
        try
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(updated));
            PlayerPrefs.Save();
        }
        catch (Exception error)
        {
            PlayerPrefs.SetString(SaveKey, oldJson);
            status.text = "保存できませんでした。再試行か「登録しない」を選んでください";
            Debug.LogWarning("Leaderboard save failed: " + error.Message);
            return;
        }
        leaderboard = updated;
        newEntries.Add(entry.id);
        UIActionAudio.Play(UIActionSound.Confirm);
        RefreshRows();
        NextCandidate();
    }

    private void Skip()
    {
        if (!IsEnteringName) return;
        UIActionAudio.Play(UIActionSound.Cancel);
        NextCandidate();
    }

    private void RefreshRows()
    {
        var board = leaderboard.GetBoard(mode, totalWalls);
        emptyLabel.gameObject.SetActive(board.Count == 0);
        for (int i = 0; i < rows.Count; i++)
        {
            var entry = i < board.Count ? board[i] : null;
            rows[i][1].text = entry != null ? LocalLeaderboard.CleanName(entry.name) : "—";
            rows[i][2].text = entry != null ? entry.score.ToString() : "—";
            rows[i][3].text = entry != null ? entry.contacts.ToString() : "—";
            foreach (var label in rows[i])
            {
                label.gameObject.SetActive(board.Count > 0);
                label.color = entry != null && newEntries.Contains(entry.id) ? Accent : Color.white;
            }
        }
    }

    private Text[] AddRow(Transform parent, float bottom, string[] values, int size)
    {
        float[] left = { 0.05f, 0.17f, 0.72f, 0.85f }, right = { 0.16f, 0.70f, 0.84f, 0.98f };
        var labels = new Text[4];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = Label("Column" + i, parent, values[i], size, new Vector2(left[i], bottom), new Vector2(right[i], bottom + 0.04f));
        return labels;
    }

    private void Button(string name, Transform parent, string text, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction click)
    {
        var rect = Rect(name, parent, min, max);
        var background = Fill(rect, new Color(0.10f, 0.28f, 0.32f));
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() =>
        {
            // Stop this same click reaching a world-space menu after the result UI closes.
            consumedPointerFrame = Time.frameCount;
            click();
        });
        var label = Label("Label", rect, text, 23, new Vector2(0.03f, 0), new Vector2(0.97f, 1));
        label.alignment = TextAnchor.MiddleCenter;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)) { layer = 5 }.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Fill(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; return image;
    }

    private Text Label(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
        label.font = font; label.text = value; label.fontSize = size;
        label.color = Color.white; label.supportRichText = false; label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
        label.resizeTextForBestFit = true; label.resizeTextMaxSize = size; label.resizeTextMinSize = size - 4;
        return label;
    }
}
