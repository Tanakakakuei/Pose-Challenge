using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HalloweenArenaValidation
{
    private const string Request = "Temp/HalloweenArenaValidation.request";
    private const string Running = "HalloweenArenaValidation.Running";
    private static int stage;
    private static double changedAt, startedAt;
    private static MocopiLocalGameLoopController loop;
    private static Vector3 firstBat;
    [InitializeOnLoadMethod]
    private static void Watch() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (!SessionState.GetBool(Running, false))
            {
                if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                try { File.Delete(Request); } catch (IOException) { return; }
                if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path != HalloweenArenaSetup.ScenePath) throw new Exception("Open Halloween scene first.");
                SessionState.SetBool(Running, true); EditorApplication.isPlaying = true; return;
            }
            if (!EditorApplication.isPlaying) return;
            if (loop == null)
            {
                loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
                if (loop == null) return;
                startedAt = changedAt = EditorApplication.timeSinceStartup;
                GameUILayoutValidation.SetSize(new Vector2Int(1920, 1080));
                loop.SetDebugMode(true); loop.SetSingleMode();
                firstBat = Object.FindFirstObjectByType<HalloweenAtmosphere>().bats[0].body.localPosition;
                return;
            }
            if (EditorApplication.timeSinceStartup - startedAt > 90) throw new Exception("Halloween play check timed out at " + stage);
            if (EditorApplication.timeSinceStartup - changedAt < 1.2) return;
            switch (stage)
            {
                case 0:
                    var decor = Object.FindFirstObjectByType<HalloweenAtmosphere>();
                    if ((firstBat - decor.bats[0].body.localPosition).sqrMagnitude < 0.001f) throw new Exception("Bat did not animate.");
                    foreach (var item in Object.FindObjectsByType<HalloweenAtmosphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (item.GetComponentsInChildren<Collider>(true).Length != 0) throw new Exception("Decoration has colliders.");
                        if (item.GetComponentsInChildren<Renderer>(true).Any(r => r.gameObject.layer != 8 + item.playerIndex || r.sharedMaterial == null)) throw new Exception("Decoration leaked between player layers.");
                    }
                    Capture("01-selection-fullhd"); Next(); break;
                case 1:
                    loop.ConfirmAvatarSelection(); loop.StartDebugRound(); Next(); break;
                case 2:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Playing) return;
                    Next(); break;
                case 3:
                    Capture("02-gameplay-fullhd"); Next(); break;
                case 4:
                    foreach (var item in Object.FindObjectsByType<HalloweenAtmosphere>(FindObjectsSortMode.None)) item.Celebrate();
                    Capture("03-lantern-celebration"); Next(); break;
                case 5:
                    loop.SetCoopMode(); Next(); break;
                case 6:
                    Capture("04-two-player-selection"); Next(); break;
                case 7:
                    loop.ConfirmAvatarSelection(); loop.SetCoopMode(); loop.StartDebugRound(); Next(); break;
                case 8:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Playing) return;
                    Next(); break;
                case 9:
                    Capture("05-two-player-gameplay"); Next(); break;
                case 10:
                    File.WriteAllText("output/halloween/play-verification.txt", "HALLOWEEN_PLAY_PASSED: full-HD solo and two-player selection, countdown and gameplay; bat animation; isolated layers; no prop colliders; celebration.\n");
                    Finish(); break;
            }
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("output/halloween"); File.WriteAllText("output/halloween/play-error.txt", error.ToString()); Debug.LogException(error); Finish();
        }
    }
    private static void Next() { stage++; changedAt = EditorApplication.timeSinceStartup; }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath("output/halloween/" + name + ".png"));
    private static void Finish() { SessionState.SetBool(Running, false); EditorApplication.isPlaying = false; }
}
