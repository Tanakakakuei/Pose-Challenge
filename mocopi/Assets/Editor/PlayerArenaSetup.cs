using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerArenaSetup
{
    [MenuItem("Tools/mocopi/Separate Player Arenas")]
    public static void Apply()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath) throw new InvalidOperationException("Open GameMode first.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        for (int i = 0; i < 2; i++)
        {
            var layer = layers.GetArrayElementAtIndex(PlayerArenaLayers.One + i);
            string name = $"Player{i + 1}Arena";
            if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != name) throw new InvalidOperationException("Arena layer is already in use.");
            layer.stringValue = name;
        }
        tags.ApplyModifiedProperties();
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var view = Object.FindFirstObjectByType<GameModePresentation>();
        var isolation = loop.GetComponent<PlayerArenaIsolation>();
        if (isolation == null)
        {
            isolation = Undo.AddComponent<PlayerArenaIsolation>(loop.gameObject);
            isolation.controller = loop;
            isolation.environments = new GameObject[2];
            Vector3 delta = loop.Players[1].CurrentMocopiAvatar.transform.position - loop.Players[0].CurrentMocopiAvatar.transform.position;
            for (int i = 0; i < 2; i++)
            {
                var world = new GameObject($"Player{i + 1}Arena");
                Undo.RegisterCreatedObjectUndo(world, "Separate player arenas");
                world.transform.position = i == 0 ? Vector3.zero : delta;
                isolation.environments[i] = new GameObject("Environment");
                isolation.environments[i].transform.SetParent(world.transform, false);
            }
            var stage = GameObject.Find("NeonArena");
            var ground = GameObject.Find("Plane");
            if (stage == null || ground == null) throw new InvalidOperationException("Stage or ground is missing.");
            var environmentalObjects = new[] { stage, ground }.Concat(Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(light => !light.transform.IsChildOf(stage.transform)).Select(light => light.gameObject)).Distinct().ToArray();
            foreach (var item in environmentalObjects)
            {
                Undo.SetTransformParent(item.transform, isolation.environments[0].transform, "Separate player arenas");
                var clone = Object.Instantiate(item, isolation.environments[1].transform, true);
                clone.name = item.name;
                clone.transform.position += delta;
            }
        }
        var loopSettings = new SerializedObject(loop);
        for (int i = 0; i < 2; i++)
        {
            int layer = PlayerArenaLayers.One + i;
            var environment = isolation.environments[i];
            var world = environment.transform.parent;
            environment.SetActive(true);
            PlayerArenaLayers.Assign(environment, layer);
            foreach (var light in environment.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << layer;
            var player = loop.Players[i];
            var settings = loopSettings.FindProperty("players").GetArrayElementAtIndex(i);
            var roots = settings.FindPropertyRelative("avatarRoots");
            for (int avatar = 0; avatar < roots.arraySize; avatar++)
                Move((GameObject)roots.GetArrayElementAtIndex(avatar).objectReferenceValue, world, layer);
            Move(player.PlayerCamera.gameObject, world, layer);
            Move(player.WallManager.gameObject, world, layer);
            var spawn = settings.FindPropertyRelative("avatarSpawnPoint").objectReferenceValue as Transform;
            if (spawn != null) Move(spawn.gameObject, world, layer);
            var orbs = settings.FindPropertyRelative("readyHandOrbVisualizer").objectReferenceValue as ReadyHandOrbVisualizer;
            if (orbs != null) Move(orbs.gameObject, world, layer);
            player.PlayerCamera.cullingMask = 1 << layer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.PlayerCamera);
            var manager = new SerializedObject(player.WallManager);
            manager.FindProperty("poseMaskAnimator").objectReferenceValue = player.CurrentMocopiAvatar.GetComponent<Animator>();
            manager.ApplyModifiedProperties();
            foreach (var station in view.stations.Where(station => station.playerIndex == i)) PlayerArenaLayers.Assign(station.root.gameObject, layer);
        }
        EditorUtility.SetDirty(isolation);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("PLAYER_ARENAS_SETUP_PASSED: duplicated stage, floor and lighting; isolated cameras, avatars, walls and touch panels.");
    }
    private static void Move(GameObject target, Transform parent, int layer)
    {
        Undo.SetTransformParent(target.transform, parent, "Separate player arenas");
        PlayerArenaLayers.Assign(target, layer);
    }
    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene(GameModeValidation.ScenePath);
        Apply();
    }
}
