using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameModeUISetup
{
    private const string ApplyRequest = "Temp/GameModeUIUpgrade.request";

    [InitializeOnLoadMethod]
    private static void WatchRequestedUpgrade()
    {
        if (Application.isBatchMode || !System.IO.File.Exists(ApplyRequest)) return;
        EditorApplication.update -= ApplyRequestedUpgrade;
        EditorApplication.update += ApplyRequestedUpgrade;
    }

    private static void ApplyRequestedUpgrade()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath) return;
        EditorApplication.update -= ApplyRequestedUpgrade;
        if (!System.IO.File.Exists(ApplyRequest)) return;
        // Apply to the live scene, retaining the user's unsaved edits instead of replacing its file.
        string backup = "Assets/Editor/GameModeBeforeUI-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        try
        {
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new System.Exception("Could not back up the current GameMode scene.");
            Apply();
            if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Could not save the updated GameMode scene.");
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/game-ui-live-apply.txt", "GAME_UI_LIVE_APPLIED\nBackup: " + backup + "\nApplied: " + System.DateTime.Now);
            System.IO.File.Delete(ApplyRequest);
            Debug.Log("GameMode UI applied; previous unsaved scene retained at " + backup);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/game-ui-live-apply.txt", "GAME_UI_APPLY_FAILED\n" + exception);
        }
    }

    [MenuItem("Tools/mocopi/Apply GameMode UI and Loop Setup")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath)
            throw new System.InvalidOperationException("Open GameMode before applying this setup.");
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var originalRoots = scene.GetRootGameObjects();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Improve GameMode UI and loop");
        foreach (var existing in originalRoots) Undo.RegisterFullObjectHierarchyUndo(existing, "Improve GameMode UI and loop");
        var settings = new SerializedObject(loop);
        // Keep authored prototypes available, but remove their unwired/duplicate touch targets from play.
        foreach (var old in Object.FindObjectsByType<TouchableUIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            old.gameObject.SetActive(false);
        var previous = GameObject.Find("UIPanels");
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        foreach (var old in scene.GetRootGameObjects())
            if (old.name == "UI_Pnanels" || old.name == "ModeSelectionPanels") old.SetActive(false);

        // These fields previously pointed into prefab assets, so the second lane never updated.
        var players = settings.FindProperty("players");
        var firstCamera = loop.Players[0].PlayerCamera;
        for (int i = 0; i < players.arraySize; i++)
        {
            var slot = players.GetArrayElementAtIndex(i);
            var playerRoot = (GameObject)slot.FindPropertyRelative("avatarRoots").GetArrayElementAtIndex(0).objectReferenceValue;
            var camera = (Camera)slot.FindPropertyRelative("playerCamera").objectReferenceValue;
            if (camera == null || EditorUtility.IsPersistent(camera))
            {
                var clone = Object.Instantiate(firstCamera.gameObject);
                clone.name = $"{i + 1}P_Camera";
                camera = clone.GetComponent<Camera>();
                camera.transform.position = firstCamera.transform.position + Vector3.right * 7 * i;
                slot.FindPropertyRelative("playerCamera").objectReferenceValue = camera;
            }
            camera.nearClipPlane = 0.08f;
            var manager = (WallManager)slot.FindPropertyRelative("wallManager").objectReferenceValue;
            if (manager == null || EditorUtility.IsPersistent(manager))
            {
                manager = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/prefab/{i + 1}P_Prefab/{i + 1}P_WallManager.prefab"))).GetComponent<WallManager>();
                slot.FindPropertyRelative("wallManager").objectReferenceValue = manager;
            }
            var wall = new SerializedObject(manager);
            var spawn = manager.transform.Find($"{i + 1}P_WallSpawn");
            if (spawn == null) spawn = new GameObject($"{i + 1}P_WallSpawn").transform;
            spawn.SetParent(manager.transform);
            spawn.position = new Vector3(playerRoot.transform.position.x, 1.3f, 12);
            wall.FindProperty("spawnPoint").objectReferenceValue = spawn;
            wall.FindProperty("moveSpeed").floatValue = 3f;
            wall.FindProperty("spawnInterval").floatValue = 4.2f;
            wall.FindProperty("destroyDistance").floatValue = playerRoot.transform.position.z - 1.5f;
            wall.FindProperty("playOnStart").boolValue = false;
            wall.FindProperty("controlledByGameLoop").boolValue = true;
            wall.ApplyModifiedPropertiesWithoutUndo();
            var avatarSpawn = GameObject.Find($"{i + 1}P_AvatarSpawn")?.transform;
            if (avatarSpawn == null) avatarSpawn = new GameObject($"{i + 1}P_AvatarSpawn").transform;
            avatarSpawn.SetPositionAndRotation(playerRoot.transform.position, playerRoot.transform.rotation);
            slot.FindPropertyRelative("avatarSpawnPoint").objectReferenceValue = avatarSpawn;
            if (camera.TryGetComponent<AudioListener>(out var listener)) listener.enabled = i == 0;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!loop.Players.Any(p => p.PlayerCamera == camera)) camera.gameObject.SetActive(false);

        var root = new GameObject("UIPanels");
        var view = root.AddComponent<GameModePresentation>();
        view.controller = loop;
        var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(root.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        GameModePresentation.ConfigureCanvas(scaler);
        var hud = canvasObject.transform;
        var header = Rect("Header", hud, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32, -122), new Vector2(-32, -28));
        Fill(header, new Color(0.025f, 0.065f, 0.105f, 0.96f));
        view.scoreLabel = Label("Score", header, new Vector2(0, 0), new Vector2(0.56f, 1), new Vector2(26, 10), new Vector2(0, -10), 32, TextAnchor.MiddleLeft);
        view.modeLabel = Label("Mode", header, new Vector2(0.56f, 0.5f), Vector2.one, Vector2.zero, new Vector2(-26, -8), 17, TextAnchor.MiddleRight);
        view.stepsLabel = Label("Steps", header, new Vector2(0.32f, 0), new Vector2(1, 0.55f), Vector2.zero, new Vector2(-26, 0), 18, TextAnchor.MiddleRight);
        var track = Rect("RoundProgress", header, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 4));
        Fill(track, new Color(0.13f, 0.22f, 0.3f));
        var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        view.progressFill = Fill(fill, new Color(0.43f, 0.95f, 0.83f));
        // A generated white sprite lets Image.Type.Filled render an exact progress fraction.
        view.progressFill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        view.progressFill.type = Image.Type.Filled; view.progressFill.fillMethod = Image.FillMethod.Horizontal; view.progressFill.fillAmount = 0;
        var card = Rect("StateCard", hud, new Vector2(0.5f, 0.67f), new Vector2(0.5f, 0.67f), new Vector2(-350, -120), new Vector2(350, 120));
        Fill(card, new Color(0.025f, 0.065f, 0.105f, 0.94f));
        view.informationCard = card.gameObject;
        view.titleLabel = Label("Title", card, new Vector2(0, 0.64f), Vector2.one, new Vector2(24, 0), new Vector2(-24, -10), 34, TextAnchor.MiddleCenter);
        view.messageLabel = Label("Message", card, Vector2.zero, new Vector2(1, 0.64f), new Vector2(30, 16), new Vector2(-30, 0), 24, TextAnchor.MiddleCenter);
        view.resultLabel = Label("Result", card, Vector2.zero, new Vector2(1, 0.7f), new Vector2(24, 12), new Vector2(-24, -2), 24, TextAnchor.MiddleCenter);
        view.countdownLabel = Label("Countdown", hud, new Vector2(0.5f, 0.57f), new Vector2(0.5f, 0.57f), new Vector2(-500, -130), new Vector2(500, 130), 150, TextAnchor.MiddleCenter);
        view.countdownLabel.resizeTextMinSize = 48;
        var outline = view.countdownLabel.gameObject.AddComponent<Outline>(); outline.effectDistance = new Vector2(3, -3); outline.effectColor = new Color(0.02f, 0.06f, 0.1f);
        view.feedbackLabel = Label("Feedback", hud, new Vector2(0.5f, 0.79f), new Vector2(0.5f, 0.79f), new Vector2(-430, -40), new Vector2(430, 40), 34, TextAnchor.MiddleCenter);
        var footer = Rect("Footer", hud, Vector2.zero, new Vector2(1, 0), new Vector2(32, 26), new Vector2(-32, 82));
        Fill(footer, new Color(0.025f, 0.065f, 0.105f, 0.94f));
        view.footerLabel = Label("Hint", footer, Vector2.zero, Vector2.one, new Vector2(15, 0), new Vector2(-15, 0), 21, TextAnchor.MiddleCenter);

        var anchors = new List<GameModePresentation.TouchStation>();
        var ready = new List<TouchableUIPanel>();
        string[] fields = { "mocopiConnectionWaitPanel", "avatarSelectionPanel", "gameModeSelectionPanel", "readyPanel", "countdownPanel", "playingPanel", "resultPanel" };
        string[] names = { "Connection", "AvatarSelection", "ModeSelection", "Ready", "Countdown", "Playing", "Result" };
        for (int state = 0; state < fields.Length; state++)
        {
            if (state == 1) { settings.FindProperty(fields[state]).objectReferenceValue = null; continue; }
            var group = new GameObject(names[state]); group.transform.SetParent(root.transform, false);
            settings.FindProperty(fields[state]).objectReferenceValue = group;
            for (int player = 0; player < 2; player++)
            {
                var station = new GameObject($"{player + 1}P_TouchActions").transform;
                station.SetParent(group.transform, false);
                station.position = new Vector3(7 * player + 0.05f, GameModePresentation.TouchStationWorldHeight, -0.37f);
                anchors.Add(new GameModePresentation.TouchStation { root = station, playerIndex = player });
                switch (state)
                {
                    case 0: Button(station, "接続を確認", -0.41f, loop.ConfirmMocopiConnected); Button(station, "マウスで試す", 0.41f, view.TryMouseMode); break;
                    
                    case 2: Button(station, "協力プレイ", -0.41f, loop.SetCoopMode); Button(station, "対戦プレイ", 0.41f, loop.SetVersusMode); break;
                    case 3: ready.Add(Button(station, "準備 OK", -0.41f, player == 0 ? view.ReadyPlayer1 : view.ReadyPlayer2)); Button(station, "1人 / 2人 切り替え", 0.41f, view.TogglePlayers); break;
                    case 6: Button(station, "もう一度！", -0.41f, loop.Retry); Button(station, "選択に戻る", 0.41f, loop.ReturnToAvatarSelection); break;
                }
            }
            group.SetActive(state == 0);
        }
        view.stations = anchors.ToArray(); view.readyButtons = ready.ToArray();
        settings.FindProperty("stateTitleText").objectReferenceValue = view.titleLabel;
        settings.FindProperty("stateMessageText").objectReferenceValue = view.messageLabel;
        settings.FindProperty("countdownText").objectReferenceValue = view.countdownLabel;
        settings.FindProperty("resultText").objectReferenceValue = view.resultLabel;
        settings.ApplyModifiedPropertiesWithoutUndo();
        view.titleLabel.text = "mocopi を接続";
        view.messageLabel.text = "1P: MAO   /   2P: Suguru\n接続後にゲームを始めます";
        view.scoreLabel.text = "POSE / PASS";
        Canvas.ForceUpdateCanvases();
        GameModePresentation.ConfigureHud((RectTransform)hud, false);
        view.resultLabel.gameObject.SetActive(false); view.countdownLabel.gameObject.SetActive(false);
        foreach (var created in scene.GetRootGameObjects().Except(originalRoots))
            Undo.RegisterCreatedObjectUndo(created, "Improve GameMode UI and loop");
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static TouchableUIPanel Button(Transform parent, string label, float x, UnityAction action)
    {
        var go = new GameObject(label); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(Mathf.Sign(x) * 0.62f, 0, 0);
        var panel = go.AddComponent<TouchableUIPanel>();
        panel.SetLabel(label);
        panel.SetClickSound(action.Method.Name == "ReturnToAvatarSelection" ? UIActionSound.Cancel : UIActionSound.Confirm);
        panel.ConfigureVisuals(new Vector2(0.72f, 0.26f), 0.16f, 700, null, 44,
            new Color(0.035f, 0.12f, 0.18f), new Color(0.95f, 0.98f, 1), new Color(0.43f, 0.95f, 0.83f),
            true, 1, 1, true, new Color(0.4f, 0.9f, 0.84f), 1, 1, 2, Color.clear, 0, 0, 0);
        UnityEventTools.AddPersistentListener(panel.OnClick, action);
        return panel;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
        return rect;
    }
    private static Image Fill(RectTransform rect, Color color) { var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    private static Text Label(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high, int size, TextAnchor alignment)
    {
        var text = Rect(name, parent, min, max, low, high).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = new Color(0.92f, 0.97f, 1);
        text.alignment = alignment; text.raycastTarget = false; text.supportRichText = true; text.resizeTextForBestFit = true;
        text.resizeTextMinSize = size - 4; text.resizeTextMaxSize = size;
        return text;
    }
    public static void ApplyAndSave()
    {
        EditorSceneManager.OpenScene(GameModeValidation.ScenePath);
        Apply();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("GAME_UI_SETUP_PASSED");
    }
}
