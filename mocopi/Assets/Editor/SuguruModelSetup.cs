using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Connects the supplied Suguru textures to persistent FBX material remaps.</summary>
public static class SuguruModelSetup
{
    private const string Root = "Assets/Models/suguru/";
    [MenuItem("Tools/mocopi/Configure Suguru Materials")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var textures = new Dictionary<string, string>
        {
            { "Mt_clothes", "FukuTEX" }, { "Mt_Face", "FaceTex" },
            { "Mt_Hair", "Hair_TEX2" }, { "Mt_others", "othersTEX" },
            { "Mt_nameplate", "nameplate" }, { "Mt_string", "string" }
        };
        var importer = (ModelImporter)AssetImporter.GetAtPath(CoopSetup.ModelPath);
        var sources = AssetDatabase.LoadAllAssetsAtPath(CoopSetup.ModelPath).OfType<Material>().ToArray();
        var shader = Shader.Find("Mocopi/Mao Soft Toon");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Soft Toon shader unavailable.");
        Directory.CreateDirectory(Root + "Materials");
        AssetDatabase.Refresh();
        foreach (var source in sources)
        {
            Texture2D texture = null;
            if (textures.TryGetValue(source.name, out string textureName))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + textureName + ".png");
                if (texture == null) throw new InvalidOperationException("Missing texture: " + textureName);
            }
            else if (source.name != "Mt_clip") throw new InvalidOperationException("Unmapped material: " + source.name);
            string path = Root + "Materials/" + source.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.mainTexture = texture;
            // Clips are solid metal parts, with no supplied texture atlas of their own.
            material.color = source.name == "Mt_clip" ? new Color(0.65f, 0.68f, 0.72f) : Color.white;
            bool face = source.name == "Mt_Face";
            material.SetFloat("_FaceLight", face ? 0.65f : 0.08f);
            material.SetFloat("_ShadowStrength", face ? 0.18f : 0.6f);
            material.SetFloat("_ShadeSoftness", face ? 0.22f : 0.065f);
            material.SetFloat("_OutlineWidth", face ? 0f : 0.65f);
            material.SetFloat("_SpecStrength", source.name == "Mt_Hair" ? 0.16f : 0.035f);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(CoopSetup.ModelPath);
        var animator = model.GetComponent<Animator>();
        if (animator == null || !animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Suguru humanoid is invalid.");
        var lines = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Select(m =>
        {
            if (m == null || m.shader != shader || (m.name != "Mt_clip" && m.mainTexture == null)) throw new InvalidOperationException("Invalid Suguru material.");
            return m.name + ": " + (m.mainTexture != null ? m.mainTexture.name : "solid metal") + " -> " + AssetDatabase.GetAssetPath(m);
        }).ToArray();
        Directory.CreateDirectory("output/suguru-update");
        RenderPreview(model);
        File.WriteAllLines("output/suguru-update/validation.txt", new[] { "PASS: valid Humanoid; persistent material remaps; all six supplied textures assigned; shader has no import errors." }.Concat(lines));
        ValidateScenes();
        SceneView.RepaintAll();
        Debug.Log("SUGURU_MATERIALS_PASSED");
    }

    private static void ValidateScenes()
    {
        foreach (string path in new[] { GameModeValidation.ScenePath, GameModeValidation.ScenePath.Replace("GameMode.unity", "GameMode-Halloween.unity") })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                var loop = components.OfType<MocopiLocalGameLoopController>().Single();
                var player = loop.Players[1];
                var avatar = player.CurrentMocopiAvatar;
                var animator = avatar.GetComponent<Animator>();
                if (avatar.name != "2P_suguru" || !animator.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException(path + ": invalid 2P avatar");
                if (!components.OfType<Mocopi.Receiver.MocopiSimpleReceiver>().Any(r => r.AvatarSettings.Any(a => a.MocopiAvatar == avatar)))
                    throw new InvalidOperationException(path + ": receiver is disconnected");
                if (avatar.GetComponent<MocopiPlayerColliderRig>() == null || avatar.GetComponentsInChildren<Collider>(true).Length < 10)
                    throw new InvalidOperationException(path + ": body colliders missing");
                foreach (var material in avatar.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
                    if (material == null || !AssetDatabase.GetAssetPath(material).StartsWith(Root + "Materials/") || (material.name != "Mt_clip" && material.mainTexture == null))
                        throw new InvalidOperationException(path + ": material override missing texture");
                if (player.ReadyPoseAnimator != animator) throw new InvalidOperationException(path + ": ready pose target differs");
                File.AppendAllText("output/suguru-update/validation.txt", "PASS: " + path + ": 2P Suguru, textures, humanoid, receiver, body colliders and ready pose references.\n");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
    }

    private static void RenderPreview(GameObject model)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var instance = Object.Instantiate(model);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
            var bounds = instance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a,b) => { a.Encapsulate(b); return a; });
            var cameraObject = new GameObject("Suguru preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.21f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20;
            var lightObject = new GameObject("Suguru preview light");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(30, 145, 0);
            target = RenderTexture.GetTemporary(1024, 1024, 24);
            camera.targetTexture = target;
            foreach (int direction in new[] { 1, -1 })
            {
                camera.transform.position = bounds.center + new Vector3(0, 0, direction * 4f);
                camera.transform.LookAt(bounds.center);
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                image.Apply();
                File.WriteAllBytes("output/suguru-update/" + (direction == 1 ? "front" : "back") + ".png", image.EncodeToPNG());
                Object.DestroyImmediate(image); image = null;
            }
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
