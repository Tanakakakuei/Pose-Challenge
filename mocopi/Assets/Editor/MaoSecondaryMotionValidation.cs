using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class MaoSecondaryMotionValidation
{
    private const string ModelPath = "Assets/prefab/Models/Mao_MocopiAvatar.prefab";

    public static void RunAll()
    {
        if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene("Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject moving = null, control = null;
        try
        {
            moving = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            control = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            var motion = moving.GetComponent<MaoSecondaryMotion>();
            Check(motion != null, "Capture avatar prefab has secondary motion.");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/MaoMocopiAvatar.prefab").GetComponent<MaoSecondaryMotion>() != null,
                "Generated avatar prefab also has secondary motion.");
            control.GetComponent<MaoSecondaryMotion>().enabled = false;
            var settings = new SerializedObject(motion);
            settings.FindProperty("airMovement").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var animated = moving.GetComponent<Animator>();
            var reference = control.GetComponent<Animator>();
            var rotations = new Dictionary<Transform, Quaternion>();
            var positions = new Dictionary<Transform, Vector3>();
            foreach (var bone in moving.GetComponentsInChildren<Transform>())
            {
                rotations[bone] = bone.localRotation;
                positions[bone] = bone.localPosition;
            }
            var comparisonBones = BonesByName(control);
            Quaternion headRest = animated.GetBoneTransform(HumanBodyBones.Head).localRotation;
            Transform referenceHead = reference.GetBoneTransform(HumanBodyBones.Head);
            Vector3 faceForwardInHead = referenceHead.InverseTransformDirection(control.transform.forward);
            Transform fringeReference = comparisonBones["fronthair3"];
            Vector3 fringeTip = fringeReference.InverseTransformVector((fringeReference.position - fringeReference.parent.position) * 0.65f);
            float maximumSway = 0;
            float maximumFringeSway = 0;
            motion.ResetSimulation();
            Check(motion.SimulatedBoneCount == 30, $"The 29 accessory bones plus fringe tip are selected; fringe root and Jaw stay fixed (found {motion.SimulatedBoneCount}).");
            var rates = new List<Quaternion>();
            foreach (int fps in new[] { 30, 60, 120 })
            {
                moving.transform.position = control.transform.position = Vector3.zero;
                animated.GetBoneTransform(HumanBodyBones.Head).localRotation = headRest;
                reference.GetBoneTransform(HumanBodyBones.Head).localRotation = headRest;
                motion.ResetSimulation();
                for (int frame = 1; frame <= fps * 3; frame++)
                {
                    float t = frame / (float)fps;
                    Vector3 position = new Vector3(0.08f * Mathf.Sin(t * 4), 0.025f * Mathf.Sin(t * 2), 0.09f * Mathf.Sin(t * 3));
                    Quaternion rotation = headRest * Quaternion.Euler(Mathf.Sin(t * 2) * 12, Mathf.Sin(t * 1.5f) * 10, Mathf.Sin(t * 3) * 15);
                    moving.transform.position = control.transform.position = position;
                    animated.GetBoneTransform(HumanBodyBones.Head).localRotation = rotation;
                    reference.GetBoneTransform(HumanBodyBones.Head).localRotation = rotation;
                    motion.Simulate(1f / fps);
                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        var a = animated.GetBoneTransform((HumanBodyBones)i);
                        var b = reference.GetBoneTransform((HumanBodyBones)i);
                        if (a != null && b != null) Check(Quaternion.Angle(a.localRotation, b.localRotation) < 0.01f &&
                            Quaternion.Angle(a.rotation, b.rotation) < 0.05f,
                            "Secondary motion never modifies humanoid animation bones.");
                    }
                    foreach (var bone in moving.GetComponentsInChildren<Transform>())
                    {
                        if (bone == moving.transform) continue;
                        Check((bone.localPosition - positions[bone]).sqrMagnitude < 0.0000001f, "Bone lengths/positions remain unchanged.");
                        Check(!float.IsNaN(bone.rotation.x) && !float.IsInfinity(bone.rotation.x), "No unstable rotations.");
                        if (!comparisonBones.TryGetValue(bone.name, out var other)) continue;
                        if (bone.name == "fronthair1" || bone.name == "fronthair2")
                            Check(Quaternion.Angle(bone.rotation, other.rotation) < 0.05f, "Fringe root and middle never receive secondary motion.");
                        if (bone.name == "fronthair3")
                        {
                            float fringeAngle = Quaternion.Angle(bone.rotation, other.rotation);
                            maximumFringeSway = Mathf.Max(maximumFringeSway, fringeAngle);
                            Check(fringeAngle <= 3.05f, "Only a small 3 degree fringe tip swing is allowed.");
                            Vector3 faceForward = referenceHead.TransformDirection(faceForwardInHead);
                            foreach (var probe in new[] { fringeTip, fringeTip + Vector3.up * 0.02f, fringeTip + Vector3.forward * 0.02f })
                                Check(Mathf.Abs(Vector3.Dot(bone.TransformPoint(probe) - other.TransformPoint(probe), faceForward)) < 0.00002f,
                                    "Fringe tip rotation preserves front/back depth, even while the head turns and pitches.");
                        }
                        if (bone.name.Contains("Twin") || bone.name.StartsWith("apron") || bone.name.StartsWith("fronthair"))
                        {
                            float angle = Quaternion.Angle(bone.rotation, other.rotation);
                            maximumSway = Mathf.Max(maximumSway, angle);
                            Check(angle <= 24.1f, "Hair and clothing stay within the configured angle limits.");
                        }
                    }
                }
                rates.Add(BonesByName(moving)["LeftTwin3"].rotation);
            }
            Check(maximumSway > 0.5f, "Body movement produces measurable accessory sway.");
            Check(maximumFringeSway > 0.1f, "Fringe tip has a small measurable sideways response.");
            Check(Quaternion.Angle(rates[0], rates[2]) < 4f && Quaternion.Angle(rates[1], rates[2]) < 4f,
                "30/60/120 FPS have similar results.");
            // Teleports and scale changes reset inertia, preserving the authored accessory pose.
            moving.transform.position += Vector3.right * 4;
            motion.Simulate(1f / 60f);
            Check(Quaternion.Angle(BonesByName(moving)["LeftTwin3"].localRotation,
                rotations[BonesByName(moving)["LeftTwin3"]]) < 0.01f, "Teleport resets secondary motion.");
            moving.transform.localScale *= 0.5f;
            motion.Simulate(1f / 60f);
            motion.ResetSimulation();
            foreach (var bone in moving.GetComponentsInChildren<Transform>())
                if (bone.name.Contains("Twin") || bone.name.StartsWith("apron"))
                    Check(Quaternion.Angle(bone.localRotation, rotations[bone]) < 0.01f, "Reset restores accessory rest rotations.");
            Debug.Log($"MAO_SECONDARY_MOTION_PASSED: 30 bones, fixed fringe roots/Jaw, sideways tip only ({maximumFringeSway:0.00} deg), zero forward/back deflection, fixed lengths, bounded sway ({maximumSway:0.0} deg), frame rates, teleport and scale reset.");
        }
        finally
        {
            if (moving != null) Object.DestroyImmediate(moving);
            if (control != null) Object.DestroyImmediate(control);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Dictionary<string, Transform> BonesByName(GameObject root)
    {
        var result = new Dictionary<string, Transform>();
        foreach (var bone in root.GetComponentsInChildren<Transform>()) result[bone.name] = bone;
        return result;
    }

    [MenuItem("Tools/mocopi/Render Mao Sway Comparison")]
    public static void RenderPreview()
    {
        RenderComparison(false);
    }

    [MenuItem("Tools/mocopi/Render Mao Fringe Tip Comparison")]
    public static void RenderFringePreview()
    {
        RenderComparison(true);
    }

    private static void RenderComparison(bool portrait)
    {
        RunAll();
        string folder = portrait ? "Logs/MaoFringeFrames" : "Logs/MaoSwayFrames";
        Directory.CreateDirectory(folder);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            var models = new GameObject[2];
            var heads = new Transform[2];
            var headRest = new Quaternion[2];
            var motions = new MaoSecondaryMotion[2];
            for (int i = 0; i < 2; i++)
            {
                models[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                foreach (var part in models[i].GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 31;
                var animator = models[i].GetComponent<Animator>();
                foreach (var side in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm })
                {
                    var arm = animator.GetBoneTransform(side);
                    var lower = animator.GetBoneTransform(side == HumanBodyBones.LeftUpperArm ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    Vector3 desired = Vector3.down + (side == HumanBodyBones.LeftUpperArm ? Vector3.left : Vector3.right) * 0.25f;
                    arm.rotation = Quaternion.FromToRotation(lower.position - arm.position, desired) * arm.rotation;
                }
                heads[i] = animator.GetBoneTransform(HumanBodyBones.Head);
                headRest[i] = heads[i].localRotation;
                motions[i] = models[i].GetComponent<MaoSecondaryMotion>();
                motions[i].enabled = false;
            }
            var camera = new GameObject("Sway Preview Camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
            camera.orthographic = true;
            camera.orthographicSize = portrait ? 0.38f : 1.08f;
            float cameraHeight = portrait ? 1.64f : 1f;
            camera.transform.position = new Vector3(0, cameraHeight, 5);
            camera.transform.LookAt(new Vector3(0, cameraHeight, 0));
            var light = new GameObject("Sway Preview Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            RenderSettings.fog = false;
            target = RenderTexture.GetTemporary(960, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            for (int frame = -120; frame < 240; frame++)
            {
                float t = frame / 60f;
                for (int i = 0; i < 2; i++)
                {
                    float spacing = portrait ? 0.29f : 0.65f;
                    models[i].transform.position = new Vector3((i == 0 ? -spacing : spacing) + Mathf.Sin(t * Mathf.PI) * (portrait ? 0.02f : 0.09f),
                        Mathf.Sin(t * Mathf.PI * 2) * 0.018f, 0);
                    models[i].transform.rotation = Quaternion.Euler(0, Mathf.Sin(t * Mathf.PI) * 23, Mathf.Sin(t * Mathf.PI) * 2);
                    heads[i].localRotation = headRest[i] * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI + 0.7f) * 8);
                }
                motions[1].Simulate(1f / 60f);
                if (frame < 0 || frame % 3 != 0) continue;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes($"{folder}/frame_{frame / 3:000}.png", image.EncodeToPNG());
            }
            Debug.Log("MAO_SWAY_PREVIEW_PASSED: left is fixed accessories, right is secondary motion.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (image != null) Object.DestroyImmediate(image);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Mao sway validation: " + message);
    }
}
