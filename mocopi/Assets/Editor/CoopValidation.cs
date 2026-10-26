using System;
using System.IO;
using System.Linq;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CoopValidation
{
    private const string Key = "CoopValidation.Running";
    private static int phase;
    private static double changedAt, startedAt;
    private static MocopiLocalGameLoopController loop;
    private static Vector3[] positions;
    [MenuItem("Tools/mocopi/Validate Suguru and Co-op")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        ValidateSettings();
        File.WriteAllText("Logs/coop-validation.txt", "Saved scene settings passed: " + loop.gameObject.scene.path + "\n");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        phase = 0;
        startedAt = changedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }
    private static void ValidateSettings()
    {
        loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var p1 = loop.Players[0]; var p2 = loop.Players[1];
        Check(p2.CurrentMocopiAvatar.name == "2P_suguru" && p2.ReadyPoseAnimator.isHuman, "Suguru humanoid assigned to 2P");
        Check(Object.FindObjectsByType<MocopiSimpleReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(r => r.AvatarSettings.Any(s => s.MocopiAvatar == p2.CurrentMocopiAvatar)), "2P receiver references Suguru");
        var one = new SerializedObject(p1.WallManager); var two = new SerializedObject(p2.WallManager);
        foreach (string field in new[] { "moveSpeed", "destroyDistance", "spawnInterval", "wallCount", "safeHitThreshold" })
            Check(one.FindProperty(field).propertyType == SerializedPropertyType.Integer
                ? one.FindProperty(field).intValue == two.FindProperty(field).intValue
                : one.FindProperty(field).floatValue == two.FindProperty(field).floatValue, "Matching " + field);
        Check(one.FindProperty("useSavedMasks").boolValue == two.FindProperty("useSavedMasks").boolValue, "Matching pose library setting");
        var template1 = (GameObject)one.FindProperty("wallPrefab").objectReferenceValue;
        var template2 = (GameObject)two.FindProperty("wallPrefab").objectReferenceValue;
        Check(template1.transform.childCount == template2.transform.childCount && template2.transform.childCount == 1800, "Both wall templates have 1800 cells");
        Check(((Transform)two.FindProperty("spawnPoint").objectReferenceValue).IsChildOf(p2.WallManager.transform.parent), "2P spawn follows arena");
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 100) throw new Exception("Co-op test timed out at phase " + phase);
            if (EditorApplication.timeSinceStartup - changedAt < 0.8) return;
            if (loop == null) loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
            var arenas = loop.GetComponent<PlayerArenaIsolation>();
            switch (phase)
            {
                case 0:
                    positions = arenas.environments.Select(e => e.transform.parent.position).ToArray();
                    loop.SetDebugMode(true);
                    loop.SetPlayerCount(2);
                    loop.ConfirmAvatarSelection();
                    loop.SetCoopMode();
                    foreach (var p in loop.Players)
                    {
                        var settings = new SerializedObject(p.WallManager);
                        settings.FindProperty("moveSpeed").floatValue = 18;
                        settings.FindProperty("spawnInterval").floatValue = 0.4f;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    break;
                case 1:
                    Check(loop.Players[0].PlayerCamera.enabled && !loop.Players[1].PlayerCamera.enabled, "Co-op uses one camera");
                    Check(loop.Players[0].PlayerCamera.rect == new Rect(0, 0, 1, 1), "Co-op full viewport");
                    Check(!arenas.environments[1].activeSelf, "Co-op single environment");
                    Check(Physics.GetIgnoreLayerCollision(8, 9), "Cross-player hits remain isolated");
                    foreach (var p in loop.Players)
                    {
                        Vector3 screen = loop.Players[0].PlayerCamera.WorldToViewportPoint(p.CurrentMocopiAvatar.transform.position + Vector3.up);
                        Check(screen.x > 0.05f && screen.x < 0.95f && screen.y > 0.05f && screen.y < 0.95f && screen.z > 0, "Both avatars in frame");
                        Check(p.CurrentMocopiAvatar.GetComponentsInChildren<Collider>().Length > 10, "Body colliders available");
                    }
                    var visual = arenas.environments[0].GetComponentInChildren<ArenaMusicVisuals>();
                    Check(Mathf.Abs(visual.transform.Find("Audio Floor").localScale.x * visual.circleDiameter - 10) < 0.01f, "Shared floor diameter is ten metres");
                    var view = Object.FindFirstObjectByType<GameModePresentation>();
                    Check(!view.modeLabel.canvas.transform.Find("2P_HUD").gameObject.activeSelf, "Only one co-op HUD");
                    var sharedCamera = loop.Players[0].PlayerCamera;
                    foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
                    {
                        sharedCamera.aspect = aspect;
                        typeof(GameModePresentation).GetMethod("UpdateSharedCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                        foreach (var p in loop.Players)
                        {
                            Vector3 screen = sharedCamera.WorldToViewportPoint(p.CurrentMocopiAvatar.transform.position + Vector3.up);
                            Check(screen.x > 0.05f && screen.x < 0.95f && screen.y > 0.05f && screen.y < 0.95f, "Avatars framed at aspect " + aspect);
                        }
                    }
                    sharedCamera.ResetAspect();
                    typeof(GameModePresentation).GetMethod("UpdateSharedCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                    GameModeValidation.CaptureView(loop, "coop-ready");
                    loop.ConfirmAvatarSelection();
                    loop.ConfirmTwoPlayerModeSelection();
                    loop.StartDebugRound();
                    break;
                case 2:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Playing) return;
                    Check(loop.Players.All(p => p.WallManager.UsesSavedMasks), "Both players use compatible saved poses");
                    foreach (var floor in arenas.environments[0].GetComponentsInChildren<Collider>())
                        if (!floor.isTrigger && (floor.name == "Plane" || floor.name == "Floor"))
                            Check((floor.includeLayers.value & (1 << WallDebrisCollision.PlayerTwoDebris)) != 0, "Shared floor catches 2P debris");
                    GameModeValidation.CaptureView(loop, "coop-playing");
                    break;
                case 3:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Result) return;
                    Check(loop.Players.All(p => p.WallManager.GetCompletedWallCount() == 10), "Both players finish all ten walls");
                    GameModeValidation.CaptureView(loop, "coop-result");
                    loop.Retry();
                    break;
                case 4:
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Ready, "Co-op retry returns to ready");
                    Check(loop.Players[0].PlayerCamera.rect.width == 1 && !loop.Players[1].PlayerCamera.enabled, "Retry restores shared full viewport");
                    loop.ReturnToAvatarSelection();
                    loop.ConfirmAvatarSelection();
                    loop.SetVersusMode();
                    break;
                case 5:
                    for (int i = 0; i < 2; i++)
                    {
                        Check(loop.Players[i].PlayerCamera.enabled && loop.Players[i].PlayerCamera.rect.width == 0.5f, "Versus restores two split cameras");
                        Check(arenas.environments[i].transform.parent.position == positions[i], "Arena positions restored");
                    }
                    GameModeValidation.CaptureView(loop, "coop-to-versus");
                    loop.SetGameMode(MocopiLocalGameLoopController.GameMode.Single);
                    break;
                case 6:
                    Check(loop.Players[0].PlayerCamera.rect.width == 1 && !loop.Players[1].PlayerCamera.gameObject.activeSelf, "Single restores full viewport");
                    GameModeValidation.CaptureView(loop, "coop-to-single");
                    File.AppendAllText("Logs/coop-validation.txt", "PASS: Suguru, settings, shared camera, floor, HUD, ten-wall co-op round, result, versus and single restoration.\n");
                    Finish(); return;
            }
            phase++; changedAt = EditorApplication.timeSinceStartup;
        }
        catch (Exception error)
        {
            File.AppendAllText("Logs/coop-validation.txt", "FAIL: " + error + "\n");
            Debug.LogException(error); Finish();
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Finish()
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
    }
}
