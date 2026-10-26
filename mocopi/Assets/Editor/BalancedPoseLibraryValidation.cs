using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Checks the bundled poses against real game scene templates without editing the user's scene.</summary>
public static class BalancedPoseLibraryValidation
{
    private const string Request = "Temp/BalancedPoseLibraryValidation.request";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    private static void Watch() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        File.Delete(Request);
        try { Run(); }
        catch (Exception error)
        {
            File.WriteAllText("output/balanced-pose-validation.txt", error.ToString());
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/mocopi/Validate 60 Balanced Poses")]
    public static void Run()
    {
        var library = SavedMaskLibrary.Load(SavedMaskLibrary.BundledDirectory, 30, 60);
        Check(library.Count == 60, "60 compatible bundled masks");
        Check(library.Select(e => Key(e.Mask)).Distinct().Count() == 60, "60 different shapes");
        foreach (string position in new[] { "right", "center", "left" })
            Check(library.Count(e => Path.GetFileName(e.Path).Contains("_" + position + "_")) == 20, position + " count");
        foreach (var entry in library)
        {
            var mask = entry.Mask;
            Check(Enumerable.Range(0, 60).All(x => mask[0, x]), "Top is not clipped: " + entry.Path);
            Check(Enumerable.Range(0, 30).All(y => mask[y, 0] && mask[y, 59]), "Sides are not clipped");
            if (entry.Path.Contains("_hop")) Check(Enumerable.Range(0, 60).All(x => mask[29, x]), "Jump clears the bottom row");
        }
        Check(SavedMaskLibrary.LoadDefault(30, 60).Count >= 60, "Default library includes bundled poses");
        Check(SavedMaskLibrary.Load(SavedMaskLibrary.BundledDirectory, 52, 42).Count == 0, "Incompatible grids excluded");
        for (int seed = 0; seed < 20; seed++)
            Check(SavedMaskLibrary.SelectRound(library, 10, new System.Random(seed)).Distinct().Count() == 10, "Ten questions without duplicates");

        Scene original = SceneManager.GetActiveScene();
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var details = new List<string>();
        int validatedManagers = 0;
        try
        {
            foreach (string name in new[] { "GameMode", "GameMode-Halloween" })
            {
                Scene scene = EditorSceneManager.OpenPreviewScene("Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/" + name + ".unity");
                try
                {
                    var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MocopiLocalGameLoopController>(true)).Single();
                    var managers = controller.Players.Select(player => player.WallManager).Distinct().ToArray();
                    Check(managers.Length > 0, "Scene managers exist");
                    foreach (var authored in managers)
                    {
                        var source = (GameObject)Get(authored, "wallPrefab");
                        Check(source != null, "Wall template exists");
                        var cells = source.GetComponentsInChildren<WallCube>(true);
                        int rows = cells.Max(c => int.Parse(c.name.Split('_')[1])) + 1;
                        int columns = cells.Max(c => int.Parse(c.name.Split('_')[2])) + 1;
                        if (rows != 30 || columns != 60)
                        {
                            Check(SavedMaskLibrary.Load(SavedMaskLibrary.BundledDirectory, rows, columns).Count == 0,
                                "Incompatible template cannot load these poses");
                            details.Add(name + "/" + authored.name + $": existing {rows}x{columns} template; bundled 30x60 poses excluded.");
                            continue;
                        }
                        validatedManagers++;
                        Check((bool)Get(authored, "useSavedMasks"), "Saved masks enabled");
                        var root = new GameObject("Pose validation manager");
                        var manager = root.AddComponent<WallManager>();
                        Set(manager, "wallPrefab", source);
                        Set(manager, "useSavedMasks", true);
                        Set(manager, "spawnPoint", root.transform);
                        try
                        {
                            Invoke(manager, "PrepareSavedRound");
                            Check(manager.UsesSavedMasks, "Manager loads default poses");
                            var selected = (List<SavedMaskLibrary.Entry>)Get(manager, "roundMasks");
                            Check(selected.Count == 10 && selected.Distinct().Count() == 10, "Manager selects ten unique poses");
                            for (int i = 0; i < 10; i++)
                            {
                                Invoke(manager, "SpawnWall");
                                var wall = ((List<GameObject>)Get(manager, "activeWalls")).Last();
                                try
                                {
                                    Check(Key(wall.GetComponent<WallMaskData>().GetMask()) == Key(selected[i].Mask), "Spawn preserves saved silhouette");
                                    var spawned = wall.GetComponentsInChildren<WallCube>(true);
                                    Check(spawned.Length == rows * columns, "Complete wall grid");
                                    foreach (var cube in spawned)
                                    {
                                        var parts = cube.name.Split('_');
                                        int y = rows - 1 - int.Parse(parts[1]), x = int.Parse(parts[2]);
                                        Check(cube.gameObject.activeSelf == selected[i].Mask[y, x], "Actual cube matches JSON");
                                    }
                                }
                                finally { Object.DestroyImmediate(wall); }
                            }
                            // An explicitly chosen empty folder must still override the default library.
                            string empty = Path.GetFullPath("Temp/EmptyPoseLibrary-" + Guid.NewGuid().ToString("N"));
                            Set(manager, "savedMaskDirectory", empty);
                            Invoke(manager, "PrepareSavedRound");
                            Check(!manager.UsesSavedMasks, "Explicit custom directory is respected");
                            details.Add(name + "/" + authored.name + ": 30x60; ten unique walls; all cells match.");
                        }
                        finally { Object.DestroyImmediate(root); }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(test, true);
        }
        Check(validatedManagers == 2, "Both GameMode and Halloween 1P templates validated");
        string result = "BALANCED_POSE_LIBRARY_VALIDATION_PASSED\n" + string.Join("\n", details);
        File.WriteAllText("output/balanced-pose-validation.txt", result);
        Debug.Log(result);
    }

    private static string Key(bool[,] mask) => string.Concat(mask.Cast<bool>().Select(solid => solid ? '1' : '0'));
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Balanced pose validation failed: " + message); }
}
