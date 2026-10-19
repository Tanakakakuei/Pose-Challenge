using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports the supplied Mao model and connects it to the active capture scene.</summary>
public static class MaoAvatarSetup
{
    private const string ModelPath = "Assets/Models/Mao/Mao.fbx";
    private const string PrefabPath = "Assets/prefab/MaoMocopiAvatar.prefab";

    [MenuItem("Tools/mocopi/Use Mao as GameMode Default")]
    public static void ApplyToGameScene()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before changing the default avatar.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != GameModeValidation.ScenePath)
            throw new InvalidOperationException("Open GameMode before applying Mao.");
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Models/Mao_MocopiAvatar.prefab");
        ValidateAvatar(prefab);
        for (int i = 0; i < loop.Players.Count; i++)
        {
            var settings = new SerializedObject(loop);
            var roots = settings.FindProperty("players").GetArrayElementAtIndex(i).FindPropertyRelative("avatarRoots");
            var oldRoot = roots.GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
            if (oldRoot.GetComponent<MaoSecondaryMotion>() != null) continue;
            var oldAvatar = oldRoot.GetComponentInChildren<MocopiAvatar>(true);
            var oldAnimator = oldAvatar.GetComponent<Animator>();
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(replacement, "Use Mao as default avatar");
            replacement.name = $"{i + 1}P_Mao";
            replacement.transform.SetParent(oldRoot.transform.parent, false);
            replacement.transform.localPosition = oldRoot.transform.localPosition;
            replacement.transform.localRotation = oldRoot.transform.localRotation;
            // Keep Mao's calibrated 1.6 scale and collider settings, shared with CaptureMode.
            replacement.tag = oldRoot.tag;
            replacement.layer = oldRoot.layer;
            replacement.SetActive(oldRoot.activeSelf);
            var avatar = replacement.GetComponent<MocopiAvatar>();
            avatar.MotionSmoothness = oldAvatar.MotionSmoothness;
            avatar.IsBufferingEnabled = oldAvatar.IsBufferingEnabled;
            avatar.DelayRecoveryRate = oldAvatar.DelayRecoveryRate;
            var animator = replacement.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var references = new Dictionary<Object, Object>
            {
                [oldRoot] = replacement, [oldRoot.transform] = replacement.transform,
                [oldAvatar] = avatar, [oldAnimator] = animator
            };
            var oldRig = oldRoot.GetComponent<MocopiPlayerColliderRig>();
            if (oldRig != null) references[oldRig] = replacement.GetComponent<MocopiPlayerColliderRig>();
            for (int bone = 0; bone < (int)HumanBodyBones.LastBone; bone++)
            {
                var before = oldAnimator.GetBoneTransform((HumanBodyBones)bone);
                var after = animator.GetBoneTransform((HumanBodyBones)bone);
                if (before == null || after == null) continue;
                references[before] = after;
                references[before.gameObject] = after.gameObject;
            }
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component.transform.IsChildOf(oldRoot.transform)) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null ||
                        !references.TryGetValue(property.objectReferenceValue, out var mapped)) continue;
                    if (!changed) Undo.RecordObject(component, "Connect default Mao");
                    property.objectReferenceValue = mapped;
                    changed = true;
                }
                if (!changed) continue;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            Undo.DestroyObjectImmediate(oldRoot);
        }
        foreach (var player in loop.Players)
        {
            var avatar = player.CurrentMocopiAvatar;
            ValidateAvatar(avatar.gameObject);
            if (avatar.GetComponent<MaoSecondaryMotion>() == null || avatar.GetComponent<MocopiPlayerColliderRig>() == null)
                throw new InvalidOperationException("Mao motion or collider rig is missing.");
            bool connected = Object.FindObjectsByType<MocopiSimpleReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(receiver => receiver.AvatarSettings.Any(setting => setting.MocopiAvatar == avatar));
            if (!connected) throw new InvalidOperationException("Receiver is not bound to " + avatar.name);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("GAME_MAO_DEFAULT_PASSED: both initial avatars are Mao; receiver, humanoid, colliders, secondary motion and materials verified.");
    }

    public static void ApplyToGameSceneBatch()
    {
        EditorSceneManager.OpenScene(GameModeValidation.ScenePath);
        ApplyToGameScene();
    }

    [MenuItem("Tools/mocopi/Apply Mao to Capture Scene")]
    public static void ApplyToCaptureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before applying Mao.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity")
            throw new InvalidOperationException("Open CaptureMaskDataMode before applying Mao.");
        var mode = Object.FindAnyObjectByType<CaptureMaskDataMode>();
        var modeSettings = new SerializedObject(mode);
        var oldAvatar = modeSettings.FindProperty("mocopiAvatar").objectReferenceValue as MocopiAvatar;
        if (oldAvatar == null) throw new InvalidOperationException("The capture avatar is missing.");

        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();
        Directory.CreateDirectory("Assets/Models/Mao/Textures");
        importer.ExtractTextures("Assets/Models/Mao/Textures");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var modelAnimator = model.GetComponent<Animator>();
        if (modelAnimator == null || modelAnimator.avatar == null || !modelAnimator.avatar.isValid || !modelAnimator.avatar.isHuman)
            throw new InvalidOperationException("Mao could not be imported as a valid Humanoid. Check its Rig mapping.");

        // Explicit material assets keep the embedded FBX textures connected after moving the project.
        Directory.CreateDirectory("Assets/Models/Mao/Materials");
        AssetDatabase.Refresh();
        foreach (var source in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>())
        {
            string path = $"Assets/Models/Mao/Materials/{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = source.name };
                AssetDatabase.CreateAsset(material, path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Models/Mao/Textures/{source.name}_BaseColor.png");
            if (texture == null) throw new InvalidOperationException($"Missing Mao texture: {source.name}");
            material.mainTexture = texture;
            material.color = Color.white;
            material.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        var template = Object.Instantiate(model);
        template.name = "MaoMocopiAvatar";
        try
        {
            var animator = template.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            template.AddComponent<MocopiAvatar>();
            template.AddComponent<MaoSecondaryMotion>();
            ValidateAvatar(template);
            PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
        }
        finally { Object.DestroyImmediate(template); }

        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        Undo.RegisterCreatedObjectUndo(replacement, "Apply Mao avatar");
        replacement.name = oldAvatar.name;
        replacement.transform.SetParent(oldAvatar.transform.parent, false);
        replacement.transform.localPosition = oldAvatar.transform.localPosition;
        replacement.transform.localRotation = oldAvatar.transform.localRotation;
        replacement.transform.localScale = oldAvatar.transform.localScale;
        replacement.tag = oldAvatar.tag;
        replacement.layer = oldAvatar.gameObject.layer;
        var newAvatar = replacement.GetComponent<MocopiAvatar>();
        var oldAnimator = oldAvatar.GetComponent<Animator>();
        var newAnimator = replacement.GetComponent<Animator>();
        var references = new Dictionary<Object, Object>
        {
            [oldAvatar] = newAvatar, [oldAvatar.gameObject] = replacement,
            [oldAvatar.transform] = replacement.transform, [oldAnimator] = newAnimator
        };
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var oldBone = oldAnimator.GetBoneTransform((HumanBodyBones)i);
            var newBone = newAnimator.GetBoneTransform((HumanBodyBones)i);
            if (oldBone != null && newBone != null)
            {
                references[oldBone] = newBone;
                references[oldBone.gameObject] = newBone.gameObject;
            }
        }
        foreach (var root in scene.GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component.transform.IsChildOf(oldAvatar.transform)) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                if (!references.TryGetValue(property.objectReferenceValue, out var mapped)) continue;
                if (!changed) Undo.RecordObject(component, "Connect Mao avatar");
                property.objectReferenceValue = mapped;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        Undo.DestroyObjectImmediate(oldAvatar.gameObject);
        ValidateAvatar(replacement);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = replacement;
        SceneView.lastActiveSceneView?.FrameSelected();
        File.WriteAllText("Temp/MaoSetupReport.txt", $"MAO_SETUP_PASSED\nScene: {scene.path}\nPrefab: {PrefabPath}\nHumanoid: {newAnimator.isHuman}\nRenderers: {replacement.GetComponentsInChildren<SkinnedMeshRenderer>().Length}\nHuman scale: {newAnimator.humanScale}\n");
        Debug.Log("MAO_SETUP_PASSED: Mao Humanoid, textures, mocopi receiver and capture source are connected.");
    }

    private static void ValidateAvatar(GameObject root)
    {
        var animator = root.GetComponent<Animator>();
        if (animator == null || !animator.isHuman || !animator.avatar.isValid)
            throw new InvalidOperationException("Invalid Mao Humanoid.");
        for (int i = 0; i < HumanTrait.BoneCount; i++)
            if (HumanTrait.RequiredBone(i) && animator.GetBoneTransform((HumanBodyBones)i) == null)
                throw new InvalidOperationException($"Mao is missing required bone {HumanTrait.BoneName[i]}.");
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (renderer.sharedMesh == null || renderer.bones.Any(b => b == null))
                throw new InvalidOperationException($"Invalid skin binding: {renderer.name}");
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.mainTexture == null)
                    throw new InvalidOperationException($"Missing texture on {renderer.name}.");
        }
    }
}
