using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD and fixed, reachable touch menus for the local game loop.</summary>
[DefaultExecutionOrder(1100)]
public class GameModePresentation : MonoBehaviour
{
    public const float TouchStationWorldHeight = 2.2f;
    [Serializable]
    public class TouchStation
    {
        public Transform root;
        public int playerIndex;
    }

    public MocopiLocalGameLoopController controller;
    public TouchStation[] stations = Array.Empty<TouchStation>();
    public Text modeLabel, stepsLabel, scoreLabel, feedbackLabel, footerLabel;
    public Text titleLabel, messageLabel, countdownLabel, resultLabel;
    public Image progressFill;
    public GameObject informationCard;
    public TouchableUIPanel[] readyButtons = Array.Empty<TouchableUIPanel>();
    private float anchorUntil;
    private PlayerHud[] playerHuds;
    private sealed class PlayerHud
    {
        public RectTransform root;
        public Text score, mode, steps, title, message, result, countdown, feedback, footer;
        public Image progress;
        public GameObject card;
        public int completed, failures;
        public float feedbackUntil;
        public PlayerHud(RectTransform viewport)
        {
            root = viewport;
            score = TextAt("Header/Score"); mode = TextAt("Header/Mode"); steps = TextAt("Header/Steps");
            title = TextAt("StateCard/Title"); message = TextAt("StateCard/Message"); result = TextAt("StateCard/Result");
            countdown = TextAt("Countdown"); feedback = TextAt("Feedback"); footer = TextAt("Footer/Hint");
            card = root.Find("StateCard").gameObject;
            progress = root.Find("Header/RoundProgress/Fill").GetComponent<Image>();
        }
        private Text TextAt(string path) => root.Find(path).GetComponent<Text>();
    }
    private Vector3[] playCameraPositions;
    private Quaternion[] playCameraRotations;

    private void Awake()
    {
        foreach (var station in stations)
        {
            Vector3 position = station.root.position;
            position.y = TouchStationWorldHeight;
            station.root.position = position;
        }
        var canvas = modeLabel.canvas.transform;
        var children = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in canvas) children.Add((RectTransform)child);
        var viewport = (RectTransform)new GameObject("1P_HUD", typeof(RectTransform), typeof(RectMask2D)).transform;
        viewport.gameObject.layer = 5;
        viewport.SetParent(canvas, false);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        foreach (var child in children)
        {
            Vector2 position = child.anchoredPosition, size = child.sizeDelta;
            child.SetParent(viewport, false);
            child.anchoredPosition = position; child.sizeDelta = size;
        }
        var second = Instantiate(viewport, canvas, false);
        second.name = "2P_HUD";
        playerHuds = new[] { new PlayerHud(viewport), new PlayerHud(second) };
        // Keep the full Japanese start cue on one line, including previously generated HUDs.
        countdownLabel.rectTransform.sizeDelta = new Vector2(1000, 260);
        countdownLabel.resizeTextMinSize = 48;
        countdownLabel.resizeTextMaxSize = 150;
        playCameraPositions = new Vector3[controller.Players.Count];
        playCameraRotations = new Quaternion[controller.Players.Count];
        for (int i = 0; i < controller.Players.Count; i++)
        {
            playCameraPositions[i] = controller.Players[i].PlayerCamera.transform.position;
            playCameraRotations[i] = controller.Players[i].PlayerCamera.transform.rotation;
        }
        // OS fallback keeps Japanese readable in the Windows player without depending on Arial.
        var font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP" }, 32);
        if (font != null)
        {
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = font;
            foreach (var panel in GetComponentsInChildren<TouchableUIPanel>(true)) panel.SetFont(font);
        }
    }

    private void OnEnable()
    {
        controller.OnStateChanged.AddListener(StateChanged);
    }

    private void OnDisable() => controller.OnStateChanged.RemoveListener(StateChanged);

    private void StateChanged(MocopiLocalGameLoopController.GameLoopState state)
    {
        anchorUntil = Time.unscaledTime + 0.5f;
        foreach (var hud in playerHuds) { hud.feedbackUntil = 0; hud.completed = hud.failures = 0; }
        foreach (var button in readyButtons)
            button.SetLabel(controller.DebugModeOn ? "スタート" : "準備 OK");
        informationCard.SetActive(state != MocopiLocalGameLoopController.GameLoopState.Playing &&
                                  state != MocopiLocalGameLoopController.GameLoopState.Countdown);
        countdownLabel.gameObject.SetActive(state == MocopiLocalGameLoopController.GameLoopState.Countdown);
        resultLabel.gameObject.SetActive(state == MocopiLocalGameLoopController.GameLoopState.Result);
        messageLabel.gameObject.SetActive(state != MocopiLocalGameLoopController.GameLoopState.Result);
    }

    private void LateUpdate()
    {
        var state = controller.CurrentState;
        for (int i = 0; i < controller.ActivePlayerCount; i++)
        {
            var player = controller.Players[i];
            var animator = player.ReadyPoseAnimator;
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var camera = player.PlayerCamera;
            Vector3 position = playCameraPositions[i];
            Quaternion rotation = playCameraRotations[i];
            if (head != null && state != MocopiLocalGameLoopController.GameLoopState.Playing && state != MocopiLocalGameLoopController.GameLoopState.Countdown)
            {
                Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
                position = head.position - forward * (controller.ActivePlayerCount == 1 ? 1.8f : 2.3f) + Vector3.up * 0.15f;
                rotation = Quaternion.LookRotation(head.position - Vector3.up * 0.15f - position, Vector3.up);
            }
            float blend = 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(camera.transform.position, position, blend), Quaternion.Slerp(camera.transform.rotation, rotation, blend));
        }
        foreach (var station in stations)
        {
            bool active = station.playerIndex < controller.ActivePlayerCount;
            station.root.gameObject.SetActive(active);
            if (!active || !station.root.gameObject.activeInHierarchy || Time.unscaledTime > anchorUntil) continue;
            var player = controller.Players[station.playerIndex];
            var camera = player.PlayerCamera;
            if (camera == null) continue;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
            var animator = player.ReadyPoseAnimator;
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 origin = head != null ? head.position : camera.transform.position;
            Vector3 stationPosition = origin + forward * 0.35f;
            stationPosition.y = TouchStationWorldHeight;
            station.root.SetPositionAndRotation(stationPosition,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        string mode = controller.CurrentMode == MocopiLocalGameLoopController.GameMode.Single ? "SOLO" :
            controller.CurrentMode == MocopiLocalGameLoopController.GameMode.Coop ? "CO-OP" : "VERSUS";
        modeLabel.text = mode + (controller.DebugModeOn ? "   /   MOUSE MODE" : "   /   MOCOPI");
        int step = state <= MocopiLocalGameLoopController.GameLoopState.GameModeSelection ? 0 :
            state <= MocopiLocalGameLoopController.GameLoopState.Countdown ? 1 :
            state == MocopiLocalGameLoopController.GameLoopState.Playing ? 2 : 3;
        string[] steps = { "01  SELECT", "02  READY", "03  PLAY", "04  RESULT" };
        steps[step] = "<color=#6EF2DA>" + steps[step] + "</color>";
        stepsLabel.text = string.Join("     /     ", steps);
        footerLabel.text = state == MocopiLocalGameLoopController.GameLoopState.Playing
            ? "壁の穴にポーズを合わせよう  ·  ぶつかったブロックが 3 個以内ならクリア"
            : "手で少し触れ続けると決定  ·  マウスでもクリックできます";

        if (state == MocopiLocalGameLoopController.GameLoopState.Ready)
        {
            messageLabel.text = controller.DebugModeOn ? "準備ができたら「スタート」\n5 秒のカウントダウン後に始まります" : "頭の上で両手を合わせて、2 秒キープ\nまたは、それぞれの「準備 OK」にタッチ";
            for (int i = 0; i < controller.ActivePlayerCount; i++)
                if (controller.GetReadyProgress(i) > 0)
                    messageLabel.text += $"\n{i + 1}P  {Mathf.RoundToInt(controller.GetReadyProgress(i) * 100)}%";
        }
        UpdatePlayerHuds(state);
    }

    private void UpdatePlayerHuds(MocopiLocalGameLoopController.GameLoopState state)
    {
        bool split = controller.ActivePlayerCount > 1;
        bool playing = state == MocopiLocalGameLoopController.GameLoopState.Playing;
        for (int i = 0; i < playerHuds.Length; i++)
        {
            var hud = playerHuds[i];
            hud.root.gameObject.SetActive(i < controller.ActivePlayerCount);
            if (i >= controller.ActivePlayerCount) continue;
            Rect viewport = controller.Players[i].PlayerCamera.rect;
            hud.root.anchorMin = viewport.min; hud.root.anchorMax = viewport.max;
            hud.root.offsetMin = hud.root.offsetMax = Vector2.zero;
            if (i > 0)
            {
                Copy(titleLabel, hud.title); Copy(messageLabel, hud.message); Copy(resultLabel, hud.result);
                Copy(countdownLabel, hud.countdown); Copy(modeLabel, hud.mode); Copy(stepsLabel, hud.steps); Copy(footerLabel, hud.footer);
                hud.card.SetActive(informationCard.activeSelf);
            }
            float width = hud.root.rect.width;
            hud.countdown.rectTransform.sizeDelta = new Vector2(Mathf.Max(200, width - 64), 260);
            hud.countdown.resizeTextMinSize = 48;
            var stepsRect = hud.steps.rectTransform;
            stepsRect.anchorMin = new Vector2(split ? 0 : 0.32f, 0);
            stepsRect.anchorMax = new Vector2(1, split ? 0.35f : 0.55f);
            hud.steps.fontSize = hud.steps.resizeTextMaxSize = split ? 12 : 18;
            hud.steps.resizeTextMinSize = split ? 10 : 14;
            hud.score.fontSize = hud.score.resizeTextMaxSize = split ? 24 : 32;
            hud.score.resizeTextMinSize = split ? 18 : 28;
            hud.score.rectTransform.anchorMin = new Vector2(0, split ? 0.3f : 0);
            hud.footer.resizeTextMinSize = split ? 13 : 17;
            hud.footer.resizeTextMaxSize = split ? 17 : 21;
            var wall = controller.Players[i].WallManager;
            int completed = wall.GetCompletedWallCount(), failures = wall.GetFailedWallCount(), total = wall.GetWallCount();
            hud.score.text = playing || state == MocopiLocalGameLoopController.GameLoopState.Result
                ? $"{i + 1}P   {wall.GetScore():00} / {total:00}  <size=16>POINTS</size>" : $"{i + 1}P  /  POSE PASS";
            hud.progress.fillAmount = total > 0 ? (float)completed / total : 0;
            if (playing && completed > hud.completed)
            {
                hud.feedback.text = failures > hud.failures ? "KEEP GOING" : "CLEAR!";
                hud.feedback.color = failures > hud.failures ? new Color(1, 0.76f, 0.45f) : new Color(0.43f, 0.95f, 0.8f);
                hud.feedbackUntil = Time.unscaledTime + 1.1f;
            }
            hud.completed = completed; hud.failures = failures;
            if (playing && Time.unscaledTime >= hud.feedbackUntil)
            {
                hud.feedback.text = $"{completed:00} / {total:00}  WALLS";
                hud.feedback.color = Color.white;
            }
            hud.feedback.gameObject.SetActive(playing);
        }
    }
    private static void Copy(Text source, Text target)
    {
        target.text = source.text;
        target.gameObject.SetActive(source.gameObject.activeSelf);
    }

    public void ReadyPlayer1() { if (controller.DebugModeOn) controller.StartDebugRound(); else controller.MarkPlayer1Ready(); }
    public void ReadyPlayer2() { if (controller.DebugModeOn) controller.StartDebugRound(); else controller.MarkPlayer2Ready(); }
    public void TogglePlayers() => controller.SetGameMode(controller.ActivePlayerCount == 1
        ? MocopiLocalGameLoopController.GameMode.Coop : MocopiLocalGameLoopController.GameMode.Single);
    public void TryMouseMode() => controller.SetDebugMode(true);
}
