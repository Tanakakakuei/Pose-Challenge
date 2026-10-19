using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run in a project copy; exercises the runtime editor then exits Unity.</summary>
public static class CaptureMaskEditorPlayValidation
{
    private const string Running = "CaptureMaskEditorPlayValidation.Running";
    private static CaptureMaskDataMode mode;
    private static int phase;
    private static double started, phaseAt;
    private static string imagePath;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        started = phaseAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 60) throw new Exception("Capture editor test timed out: phase " + phase);
            double elapsed = EditorApplication.timeSinceStartup - phaseAt;
            if (mode == null) mode = UnityEngine.Object.FindFirstObjectByType<CaptureMaskDataMode>();
            if (mode == null || elapsed < 1) return;
            switch (phase)
            {
                case 0:
                    Screen.SetResolution(1600, 900, false);
                    Set("captureDelay", 0f);
                    mode.RequestCapture(); Next(1); break;
                case 1:
                    Check(mode.HasPreview, "Runtime capture produced a preview.");
                    var data = (WallMaskData)Get("snapshot");
                    var original = (bool[,])data.GetMask().Clone();
                    Send(EventType.MouseDown, 1, new Vector2Int(0, 0));
                    Send(EventType.MouseDrag, 1, new Vector2Int(6, 0));
                    Send(EventType.MouseUp, 1, new Vector2Int(6, 0));
                    for (int x = 0; x <= 6; x++) Check(!data.GetMask()[0, x], "Right drag removes every traversed cell.");
                    mode.UndoEdit(); Equal(original, data.GetMask(), "One undo removes the entire drag stroke.");
                    mode.RedoEdit();
                    Send(EventType.MouseDown, 0, new Vector2Int(2, 0));
                    Send(EventType.MouseUp, 0, new Vector2Int(2, 0));
                    Check(data.GetMask()[0, 2], "Left click adds a cube to the empty cell.");
                    var beforeOutside = (bool[,])data.GetMask().Clone();
                    Send(EventType.MouseDown, 1, new Vector2Int(10, 0), false);
                    Send(EventType.MouseDrag, 1, new Vector2Int(12, 0));
                    Send(EventType.MouseUp, 1, new Vector2Int(12, 0));
                    Equal(beforeOutside, data.GetMask(), "Dragging from a UI control/outside cannot paint the grid.");
                    mode.SetOutlineMargin(0.08f);
                    Check(data.GetMask()[0, 2] && !data.GetMask()[0, 3], "Runtime slider preserves overrides.");
                    Set("capturePending", true);
                    Check(!mode.SetPreviewCell(0, 2, false), "Countdown blocks manual editing.");
                    Set("capturePending", false);
                    // Test invalid final shapes without writing to the user's mask library.
                    var edits = (CaptureMaskEdits)Get("edits");
                    edits.BeginStroke();
                    for (int y = 0; y < 52; y++)
                    for (int x = 0; x < 42; x++) edits.Paint(y, x, false);
                    edits.EndStroke();
                    Invoke("RefreshEditedPreview");
                    mode.SaveCapture();
                    Check(mode.LastSavedPath == null, "An entirely empty wall cannot be saved.");
                    mode.UndoEdit();
                    edits.BeginStroke();
                    for (int y = 0; y < 52; y++)
                    for (int x = 0; x < 42; x++) edits.Paint(y, x, true);
                    edits.EndStroke();
                    Invoke("RefreshEditedPreview"); mode.SaveCapture();
                    Check(mode.LastSavedPath == null, "A wall without a hole cannot be saved.");
                    mode.UndoEdit();
                    Debug.Log("CAPTURE_CELL_EDITOR_PLAY_PASSED: runtime click/drag, interpolation, undo/redo, empty-cell placement, outside drag guard, slider persistence, capture lock, invalid mask save guards.");
                    Next(2); break;
                case 2:
                    if (Application.isBatchMode) { Next(3); break; }
                    Directory.CreateDirectory("Logs/CaptureMaskEditor");
                    imagePath = Path.GetFullPath("Logs/CaptureMaskEditor/edited.png");
                    ScreenCapture.CaptureScreenshot(imagePath);
                    Next(3); break;
                case 3:
                    if (!Application.isBatchMode && !File.Exists(imagePath)) return;
                    if (imagePath != null) Debug.Log("CAPTURE_CELL_EDITOR_SCREENSHOT: " + imagePath);
                    mode.RetryCapture();
                    Check(!mode.HasPreview && Get("edits") == null && Get("editorTexture") == null, "Retry clears manual state and resources.");
                    mode.RequestCapture(); Next(4); break;
                case 4:
                    Check(mode.HasPreview && ((CaptureMaskEdits)Get("edits")).Count == 0, "New capture starts without stale edits.");
                    Debug.Log("CAPTURE_CELL_EDITOR_RECAPTURE_PASSED");
                    Finish(0); break;
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void Send(EventType type, int button, Vector2Int cell, bool hit = true)
    {
        typeof(CaptureMaskDataMode).GetMethod("HandleBrushEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(mode, new object[] { type, button, hit, true, cell });
    }
    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
    private static object Get(string field) => typeof(CaptureMaskDataMode).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mode);
    private static void Set(string field, object value) => typeof(CaptureMaskDataMode).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mode, value);
    private static void Invoke(string method) => typeof(CaptureMaskDataMode).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mode, null);
    private static void Equal(bool[,] expected, bool[,] actual, string reason)
    {
        for (int y = 0; y < expected.GetLength(0); y++)
        for (int x = 0; x < expected.GetLength(1); x++) Check(expected[y, x] == actual[y, x], reason);
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
