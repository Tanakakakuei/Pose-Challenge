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
    private ResultLeaderboardPanel leaderboardPanel;
    private bool wasEnteringName;
    private Rect[] resultCameraRects;
    private bool resultViewportActive;
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
        ConfigureCanvas(canvas.GetComponent<CanvasScaler>());
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
        leaderboardPanel = gameObject.AddComponent<ResultLeaderboardPanel>();
        leaderboardPanel.Initialize(controller, canvas, font != null ? font : modeLabel.font);
    }

    private void OnEnable()
    {
        controller.OnStateChanged.AddListener(StateChanged);
    }

    private void OnDisable() => controller.OnStateChanged.RemoveListener(StateChanged);

    private void StateChanged(MocopiLocalGameLoopController.GameLoopState state)
    {
        UpdateResultViewports(state == MocopiLocalGameLoopController.GameLoopState.Result);
        anchorUntil = Time.unscaledTime + 0.5f;
        foreach (var hud in playerHuds) { hud.feedbackUntil = 0; hud.completed = hud.failures = 0; }
        foreach (var button in readyButtons)
            button.SetLabel(controller.DebugModeOn ? "スタート" : "準備 OK");
        informationCard.SetActive(state != MocopiLocalGameLoopController.GameLoopState.Playing &&
                                  state != MocopiLocalGameLoopController.GameLoopState.Countdown);
        countdownLabel.gameObject.SetActive(state == MocopiLocalGameLoopController.GameLoopState.Countdown);
        resultLabel.gameObject.SetActive(state == MocopiLocalGameLoopController.GameLoopState.Result);
        messageLabel.gameObject.SetActive(state != MocopiLocalGameLoopController.GameLoopState.Result);
        leaderboardPanel.SetState(state);
    }

    private void LateUpdate()
    {
        var state = controller.CurrentState;
        if (wasEnteringName && !leaderboardPanel.IsEnteringName) anchorUntil = Time.unscaledTime + 0.5f;
        wasEnteringName = leaderboardPanel.IsEnteringName;
        for (int i = 0; i < controller.ViewportCount; i++)
        {
            if (controller.UsesSharedCoopView)
            {
                if (i == 0) UpdateSharedCamera();
                continue;
            }
            var player = controller.Players[i];
            var animator = player.ReadyPoseAnimator;
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var camera = player.PlayerCamera;
            Vector3 position = playCameraPositions[i];
            Quaternion rotation = playCameraRotations[i];
            if (head != null && state != MocopiLocalGameLoopController.GameLoopState.Playing && state != MocopiLocalGameLoopController.GameLoopState.Countdown)
            {
                Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
                // Keep the physical touch targets reachable; widen the camera framing in narrow windows.
                float halfFov = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                float distance = Mathf.Max(controller.ViewportCount == 1 ? 1.8f : 2.3f,
                    1.12f / (halfFov * Mathf.Max(0.2f, camera.aspect) * 0.86f) - 0.35f);
                position = head.position - forward * distance;
                // Reserve the upper band for instructions regardless of avatar height or viewport width.
                position.y = TouchStationWorldHeight - (distance + 0.35f) * halfFov * 0.2f;
                rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            float blend = 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime);
            camera.transform.SetPositionAndRotation(Vector3.Lerp(camera.transform.position, position, blend), Quaternion.Slerp(camera.transform.rotation, rotation, blend));
        }
        foreach (var station in stations)
        {
            // Keep physical retry/menu controls, but prevent stray touches during keyboard entry.
            bool active = station.playerIndex < (controller.UsesSharedCoopView ? controller.ActivePlayerCount : controller.ViewportCount) &&
                !leaderboardPanel.IsEnteringName;
            station.root.gameObject.SetActive(active);
            if (!active || !station.root.gameObject.activeInHierarchy || (Time.unscaledTime > anchorUntil && !controller.UsesSharedCoopView)) continue;
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
        if (state <= MocopiLocalGameLoopController.GameLoopState.GameModeSelection)
            mode = controller.ActivePlayerCount == 1 ? "SOLO" : "2P / SELECT MODE";
        modeLabel.text = mode + (controller.DebugModeOn ? "   /   MOUSE MODE" : "   /   MOCOPI");
        int step = state <= MocopiLocalGameLoopController.GameLoopState.GameModeSelection ? 0 :
            state <= MocopiLocalGameLoopController.GameLoopState.Countdown ? 1 :
            state == MocopiLocalGameLoopController.GameLoopState.Playing ? 2 : 3;
        string[] steps = { "01  SELECT", "02  READY", "03  PLAY", "04  RESULT" };
        steps[step] = "<color=#6EF2DA>" + steps[step] + "</color>";
        stepsLabel.text = string.Join("     /     ", steps);
        footerLabel.text = state == MocopiLocalGameLoopController.GameLoopState.Playing
            ? $"壁の穴にポーズを合わせよう  ·  ぶつかったブロックが {controller.Players[0].WallManager.GetSafeHitThreshold()} 個以内ならクリア"
            : state == MocopiLocalGameLoopController.GameLoopState.Result
            ? (leaderboardPanel.IsEnteringName ? "ランクイン！ キーボードで名前を入力し「登録」 · スキップもできます" : "もう一度挑戦するか、アバター選択に戻れます")
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
        bool shared = controller.UsesSharedCoopView;
        bool split = controller.UsesSplitScreenView;
        bool playing = state == MocopiLocalGameLoopController.GameLoopState.Playing;
        for (int i = 0; i < playerHuds.Length; i++)
        {
            var hud = playerHuds[i];
            hud.root.gameObject.SetActive(i < controller.ViewportCount);
            if (i >= controller.ViewportCount) continue;
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
            ConfigureHud(hud.root, state == MocopiLocalGameLoopController.GameLoopState.Result);
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
            if (shared)
            {
                var other = controller.Players[1].WallManager;
                hud.score.text = playing || state == MocopiLocalGameLoopController.GameLoopState.Result
                    ? $"1P {wall.GetScore():00}/{total:00}   2P {other.GetScore():00}/{other.GetWallCount():00}"
                    : "1P + 2P  /  CO-OP";
                completed += other.GetCompletedWallCount();
                failures += other.GetFailedWallCount();
                total += other.GetWallCount();
            }
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

    private void UpdateSharedCamera()
    {
        var camera = controller.Players[0].PlayerCamera;
        var arenas = controller.GetComponent<PlayerArenaIsolation>();
        Vector3 center = arenas.SharedCenter;
        // Frame both six metre lanes, their full height and the common floor ring.
        float halfFov = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float distance = Mathf.Max(12.5f, 6.8f / (halfFov * Mathf.Max(0.2f, camera.aspect)));
        Vector3 target = center + new Vector3(0, 0.9f, 1f);
        camera.transform.SetPositionAndRotation(target + new Vector3(0, distance * 0.28f, -distance),
            Quaternion.LookRotation(new Vector3(0, -0.28f, 1), Vector3.up));
    }

    public static void ConfigureCanvas(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1; // Stable vertical spacing at Full HD, 720p and taller windows.
    }

    public static void ConfigureHud(RectTransform root, bool result)
    {
        float width = root.rect.width, height = root.rect.height;
        float margin = Mathf.Min(28, width * 0.035f);
        var header = (RectTransform)root.Find("Header");
        header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
        header.offsetMin = new Vector2(margin, -Mathf.Min(124, height * 0.19f));
        header.offsetMax = new Vector2(-margin, -20);
        var card = (RectTransform)root.Find("StateCard");
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1);
        card.pivot = new Vector2(0.5f, 1);
        card.anchoredPosition = new Vector2(0, result ? -height * 0.48f : -Mathf.Min(144, height * 0.22f));
        card.sizeDelta = new Vector2(Mathf.Min(800, width - margin * 2), Mathf.Min(result ? 240 : 176, height * 0.28f));
        var footer = (RectTransform)root.Find("Footer");
        footer.anchorMin = Vector2.zero; footer.anchorMax = Vector2.right;
        footer.offsetMin = new Vector2(margin, 20);
        footer.offsetMax = new Vector2(-margin, Mathf.Min(82, height * 0.14f));
        var feedback = (RectTransform)root.Find("Feedback");
        feedback.anchorMin = feedback.anchorMax = new Vector2(0.5f, 0.78f);
        feedback.anchoredPosition = Vector2.zero;
        feedback.sizeDelta = new Vector2(width - margin * 2, 80);
    }

    private void UpdateResultViewports(bool result)
    {
        if (result == resultViewportActive) return;
        if (result)
        {
            resultCameraRects = new Rect[controller.Players.Count];
            for (int i = 0; i < controller.Players.Count; i++)
            {
                var camera = controller.Players[i].PlayerCamera;
                resultCameraRects[i] = camera.rect;
                Rect rect = camera.rect; rect.x *= 0.63f; rect.width *= 0.63f;
                camera.rect = rect;
            }
        }
        else
        {
            for (int i = 0; i < controller.Players.Count; i++)
            {
                Rect shrunk = resultCameraRects[i]; shrunk.x *= 0.63f; shrunk.width *= 0.63f;
                var camera = controller.Players[i].PlayerCamera;
                if (camera.rect == shrunk) camera.rect = resultCameraRects[i];
            }
        }
        resultViewportActive = result;
    }

    public void ReadyPlayer1() { if (controller.DebugModeOn) controller.StartDebugRound(); else controller.MarkPlayer1Ready(); }
    public void ReadyPlayer2() { if (controller.DebugModeOn) controller.StartDebugRound(); else controller.MarkPlayer2Ready(); }
    public void TogglePlayers() => controller.SetPlayerCount(controller.ActivePlayerCount == 1 ? 2 : 1);
    public void TryMouseMode() => controller.SetDebugMode(true);
}
