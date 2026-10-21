using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GameModeValidation
{
    public const string ScenePath = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/GameMode.unity";
    public static void Inspect()
    {
        EditorSceneManager.OpenScene(ScenePath);
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Debug.Log($"GAME_CAMERA {camera.name}: {camera.transform.position} {camera.transform.eulerAngles} active={camera.gameObject.activeInHierarchy}");
        foreach (var loop in Object.FindObjectsByType<MocopiLocalGameLoopController>(FindObjectsSortMode.None))
            foreach (var slot in loop.Players)
                Debug.Log($"GAME_PLAYER {slot.DisplayName}: avatar={slot.ReadyPoseAnimator?.name} head={slot.ReadyPoseAnimator?.GetBoneTransform(HumanBodyBones.Head)?.position} manager={slot.WallManager?.transform.position}");
        foreach (var panel in Object.FindObjectsByType<TouchableUIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Debug.Log($"GAME_PANEL {panel.name}: {panel.transform.position} label={panel.Label} active={panel.gameObject.activeInHierarchy}");
        foreach (var manager in Object.FindObjectsByType<WallManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(manager);
            var spawn = (Transform)so.FindProperty("spawnPoint").objectReferenceValue;
            Debug.Log($"GAME_WALL {manager.name} spawn={spawn?.position} asset={EditorUtility.IsPersistent(spawn)}");
        }
    }

    private const string RunningKey = "GameModeValidation.Running";
    private static MocopiLocalGameLoopController loop;
    private static int phase, round;
    private static double phaseAt, startedAt;
    private static bool capturedPlay;
    private static double allWallsFinishedAt;
    private static readonly System.Collections.Generic.HashSet<WallCube> authoredCubes = new System.Collections.Generic.HashSet<WallCube>();
    private static TouchableUIPanel legacyCountdown;
    private static UnityEngine.UI.Text standardCountdown;
    private static readonly System.Collections.Generic.List<string> countdownFrames = new System.Collections.Generic.List<string>();
    private static readonly System.Collections.Generic.List<double> countdownTimes = new System.Collections.Generic.List<double>();

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(RunningKey, false))
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            startedAt = phaseAt = EditorApplication.timeSinceStartup;
        }
    }

    public static void PlayTest()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 180) throw new Exception("Game loop timed out at phase " + phase);
            if (loop == null) loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
            if (loop == null) return;
            double elapsed = EditorApplication.timeSinceStartup - phaseAt;
            var state = loop.CurrentState;
            switch (phase)
            {
                case 0:
                    if (elapsed < 1) return;
                    Assert(state == MocopiLocalGameLoopController.GameLoopState.AvatarSelection, "Initial avatar selection");
                    authoredCubes.Clear();
                    foreach (var cube in Object.FindObjectsByType<WallCube>(FindObjectsInactive.Include, FindObjectsSortMode.None)) authoredCubes.Add(cube);
                    TestTouchLifecycle();
                    TestArenaIsolation();
                    TestMouseRadiusEdit();
                    Assert(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "Single active camera");
                    foreach (var player in loop.Players)
                    {
                        Assert(player.WallManager != null && !EditorUtility.IsPersistent(player.WallManager), "Scene wall manager");
                        Assert(player.PlayerCamera != null && !EditorUtility.IsPersistent(player.PlayerCamera), "Scene player camera");
                        Set(player.WallManager, "moveSpeed", 35f);
                        Set(player.WallManager, "spawnInterval", 0.3f);
                    }
                    Capture("01-selection");
                    Click("このアバターで進む");
                    loop.ConfirmAvatarSelection(); // A duplicate old touch event must not skip ready.
                    Assert(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Ready, "Duplicate selection guarded");
                    loop.StartDebugRound();
                    loop.ReturnToAvatarSelection();
                    Next(8); break;
                case 1:
                    if (elapsed < 0.8) return;
                    Capture(round == 0 ? "02-ready" : "retry-ready");
                    if (round == 3)
                    {
                        standardCountdown = (UnityEngine.UI.Text)Get(loop, "countdownText");
                        standardCountdown.text = "";
                        Set(loop, "countdownText", null);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Panels/CountDown_UI.prefab");
                        legacyCountdown = Object.Instantiate(prefab, ((GameObject)Get(loop, "countdownPanel")).transform).GetComponent<TouchableUIPanel>();
                        legacyCountdown.gameObject.SetActive(true);
                    }
                    countdownFrames.Clear(); countdownTimes.Clear();
                    if (round == 0) Time.timeScale = 0; // Countdown is independent of gameplay time scale.
                    Click("スタート");
                    Assert(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Countdown, "Start button wired");
                    ObserveCountdown();
                    Next(2); break;
                case 2:
                    if (state != MocopiLocalGameLoopController.GameLoopState.Playing)
                    {
                        Assert(loop.Players.Take(loop.ActivePlayerCount).All(p => !p.WallManager.IsGameRunning()), "Walls wait for the start cue");
                        ObserveCountdown();
                        return;
                    }
                    Assert(string.Join(",", countdownFrames) == "5,4,3,2,1,スタート！", "Complete countdown sequence");
                    for (int i = 1; i < countdownTimes.Count; i++)
                        Assert(countdownTimes[i] - countdownTimes[i - 1] >= 0.9 && countdownTimes[i] - countdownTimes[i - 1] < 1.5, "Each countdown digit lasts one real second");
                    Assert(Time.realtimeSinceStartupAsDouble - countdownTimes.Last() >= 0.7, "Start cue visible before play");
                    Debug.Log("GAME_COUNTDOWN_PASSED: 5,4,3,2,1,スタート！; real seconds; start held; round=" + round);
                    if (legacyCountdown != null)
                    {
                        Assert(legacyCountdown.Label == "スタート！", "Legacy CountDown_UI shows final cue");
                        Set(loop, "countdownText", standardCountdown);
                        Object.Destroy(legacyCountdown.gameObject);
                        Debug.Log("GAME_LEGACY_COUNTDOWN_PASSED: original CountDown_UI prefab without a Text reference.");
                    }
                    capturedPlay = false;
                    allWallsFinishedAt = -1;
                    Next(3); break;
                case 3:
                    if (loop.Players.Take(loop.ActivePlayerCount).All(p => p.WallManager.GetCompletedWallCount() == 10))
                    {
                        if (allWallsFinishedAt < 0) allWallsFinishedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
                        if (state == MocopiLocalGameLoopController.GameLoopState.Result)
                            Assert(UnityEngine.Time.realtimeSinceStartupAsDouble - allWallsFinishedAt >= 0.45,
                                "Final wall remains visible for roughly half a real second before results");
                    }
                    foreach (var cube in SpawnedCubes())
                        if (cube.OriginalWall != null)
                            Assert(PlayerArenaLayers.IsArena(cube.gameObject.layer) && cube.gameObject.layer == cube.OriginalWall.layer, "Spawned walls and detached cubes retain their arena");
                    if (!capturedPlay && elapsed > 0.4)
                    {
                        Capture(round == 0 ? "04-playing" : "split-playing-" + round);
                        capturedPlay = true;
                    }
                    if (state != MocopiLocalGameLoopController.GameLoopState.Result) return;
                    foreach (var p in loop.Players.Take(loop.ActivePlayerCount))
                        Assert(p.WallManager.GetCompletedWallCount() == 10, "Ten walls resolved");
                    Debug.Log($"GAME_ROUND_PASSED round={round} mode={loop.CurrentMode} completed=10 players={loop.ActivePlayerCount}");
                    Next(4); break;
                case 4:
                    if (elapsed < 0.8) return;
                    var ranking = Object.FindAnyObjectByType<ResultLeaderboardPanel>();
                    // Existing flow validation never writes sample names into the user's local ranking.
                    while (ranking != null && ranking.IsEnteringName)
                        ranking.GetType().GetMethod("Skip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(ranking, null);
                    if (ranking != null) { Next(10); break; }
                    goto case 10;
                case 10:
                    if (elapsed < 0.1) return;
                    Capture(round == 0 ? "05-result" : "result-" + round);
                    // Detached hit cubes now keep their own lifetime after a wall passes.
                    Assert(SpawnedCubes().All(cube => cube.IsHit && cube.transform.parent == null),
                        "Only independent hit debris may remain after result");
                    if (round == 0)
                    {
                        Time.timeScale = 0;
                        Click("もう一度！");
                        Assert(Time.timeScale == 1, "Retry unpauses");
                        Assert(loop.Players[0].WallManager.GetCompletedWallCount() == 0, "Retry resets score and progress");
                        round++; Next(1);
                    }
                    else if (round < 3)
                    {
                        Click("選択に戻る");
                        loop.SetGameMode(MocopiLocalGameLoopController.GameMode.Coop);
                        Assert(loop.ActivePlayerCount == 2, "Two players active");
                        round++; Next(5);
                    }
                    else
                    {
                        loop.Retry();
                        var wall = loop.Players[0].WallManager;
                        wall.GetType().GetMethod("SpawnWall", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(wall, null);
                        var cube = SpawnedCubes().First();
                        var hand = new GameObject("Validation hand", typeof(SphereCollider));
                        hand.tag = "Player";
                        hand.layer = cube.gameObject.layer;
                        cube.SendMessage("OnTriggerEnter", hand.GetComponent<Collider>());
                        Assert(cube.IsHit && cube.transform.parent == null, "Hit cube detached");
                        Object.Destroy(hand);
                        loop.ReturnToAvatarSelection();
                        Next(7);
                    }
                    break;
                case 5:
                    if (elapsed < 0.8) return;
                    TestArenaViewports(2);
                    Click("このアバターで進む");
                    Assert(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.GameModeSelection, "2P mode selection reachable");
                    Next(6); break;
                case 6:
                    if (elapsed < 0.8) return;
                    Capture("06-mode-selection");
                    Click(round == 2 ? "協力プレイ" : "対戦プレイ");
                    Next(1); break;
                case 7:
                    if (elapsed < 0.3) return;
                    Assert(!SpawnedCubes().Any(), "Mid-round return removes spawned detached debris");
                    loop.ReturnToConnectionWait();
                    Assert(loop.Players.All(p => p.CurrentMocopiAvatar.gameObject.activeInHierarchy), "Both receivers active in connection wait");
                    loop.SetSingleMode();
                    Next(9); break;
                case 8:
                    if (elapsed < 6) return;
                    Assert(state == MocopiLocalGameLoopController.GameLoopState.AvatarSelection, "Cancelled countdown cannot start a round later");
                    Assert(loop.Players.All(p => !p.WallManager.IsGameRunning()), "No walls after countdown cancellation");
                    Click("このアバターで進む");
                    Next(1); break;
                case 9:
                    if (elapsed < 0.3) return;
                    TestArenaViewports(1);
                    Debug.Log("GAME_LOOP_PLAYMODE_PASSED: selection, ready, countdown, ten walls, result, paused retry, co-op, versus, menu return, debris cleanup, 2P to 1P stage and HUD reset.");
                    Finish(0); break;
            }
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    // Authoring prototypes can exist in the scene; round cleanup only owns spawned walls.
    private static System.Collections.Generic.IEnumerable<WallCube> SpawnedCubes() =>
        Object.FindObjectsByType<WallCube>(FindObjectsSortMode.None).Where(cube => !authoredCubes.Contains(cube));
    private static void Next(int next) { phase = next; phaseAt = EditorApplication.timeSinceStartup; }
    private static void ObserveCountdown()
    {
        var label = (UnityEngine.UI.Text)Get(loop, "countdownText");
        if (label == null) label = legacyCountdown.GetComponentInChildren<UnityEngine.UI.Text>();
        Assert(label.gameObject.activeInHierarchy, "Countdown UI visible");
        if (countdownFrames.Count > 0 && countdownFrames.Last() == label.text) return;
        countdownFrames.Add(label.text);
        countdownTimes.Add(Time.realtimeSinceStartupAsDouble);
        if (round == 0) Capture("countdown-" + (label.text == "スタート！" ? "start" : label.text));
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
    private static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    private static void Click(string label)
    {
        var button = Object.FindObjectsByType<TouchableUIPanel>(FindObjectsSortMode.None).First(p => p.Label == label);
        var scale = button.transform.localScale;
        button.Activate();
        Assert(button.transform.localScale == scale, "Touch feedback never scales the collider");
        Assert(!button.IsTouched, "Hidden panels clear contact state");
    }

    private static void TestMouseRadiusEdit()
    {
        float original = (float)Get(loop, "debugMouseColliderRadius");
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            Set(loop, "debugMouseColliderRadius", 0.043f);
            typeof(MocopiLocalGameLoopController).GetMethod("OnValidate", flags).Invoke(loop, null);
            typeof(MocopiLocalGameLoopController).GetMethod("Update", flags).Invoke(loop, null);
            var mouse = Object.FindFirstObjectByType<DebugMousePlayerCollider>();
            Assert(mouse != null && Mathf.Abs(mouse.GetComponent<SphereCollider>().radius - 0.043f) < 0.0001f,
                "Controller Inspector radius refreshes the existing mouse collider");
            Assert(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.AvatarSelection,
                "Changing a mouse radius preserves the current game state");
            Debug.Log("MOUSE_COLLIDER_INSPECTOR_PASSED: live radius update without a state transition.");
        }
        finally
        {
            Set(loop, "debugMouseColliderRadius", original);
            typeof(MocopiLocalGameLoopController).GetMethod("OnValidate", flags).Invoke(loop, null);
            typeof(MocopiLocalGameLoopController).GetMethod("Update", flags).Invoke(loop, null);
        }
    }

    private static void TestTouchLifecycle()
    {
        var root = new GameObject("Touch validation");
        root.transform.position = Vector3.one * 1000;
        root.transform.localScale = new Vector3(1.2f, 0.9f, 1);
        var panel = root.AddComponent<TouchableUIPanel>();
        int clicks = 0;
        panel.OnClick.AddListener(() => clicks++);
        var hand = new GameObject("Test Player", typeof(SphereCollider));
        hand.transform.position = Vector3.one * 2000;
        hand.tag = "Player";
        var collider = hand.GetComponent<Collider>();
        panel.SendMessage("OnTriggerEnter", collider);
        Set(panel, "enabledAt", -10f); Set(panel, "heldTime", 1f);
        panel.SendMessage("Update");
        Assert(clicks == 0, "Initial overlap must leave before activation");
        panel.SendMessage("OnTriggerExit", collider);
        panel.SendMessage("OnTriggerEnter", collider);
        Set(panel, "heldTime", 1f);
        panel.SendMessage("Update"); panel.SendMessage("Update");
        Assert(clicks == 1, $"One confirmation per held touch: clicks={clicks}, touched={panel.IsTouched}, progress={panel.HoldProgress}, active={panel.isActiveAndEnabled}, layer={hand.layer}, playerLayer={Get(panel, "playerLayer")}, wait={Get(panel, "waitForRelease")}, activated={Get(panel, "activated")}");
        Assert(root.transform.localScale == new Vector3(1.2f, 0.9f, 1), "Touch preserves authored scale and collider");
        root.SetActive(false); root.SetActive(true);
        Assert(!panel.IsTouched && panel.HoldProgress == 0, "Re-enabled panel is clean");
        Set(panel, "enabledAt", -10f);
        panel.SendMessage("OnTriggerEnter", collider);
        collider.enabled = false;
        panel.SendMessage("Update");
        Assert(!panel.IsTouched, "Disabled colliders cannot stick");
        var rig = loop.Players[0].ReadyPoseAnimator.GetComponent<MocopiPlayerColliderRig>();
        var filter = typeof(TouchableUIPanel).GetMethod("IsAcceptedTouch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        foreach (var body in rig.GetComponentsInChildren<Collider>())
            if (body.name.StartsWith("Head")) Assert(!(bool)filter.Invoke(panel, new object[] { body }), "Head cannot activate menu");
        Object.Destroy(root); Object.Destroy(hand);
        Debug.Log("GAME_TOUCH_PASSED: early overlap, dwell, single activation, stable collider, re-enable, removed collider, body filtering.");
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

    private static void TestArenaIsolation()
    {
        var arenas = Object.FindFirstObjectByType<PlayerArenaIsolation>();
        Assert(arenas != null, "Player arenas are configured in the saved scene");
        Assert(arenas.environments.Length == 2 && arenas.environments[0] != arenas.environments[1], "Independent stage instances");
        Assert(Physics.GetIgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two), "Physics between player arenas disabled");
        for (int i = 0; i < 2; i++)
        {
            int layer = PlayerArenaLayers.One + i, otherLayer = PlayerArenaLayers.One + 1 - i;
            var player = loop.Players[i];
            Assert(player.PlayerCamera.cullingMask == 1 << layer, "Camera sees only its arena");
            Assert(arenas.environments[i].GetComponentsInChildren<MeshRenderer>(true).Length > 0, "Each arena contains the stage mesh");
            foreach (var child in arenas.environments[i].GetComponentsInChildren<Transform>(true)) Assert(child.gameObject.layer == layer, "Entire environment has its own layer");
            foreach (var light in arenas.environments[i].GetComponentsInChildren<Light>(true)) Assert(light.cullingMask == 1 << layer, "Lighting cannot cast shadows into the other arena");
            foreach (var collider in player.CurrentMocopiAvatar.GetComponentsInChildren<Collider>(true)) Assert(collider.gameObject.layer == layer, "Generated body colliders inherit arena");
            var hand = new GameObject("Arena test hand", typeof(SphereCollider));
            hand.tag = "Player"; hand.layer = otherLayer;
            hand.transform.position = Vector3.one * 2000;
            var cubeObject = new GameObject("Arena test cube", typeof(BoxCollider), typeof(Rigidbody), typeof(WallCube));
            cubeObject.layer = layer; cubeObject.transform.position = Vector3.one * 1000;
            var cube = cubeObject.GetComponent<WallCube>();
            cube.SendMessage("OnTriggerEnter", hand.GetComponent<Collider>());
            Assert(!cube.IsHit, "Opponent cannot score a hit on this wall");
            var panelObject = new GameObject("Arena test panel");
            panelObject.layer = layer; panelObject.transform.position = Vector3.one * 3000;
            var panel = panelObject.AddComponent<TouchableUIPanel>();
            panel.SendMessage("OnTriggerEnter", hand.GetComponent<Collider>());
            Assert(!panel.IsTouched, "Opponent cannot touch this menu");
            hand.layer = layer;
            cube.SendMessage("OnTriggerEnter", hand.GetComponent<Collider>());
            Assert(cube.IsHit && cube.gameObject.layer == layer, "Own contact works and detached cube keeps its layer");
            panel.SendMessage("OnTriggerEnter", hand.GetComponent<Collider>());
            Assert(panel.IsTouched, "Own hand can touch its menu");
            Object.Destroy(hand); Object.Destroy(cubeObject); Object.Destroy(panelObject);
        }
        Debug.Log("PLAYER_ARENAS_ISOLATION_PASSED: stage duplication, camera masks, lighting, generated colliders, cross-player hit rejection, touch rejection, own interaction.");
    }

    private static void TestArenaViewports(int count)
    {
        var arenas = Object.FindFirstObjectByType<PlayerArenaIsolation>();
        var view = Object.FindFirstObjectByType<GameModePresentation>();
        var canvas = view.modeLabel.canvas;
        for (int i = 0; i < 2; i++)
        {
            bool active = i < count;
            var hud = (RectTransform)canvas.transform.Find($"{i + 1}P_HUD");
            Assert(arenas.environments[i].activeInHierarchy == active, "Only active players have visible environments");
            Assert(hud.gameObject.activeInHierarchy == active, "Only active players have visible HUDs");
            if (!active) continue;
            var camera = loop.Players[i].PlayerCamera;
            Assert(hud.anchorMin == camera.rect.min && hud.anchorMax == camera.rect.max, "HUD is confined to its player's viewport");
            Assert(hud.GetComponent<UnityEngine.UI.RectMask2D>() != null, "HUD is clipped at the viewport boundary");
            foreach (var collider in loop.Players[i].CurrentMocopiAvatar.GetComponentsInChildren<Collider>())
                Assert(collider.gameObject.layer == PlayerArenaLayers.One + i, "Activated avatar colliders keep their player layer");
        }
        Debug.Log("PLAYER_ARENAS_VIEWPORTS_PASSED: active players=" + count);
    }

    private static void Capture(string name)
        => CaptureView(loop, name);

    public static void CaptureView(MocopiLocalGameLoopController loop, string name)
    {
        Directory.CreateDirectory("Logs/GameModePreview");
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        foreach (var player in loop.Players.Take(loop.ActivePlayerCount))
        {
            var camera = player.PlayerCamera;
            var target = camera.targetTexture;
            var rect = camera.rect;
            int width = Mathf.RoundToInt(1600 * rect.width), height = Mathf.RoundToInt(900 * rect.height);
            var viewport = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = viewport;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.Render();
            camera.targetTexture = target;
            camera.rect = rect;
            Graphics.CopyTexture(viewport, 0, 0, 0, 0, width, height, rt, 0, 0, Mathf.RoundToInt(rect.x * 1600), Mathf.RoundToInt(rect.y * 900));
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(viewport);
        }
        var canvas = Object.FindFirstObjectByType<GameModePresentation>().GetComponentInChildren<Canvas>();
        var uiObject = new GameObject("Preview UI camera");
        var ui = uiObject.AddComponent<Camera>(); ui.enabled = false;
        ui.clearFlags = CameraClearFlags.Depth; ui.cullingMask = 1 << 5; ui.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = ui; canvas.planeDistance = 1;
        foreach (var text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>()) text.SetAllDirty();
        Canvas.ForceUpdateCanvases(); ui.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/GameModePreview/" + name + ".png", image.EncodeToPNG());
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
        RenderTexture.active = previous;
        ui.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(uiObject);
        RenderTexture.ReleaseTemporary(rt);
    }
}
