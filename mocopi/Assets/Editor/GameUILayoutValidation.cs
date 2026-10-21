using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises the real Game view at desktop and windowed resolutions.</summary>
public static class GameUILayoutValidation
{
    private const string Request = "Temp/GameUILayoutValidation.request";
    private const string Running = "GameUILayoutValidation.Running";
    private const string Output = "output/ui-layout";
    private static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(1024, 768) };
    private static int step = -1;
    private static double changedAt;
    private static bool captureQueued;
    private static EditorWindow gameView;
    private static MocopiLocalGameLoopController loop;
    private static GameModePresentation view;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (!SessionState.GetBool(Running, false))
            {
                if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(Request);
                Directory.CreateDirectory(Output);
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.path != GameModeValidation.ScenePath) throw new Exception("Open GameMode before the UI check.");
                string backup = Output + "/GameMode-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not back up the live scene.");
                var presentation = Object.FindFirstObjectByType<GameModePresentation>();
                var canvas = presentation.modeLabel.canvas;
                Undo.RecordObject(canvas.GetComponent<CanvasScaler>(), "Full HD UI layout");
                GameModePresentation.ConfigureCanvas(canvas.GetComponent<CanvasScaler>());
                Canvas.ForceUpdateCanvases();
                foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true)) Undo.RecordObject(rect, "Full HD UI layout");
                GameModePresentation.ConfigureHud((RectTransform)canvas.transform, false);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save UI layout.");
                SessionState.SetBool(Running, true);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (loop == null)
            {
                loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
                view = Object.FindFirstObjectByType<GameModePresentation>();
                if (loop == null || view == null || !view.GetComponent<ResultLeaderboardPanel>()) return;
                gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
                SessionState.SetInt(Running + ".OriginalSize", (int)gameView.GetType().GetProperty("selectedSizeIndex", Flags).GetValue(gameView));
                loop.SetDebugMode(true);
                changedAt = EditorApplication.timeSinceStartup;
            }
            if (EditorApplication.timeSinceStartup - changedAt < 0.85) return;
            if (step >= 0 && !captureQueued)
            {
                CheckAndCapture();
                captureQueued = true;
                changedAt = EditorApplication.timeSinceStartup;
                return; // CaptureScreenshot renders at end of frame; keep this state until it finishes.
            }
            captureQueued = false;
            step++;
            if (step >= Sizes.Length * 8)
            {
                File.WriteAllText(Output + "/verification.txt", "PASSED: 1920x1080, 1280x720, 1024x768; solo and split; selection, ready, result name entry and navigation. HUD bounds, touch bounds, non-overlap, camera restore.\n");
                Finish();
                return;
            }
            SetSize(Sizes[step / 8]);
            int state = step % 4;
            if (state == 0)
            {
                loop.SetGameMode(step % 8 < 4 ? MocopiLocalGameLoopController.GameMode.Single : MocopiLocalGameLoopController.GameMode.Coop);
            }
            else if (state == 1)
                ChangeState(MocopiLocalGameLoopController.GameLoopState.Ready);
            else if (state == 2)
            {
                foreach (var player in loop.Players)
                    player.GetType().GetField("lastResult", Flags).SetValue(player, new WallManager.ResultData(8, 10, 4, 8, 2, 3));
                var board = view.GetComponent<ResultLeaderboardPanel>();
                board.GetType().GetField("leaderboard", Flags).SetValue(board, new LocalLeaderboard());
                ChangeState(MocopiLocalGameLoopController.GameLoopState.Result);
            }
            else
            {
                var board = view.GetComponent<ResultLeaderboardPanel>();
                while (board.IsEnteringName) board.GetType().GetMethod("Skip", Flags).Invoke(board, null);
            }
            changedAt = EditorApplication.timeSinceStartup;
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/error.txt", "Step " + step + "\n" + error);
            Debug.LogException(error);
            Finish();
        }
    }

    private static void ChangeState(MocopiLocalGameLoopController.GameLoopState state) =>
        loop.GetType().GetMethod("ChangeState", Flags).Invoke(loop, new object[] { state });

    private static void SetSize(Vector2Int size)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { GameViewSizeGroupType.Standalone });
        var groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int selected = -1;
        for (int i = 0; i < count; i++)
        {
            var item = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)item.GetType().GetProperty("width").GetValue(item) == size.x &&
                (int)item.GetType().GetProperty("height").GetValue(item) == size.y) { selected = i; break; }
        }
        if (selected < 0)
        {
            var type = assembly.GetType("UnityEditor.GameViewSize");
            var fixedType = Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1);
            var item = Activator.CreateInstance(type, Flags, null, new object[] { fixedType, size.x, size.y, "UI check " + size.x + "x" + size.y }, null);
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { item });
            selected = count;
        }
        gameView.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(gameView, selected);
        gameView.Show(); gameView.Repaint();
    }

    private static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    private static void CheckAndCapture()
    {
        var size = Sizes[step / 8];
        Assert(Screen.width == size.x && Screen.height == size.y, "Game view resolution");
        var canvas = view.modeLabel.canvas;
        bool result = loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Result;
        for (int i = 0; i < loop.ActivePlayerCount; i++)
        {
            var camera = loop.Players[i].PlayerCamera;
            var root = (RectTransform)canvas.transform.Find((i + 1) + "P_HUD");
            Assert(root.anchorMin == camera.rect.min && root.anchorMax == camera.rect.max, "HUD viewport matches camera");
            Rect available = camera.pixelRect;
            foreach (string name in new[] { "Header", "StateCard", "Footer" })
            {
                Rect rect = ScreenRect((RectTransform)root.Find(name));
                Assert(rect.xMin >= available.xMin - 1 && rect.xMax <= available.xMax + 1 &&
                    rect.yMin >= available.yMin - 1 && rect.yMax <= available.yMax + 1, name + " inside viewport");
            }
            Rect card = ScreenRect((RectTransform)root.Find("StateCard"));
            Assert(!card.Overlaps(ScreenRect((RectTransform)root.Find("Header"))), "Header/card separation");
            foreach (var station in view.stations.Where(s => s.playerIndex == i && s.root.gameObject.activeInHierarchy))
            foreach (var button in station.root.GetComponentsInChildren<TouchableUIPanel>())
            {
                var bounds = button.GetComponent<BoxCollider>().bounds;
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 screen = camera.WorldToScreenPoint(p); min = Vector3.Min(min, screen); max = Vector3.Max(max, screen);
                }
                Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                Assert(rect.xMin >= available.xMin && rect.xMax <= available.xMax && rect.yMin >= available.yMin && rect.yMax <= available.yMax, "Touch button inside viewport");
                Assert(!rect.Overlaps(card), $"{i + 1}P touch {rect} clear of explanation {card}");
            }
            Assert(result || camera.rect.xMax <= 1, "Restored normal viewport");
        }
        string label = new[] { "selection", "ready", "result-entry", "result-actions" }[step % 4];
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + size.x + "x" + size.y + "-" + loop.ActivePlayerCount + "P-" + label + ".png"));
        File.AppendAllText(Output + "/checks.txt", step + ": " + size + " " + loop.ActivePlayerCount + "P " + label + " passed\n");
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Finish()
    {
        SessionState.SetBool(Running, false);
        if (gameView != null) gameView.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(gameView, SessionState.GetInt(Running + ".OriginalSize", 0));
        EditorApplication.isPlaying = false;
    }
}
