using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GameModeSwitchValidation
{
    private const string Key = "GameModeSwitchValidation.Running";
    private const string Output = "output/mode-switch";
    private static int step;
    private static double changedAt, startedAt;
    private static Vector3 singlePosition;
    private static Quaternion singleRotation;
    private static MocopiLocalGameLoopController loop;

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.update -= Tick;
        if (SessionState.GetBool(Key, false))
        {
            step = 0;
            startedAt = changedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
    }
    [MenuItem("Tools/mocopi/Validate Mode Switching")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/validation.txt", "Mode switching regression: " + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path + "\n");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 100) throw new Exception("Timed out at step " + step);
            if (EditorApplication.timeSinceStartup - changedAt < 1.2) return;
            if (loop == null) loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
            var camera = loop.Players[0].PlayerCamera;
            switch (step)
            {
                case 0:
                    var settings = new SerializedObject(loop);
                    settings.FindProperty("debugSkipMocopiConnection").boolValue = true;
                    settings.FindProperty("debugAutoReady").boolValue = false;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    loop.SetDebugMode(true); loop.SetPlayerCount(1); break;
                case 1:
                    Check(loop.ActivePlayerCount == 1 && loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.AvatarSelection, "1P selection available: " + loop.CurrentState + ", count=" + loop.ActivePlayerCount);
                    FullView(); singlePosition = camera.transform.position; singleRotation = camera.transform.rotation;
                    Capture("01-single"); Click("1人 / 2人 切り替え"); break;
                case 2:
                    Check(loop.ActivePlayerCount == 2 && loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.AvatarSelection, "2P count chosen without committing a mode");
                    SelectionView(); Capture("02-two-players-pending"); Click("このアバターで進む"); break;
                case 3:
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.GameModeSelection, "Co-op / versus choice shown");
                    SelectionView(); Capture("03-mode-selection"); Click("協力プレイ"); break;
                case 4:
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Ready && loop.UsesSharedCoopView, "Co-op commits shared view");
                    FullView();
                    Check(Vector3.Distance(singlePosition, camera.transform.position) > 5, "Co-op camera pulls back");
                    foreach (var p in loop.Players)
                    {
                        var screen = camera.WorldToViewportPoint(p.CurrentMocopiAvatar.transform.position + Vector3.up);
                        Check(screen.z > 0 && screen.x > 0 && screen.x < 1 && screen.y > 0 && screen.y < 1, "Both players framed in co-op");
                    }
                    Capture("04-coop"); Click("選び直す"); break;
                case 5:
                    SelectionView(); Click("このアバターで進む"); Click("対戦プレイ"); break;
                case 6:
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Ready && loop.UsesSplitScreenView, "Versus commits split view");
                    Check(loop.Players.All(p => p.PlayerCamera.enabled && Mathf.Abs(p.PlayerCamera.rect.width - 0.5f) < 0.001f), "Two half-width cameras enabled");
                    Check(loop.Players[0].PlayerCamera.rect.x == 0 && loop.Players[1].PlayerCamera.rect.x == 0.5f, "Viewports do not overlap");
                    Check(loop.GetComponent<PlayerArenaIsolation>().environments.All(e => e.activeSelf), "Versus restores separate arenas");
                    Capture("05-versus"); Click("選び直す"); break;
                case 7:
                    SelectionView(); Click("1人 / 2人 切り替え"); break;
                case 8:
                    Check(loop.ActivePlayerCount == 1, "Switching back to 1P available");
                    SelectionView(); Click("このアバターで進む"); break;
                case 9:
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Ready && loop.CurrentMode == MocopiLocalGameLoopController.GameMode.Single, "1P proceeds directly to ready");
                    FullView(); Capture("06-single-ready");
                    File.AppendAllText(Output + "/validation.txt", "PASS: 1P -> pending 2P -> mode selection -> co-op -> select again -> versus -> select again -> 1P ready; real button events, cameras, camera poses, HUD and arenas.\n");
                    Finish(); return;
            }
            step++; changedAt = EditorApplication.timeSinceStartup;
        }
        catch (Exception e)
        {
            File.AppendAllText(Output + "/validation.txt", "FAIL step " + step + ": " + e + "\n");
            Debug.LogException(e); Finish();
        }
    }
    private static void SelectionView()
    {
        FullView();
        Check(!loop.UsesSharedCoopView && !loop.UsesSplitScreenView, "Selection has no committed camera layout");
        Check(Vector3.Distance(singlePosition, loop.Players[0].PlayerCamera.transform.position) < 0.05f && Quaternion.Angle(singleRotation, loop.Players[0].PlayerCamera.transform.rotation) < 0.2f, "Selection keeps the 1P camera pose");
        Check(!loop.GetComponent<PlayerArenaIsolation>().environments[1].activeSelf, "Selection keeps one environment");
    }
    private static void FullView()
    {
        Check(loop.Players[0].PlayerCamera.enabled && loop.Players[0].PlayerCamera.rect == new Rect(0,0,1,1), "1P camera fills viewport");
        Check(!loop.Players[1].PlayerCamera.enabled, "2P camera disabled in full view");
        var view = Object.FindFirstObjectByType<GameModePresentation>();
        Check(!view.modeLabel.canvas.transform.Find("2P_HUD").gameObject.activeSelf, "One HUD in full view");
    }
    private static void Click(string label)
    {
        var button = Object.FindObjectsByType<TouchableUIPanel>(FindObjectsSortMode.None).FirstOrDefault(p => p.Label == label && p.isActiveAndEnabled);
        if (button == null) throw new Exception("Button not available: " + label);
        button.OnClick.Invoke();
    }
    private static void Capture(string name)
    {
        GameModeValidation.CaptureView(loop, "mode-switch-" + name);
        File.Copy("Logs/GameModePreview/mode-switch-" + name + ".png", Output + "/" + name + ".png", true);
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Finish()
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
    }
}
