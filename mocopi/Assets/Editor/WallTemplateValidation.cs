using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Batch play-mode regression for contacts damaging a scene-based spawn template.</summary>
public static class WallTemplateValidation
{
    private const string Running = "WallTemplateValidation.Running";
    private static WallManager manager;
    private static GameObject template;
    private static BoxCollider player;
    private static string savedPath, savedJson;
    private static int phase;
    private static double started, phaseAt;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            if (EditorApplication.timeSinceStartup - started > 40) throw new Exception("Validation timed out.");
            if (phase == 0)
            {
                template = new GameObject("Scene wall template");
                template.transform.position = new Vector3(200, 0, 0);
                var data = template.AddComponent<WallMaskData>();
                var mask = new bool[3, 3];
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    mask[y, x] = y != 0 || x != 0;
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = $"Cube_{y}_{x}";
                    cell.transform.SetParent(template.transform, false);
                    cell.transform.localPosition = new Vector3(x, y, 0);
                    cell.transform.localScale = Vector3.one * 0.5f;
                    cell.GetComponent<BoxCollider>().isTrigger = true;
                    var body = cell.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    cell.AddComponent<WallCube>();
                }
                data.SetSavedMask(mask);
                data.ApplyCurrentMask();
                string directory = Path.GetFullPath("Temp/WallTemplateValidation/" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                savedPath = Path.Combine(directory, "pose.json");
                savedJson = data.ExportAsJSON();
                File.WriteAllText(savedPath, savedJson);

                var root = new GameObject("Test manager");
                root.SetActive(false);
                manager = root.AddComponent<WallManager>();
                manager.enabled = false; // Explicit spawns make the test independent of frame timing.
                Set(manager, "wallPrefab", template);
                Set(manager, "useSavedMasks", true);
                Set(manager, "savedMaskDirectory", directory);
                Set(manager, "spawnPoint", root.transform);
                root.SetActive(true);

                var hand = new GameObject("Player", typeof(BoxCollider));
                hand.tag = "Player";
                player = hand.GetComponent<BoxCollider>();
                hand.transform.position = template.transform.position;
                Next();
            }
            else if (phase == 1)
            {
                if (EditorApplication.timeSinceStartup - phaseAt < 0.5) return;
                Check(!template.activeInHierarchy, "Scene template is excluded from gameplay contacts.");
                Check(template.GetComponentsInChildren<WallCube>(true).Length == 9,
                    "Overlapping player cannot remove template cells.");
                manager.BeginGame();
                Check(manager.UsesSavedMasks, "Saved pose loaded from isolated test folder.");
                var first = Spawn();
                var cell = first.GetComponentsInChildren<WallCube>().First();
                cell.SendMessage("OnTriggerEnter", player);
                Check(cell.IsHit && cell.transform.parent == null, "Spawned walls still break on contact.");
                Check(first.GetComponent<WallHitTracker>().HitCount == 1, "Contact still counts for scoring.");
                Check(first.GetComponentsInChildren<WallCube>(true).Length == 8, "Only the current wall loses a cell.");
                CheckFresh(Spawn());
                Check(File.ReadAllText(savedPath) == savedJson, "Contact leaves the saved pose unchanged.");
                manager.ResetGame();
                Next();
            }
            else if (phase == 2)
            {
                if (EditorApplication.timeSinceStartup - phaseAt < 0.2) return;
                Check(!Object.FindObjectsByType<WallCube>().Any(), "Retry removes walls and debris.");
                manager.BeginGame();
                CheckFresh(Spawn());
                Check(template.GetComponentsInChildren<WallCube>(true).Length == 9, "Retry retains the complete template.");
                manager.ResetGame();
                // Prefab assets must also keep working when scene templates are hidden.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/2P_Prefab/2P_WallBuilder.prefab");
                Set(manager, "wallPrefab", prefab);
                Set(manager, "useSavedMasks", false);
                manager.BeginGame();
                var wall = Spawn();
                Check(wall.activeInHierarchy && wall.GetComponentsInChildren<WallCube>().Length > 0,
                    "Prefab-backed walls remain visible and playable.");
                Check(wall.GetComponentsInChildren<WallCube>(true).Length == prefab.GetComponentsInChildren<WallCube>(true).Length,
                    "Prefab-backed walls retain all cells.");
                Debug.Log("WALL_TEMPLATE_VALIDATION_PASSED: source contact isolation, breakage, score, next spawn, retry, saved JSON, prefab spawning.");
                Finish(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static GameObject Spawn()
    {
        typeof(WallManager).GetMethod("SpawnWall", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        var walls = (List<GameObject>)typeof(WallManager).GetField("activeWalls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
        return walls.Last();
    }

    private static void CheckFresh(GameObject wall)
    {
        Check(wall.activeInHierarchy, "Spawn from an inactive scene template is active.");
        var cells = wall.GetComponentsInChildren<WallCube>(true);
        Check(cells.Length == 9 && cells.All(c => !c.IsHit), "New wall starts complete and undamaged.");
        Check(wall.GetComponentsInChildren<WallCube>().Length == 8, "Only the saved hole is absent.");
        Check(wall.GetComponent<WallMaskData>().ExportAsJSON() == savedJson, "New wall retains the original saved mask.");
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Next() { phase++; phaseAt = EditorApplication.timeSinceStartup; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
