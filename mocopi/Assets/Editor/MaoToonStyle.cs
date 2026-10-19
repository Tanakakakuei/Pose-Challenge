using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MaoToonStyle
{
    [MenuItem("Tools/mocopi/Style Mao - Soft Toon")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before styling Mao.");
        Shader shader = Shader.Find("Mocopi/Mao Soft Toon");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("The Mao Soft Toon shader has import errors.");
        Directory.CreateDirectory("Temp/MaoToonPreview");
        RenderPreview("Temp/MaoToonPreview/before.png", false);
        foreach (string name in new[] { "Face", "Hair", "Deco", "Upbody", "Lowbody" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Models/Mao/Materials/{name}.mat");
            if (material == null) throw new InvalidOperationException($"Missing Mao material: {name}");
            Undo.RecordObject(material, "Mao soft toon style");
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.renderQueue = -1;
            material.SetColor("_Color", Color.white);
            bool face = name == "Face";
            bool hair = name == "Hair";
            material.SetColor("_ShadeColor", face ? new Color(0.96f, 0.84f, 0.87f) : new Color(0.76f, 0.75f, 0.92f));
            material.SetFloat("_ShadeThreshold", 0.52f);
            material.SetFloat("_ShadeSoftness", face ? 0.22f : 0.065f);
            material.SetFloat("_ShadowStrength", face ? 0.18f : 0.6f);
            material.SetFloat("_FaceLight", face ? 0.65f : 0.08f);
            material.SetFloat("_LightInfluence", 0.15f);
            material.SetColor("_SpecColor", new Color(0.88f, 0.94f, 1f));
            material.SetFloat("_SpecStrength", hair ? 0.16f : face ? 0f : 0.035f);
            material.SetFloat("_SpecPower", hair ? 42f : 30f);
            material.SetColor("_RimColor", new Color(1f, 0.88f, 0.96f));
            material.SetFloat("_RimStrength", face ? 0.035f : hair ? 0.16f : 0.1f);
            material.SetFloat("_RimPower", 3.5f);
            material.SetColor("_OutlineColor", hair ? new Color(0.32f, 0.26f, 0.43f) : new Color(0.25f, 0.31f, 0.39f));
            // Face material also includes eyes/lashes: avoid outlining those internal surfaces.
            material.SetFloat("_OutlineWidth", face ? 0f : hair ? 0.8f : 0.65f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        RenderPreview("Temp/MaoToonPreview/after.png", false);
        RenderPreview("Temp/MaoToonPreview/portrait.png", true);
        if (ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
        SceneView.RepaintAll();
        File.WriteAllText("Temp/MaoToonPreview/result.txt", "MAO_TOON_PASSED: five materials styled; shader rendered without errors.\n");
        Debug.Log("MAO_TOON_PASSED: soft cel shading, face fill, silky hair highlights and fine colored outlines applied.");
    }

    private static void RenderPreview(string path, bool portrait)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/MaoMocopiAvatar.prefab");
        GameObject model = null, cameraObject = null, lightObject = null;
        RenderTexture target = null;
        Texture2D image = null;
        var previousTarget = RenderTexture.active;
        try
        {
            model = Object.Instantiate(prefab);
            model.hideFlags = HideFlags.HideAndDontSave;
            model.transform.position = new Vector3(1000, 0, 1000);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var animator = model.GetComponent<Animator>();
            Vector3 center = portrait ? animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.down * 0.04f : bounds.center;
            cameraObject = new GameObject("Mao style preview camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.97f, 0.94f, 0.96f);
            camera.orthographic = true;
            camera.orthographicSize = portrait ? bounds.size.y * 0.205f : Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.transform.position = center + new Vector3(0.15f, 0.06f, 3f);
            camera.transform.LookAt(center);
            lightObject = new GameObject("Mao style preview light") { hideFlags = HideFlags.HideAndDontSave };
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.94f);
            light.intensity = 1.1f;
            light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35, 155, 0);
            target = RenderTexture.GetTemporary(1024, 1024, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (image != null) Object.DestroyImmediate(image);
            if (model != null) Object.DestroyImmediate(model);
            if (lightObject != null) Object.DestroyImmediate(lightObject);
        }
    }
}
