using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArenaMusicSetup
{
    public const string ClipPath = "Assets/Audio/Tick Tock Tantrum.mp3";
    private const string ApplyRequest = "Temp/ArenaMusicUpgrade.request";
    [InitializeOnLoadMethod]
    private static void WatchRequestedUpgrade()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update -= ApplyRequestedUpgrade;
        EditorApplication.update += ApplyRequestedUpgrade;
    }
    private static void ApplyRequestedUpgrade()
    {
        if (!System.IO.File.Exists(ApplyRequest) || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath) return;
        EditorApplication.update -= ApplyRequestedUpgrade;
        string backup = "Assets/Editor/GameModeBeforeMusic-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        try
        {
            // Save a copy of the live scene, including edits not yet present on disk.
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not back up the live scene.");
            Apply();
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/arena-music-live-apply.txt", "ARENA_MUSIC_LIVE_APPLIED\nBackup: " + backup + "\nApplied: " + DateTime.Now);
            System.IO.File.Delete(ApplyRequest);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/arena-music-live-apply.txt", "ARENA_MUSIC_APPLY_FAILED\n" + error);
        }
    }
    [MenuItem("Tools/mocopi/Apply Music and Audio Floor")]
    public static void Apply()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath) throw new InvalidOperationException("Open GameMode first.");
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var arenas = loop.GetComponent<PlayerArenaIsolation>();
        if (arenas == null || arenas.environments.Length != 2) throw new InvalidOperationException("Configure the independent player arenas first.");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null) throw new InvalidOperationException("Soundtrack is missing.");
        Shader trails = Shader.Find("Mocopi/Arena Speed Lines"), floor = Shader.Find("Mocopi/Arena Audio Floor"), halo = Shader.Find("Mocopi/Arena Electric Halo");
        if (trails == null || floor == null || halo == null || ShaderUtil.ShaderHasError(trails) || ShaderUtil.ShaderHasError(floor) || ShaderUtil.ShaderHasError(halo))
            throw new InvalidOperationException("Arena shaders did not compile.");
        var director = loop.GetComponent<GameMusicDirector>();
        if (director == null) director = Undo.AddComponent<GameMusicDirector>(loop.gameObject);
        Undo.RecordObject(director, "Set game soundtrack");
        director.controller = loop; director.music = clip; director.fadeOutSeconds = 3;
        var source = director.GetComponent<AudioSource>();
        Undo.RecordObject(source, "Configure shared soundtrack");
        source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.clip = clip; source.volume = 0;
        for (int i = 0; i < arenas.environments.Length; i++)
        {
            var parent = arenas.environments[i].transform;
            var root = parent.Find("Music Visuals");
            if (root == null)
            {
                root = new GameObject("Music Visuals").transform;
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Create audio floor");
                root.SetParent(parent, false);
                var avatar = loop.Players[i].CurrentMocopiAvatar.transform;
                root.position = new Vector3(avatar.position.x, 0, avatar.position.z);
                var forward = Vector3.ProjectOnPlane(loop.Players[i].PlayerCamera.transform.forward, Vector3.up).normalized;
                root.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            var visuals = root.GetComponent<ArenaMusicVisuals>();
            if (visuals == null) visuals = Undo.AddComponent<ArenaMusicVisuals>(root.gameObject);
            Undo.RecordObject(visuals, "Configure circular electric floor");
            visuals.music = director; visuals.trailShader = trails; visuals.floorShader = floor; visuals.haloShader = halo;
            visuals.maximumBarHeight = 0.22f;
            var manager = new SerializedObject(loop.Players[i].WallManager);
            var wall = (GameObject)manager.FindProperty("wallPrefab").objectReferenceValue;
            var renderers = wall.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Wall has no visible geometry to measure.");
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visuals.circleDiameter = bounds.size.x * 1.15f;
            Debug.Log($"Player {i+1}: wall width {bounds.size.x:0.00}m, electric circle diameter {visuals.circleDiameter:0.00}m.");
            PlayerArenaLayers.Assign(root.gameObject, PlayerArenaLayers.One + i);
            EditorUtility.SetDirty(visuals);
        }
        EditorUtility.SetDirty(director); EditorUtility.SetDirty(source);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save GameMode.");
        AssetDatabase.SaveAssets();
        Debug.Log($"ARENA_MUSIC_SETUP_PASSED: {clip.name}, {clip.length:0.0}s, shared 2D soundtrack, 3s fade, isolated spectral floors and flat light trails.");
    }
    public static void ApplyBatch() { EditorSceneManager.OpenScene(GameModeValidation.ScenePath); Apply(); }
}
