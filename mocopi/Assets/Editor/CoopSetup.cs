using System;
using System.Linq;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CoopSetup
{
    public const string ModelPath = "Assets/Models/suguru/suguru_03.fbx";
    [MenuItem("Tools/mocopi/Configure Suguru and Co-op")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var original = EditorSceneManager.GetSceneManagerSetup();
        if (original.Any(s => s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
            throw new InvalidOperationException("Save the open scene before applying the co-op setup.");
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
        }
        try
        {
            foreach (string path in new[] { GameModeValidation.ScenePath, GameModeValidation.ScenePath.Replace("GameMode.unity", "GameMode-Halloween.unity") })
            {
                var scene = EditorSceneManager.OpenScene(path);
                ConfigureScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
        Debug.Log("COOP_SETUP_PASSED");
    }

    private static void ConfigureScene()
    {
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var p1 = loop.Players[0]; var p2 = loop.Players[1];
        var old = p2.CurrentMocopiAvatar;
        var avatar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        avatar.name = "2P_suguru";
        avatar.transform.SetParent(old.transform.parent, false);
        avatar.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
        var animator = avatar.GetComponent<Animator>();
        if (animator == null || !animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("suguru must have a valid humanoid rig.");
        var mocopi = avatar.AddComponent<MocopiAvatar>();
        EditorUtility.CopySerialized(old, mocopi);
        var rig = avatar.AddComponent<MocopiPlayerColliderRig>();
        EditorUtility.CopySerialized(p1.CurrentMocopiAvatar.GetComponent<MocopiPlayerColliderRig>(), rig);
        PlayerArenaLayers.Assign(avatar, PlayerArenaLayers.Two);
        rig.RebuildColliders();
        var settings = new SerializedObject(loop);
        var slot = settings.FindProperty("players").GetArrayElementAtIndex(1);
        var roots = slot.FindPropertyRelative("avatarRoots"); roots.arraySize = 1;
        roots.GetArrayElementAtIndex(0).objectReferenceValue = avatar;
        slot.FindPropertyRelative("avatarPrefabs").arraySize = 0;
        slot.FindPropertyRelative("readyPoseAnimator").objectReferenceValue = animator;
        var orbs = slot.FindPropertyRelative("readyHandOrbVisualizer").objectReferenceValue as ReadyHandOrbVisualizer;
        if (orbs != null) orbs.SetTargetAnimator(animator);
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var receiver in Object.FindObjectsByType<MocopiSimpleReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var entry in receiver.AvatarSettings) if (entry.MocopiAvatar == old) entry.MocopiAvatar = mocopi;
            EditorUtility.SetDirty(receiver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(receiver);
        }
        var follow = p2.PlayerCamera.GetComponent<MocopiFirstPersonCamera>();
        if (follow != null) { follow.SetTargetAnimator(animator); EditorUtility.SetDirty(follow); }
        var one = new SerializedObject(p1.WallManager); var two = new SerializedObject(p2.WallManager);
        foreach (string name in new[] { "moveSpeed", "destroyDistance", "spawnInterval", "randomizeLetters", "firstLetter", "useSavedMasks", "savedMaskDirectory", "useMocopiPoseMask", "wallCount", "safeHitThreshold", "pauseOnResult", "playOnStart", "clearWallsOnReset", "controlledByGameLoop" })
            two.CopyFromSerializedProperty(one.FindProperty(name));
        var sourceTemplate = (GameObject)one.FindProperty("wallPrefab").objectReferenceValue;
        var oldTemplate = (GameObject)two.FindProperty("wallPrefab").objectReferenceValue;
        // WallManager assigns the owning player's layer to each spawned copy.
        // Sharing the source keeps the geometry and saved-mask dimensions identical.
        var template = sourceTemplate;
        two.FindProperty("wallPrefab").objectReferenceValue = template;
        two.FindProperty("poseMaskAnimator").objectReferenceValue = animator;
        var spawn1 = (Transform)one.FindProperty("spawnPoint").objectReferenceValue;
        var spawn2 = (Transform)two.FindProperty("spawnPoint").objectReferenceValue;
        if (spawn1 == null || EditorUtility.IsPersistent(spawn1)) throw new InvalidOperationException("1P requires a scene wall spawn point.");
        spawn1.SetParent(p1.WallManager.transform.parent, true);
        if (spawn2 == null || EditorUtility.IsPersistent(spawn2))
        {
            spawn2 = new GameObject("2P_WallSpawnPoint").transform;
            spawn2.SetParent(p2.WallManager.transform.parent, false);
            two.FindProperty("spawnPoint").objectReferenceValue = spawn2;
        }
        spawn2.SetParent(p2.WallManager.transform.parent, true);
        spawn2.SetPositionAndRotation(spawn1.position + p2.WallManager.transform.parent.position - p1.WallManager.transform.parent.position, spawn1.rotation);
        EditorUtility.SetDirty(spawn2);
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawn2);
        two.ApplyModifiedPropertiesWithoutUndo();
        if (oldTemplate != null && oldTemplate.scene.IsValid() && oldTemplate != sourceTemplate) Object.DestroyImmediate(oldTemplate);
        Object.DestroyImmediate(old.gameObject);
        Debug.Log($"COOP_SCENE_CONFIGURED {loop.gameObject.scene.path} avatar={animator.isHuman} template={template.transform.childCount}");
    }
}
