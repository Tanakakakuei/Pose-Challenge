using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerColliderValidation
{
    private const string PlayKey = "PlayerColliderValidation.Play";
    private static int phase, colliderCount;
    private static double startedAt, changedAt;
    private static SphereCollider playProbe;

    public static void PlayTest()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var parent = new GameObject("Collider validation parent");
        var avatar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Models/Mao_MocopiAvatar.prefab"), parent.transform);
        avatar.layer = PlayerArenaLayers.One;
        SessionState.SetBool(PlayKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void ResumePlayTest()
    {
        if (!SessionState.GetBool(PlayKey, false)) return;
        startedAt = changedAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 45) throw new Exception("Runtime collider validation timed out");
            if (EditorApplication.timeSinceStartup - changedAt < 0.5) return;
            var rig = Object.FindFirstObjectByType<MocopiPlayerColliderRig>();
            if (rig == null) return;
            var animator = rig.GetComponent<Animator>();
            switch (phase)
            {
                case 0:
                    playProbe = new GameObject("Play contact probe").AddComponent<SphereCollider>(); playProbe.radius = 0.001f;
                    rig.transform.parent.localScale = new Vector3(1.2f, 0.8f, 1.7f);
                    rig.transform.parent.rotation = Quaternion.Euler(10, 30, 7);
                    rig.transform.localScale = Vector3.one * 1.6f;
                    break;
                case 1:
                    Verify(rig, animator, playProbe, "automatic runtime scale refresh");
                    Set(rig, "headRadius", 0.041f); Set(rig, "leftHandOffset", new Vector3(0.03f, 0.02f, -0.01f));
                    rig.transform.localScale = Vector3.one * 0.5f;
                    break;
                case 2:
                    Verify(rig, animator, playProbe, "automatic runtime radius/offset refresh");
                    colliderCount = rig.GetComponentsInChildren<Collider>().Length;
                    rig.RebuildColliders(); rig.RebuildColliders();
                    Check(rig.GetComponentsInChildren<Collider>().Length == colliderCount, "No duplicate active contacts during deferred rebuild");
                    break;
                case 3:
                    Check(rig.GetComponentsInChildren<Collider>(true).Length == colliderCount, "Retired runtime colliders are destroyed");
                    Verify(rig, animator, playProbe, "runtime rebuild");
                    Debug.Log("PLAYER_COLLIDER_PLAY_PASSED: automatic scale/radius/offset updates, PhysX surfaces/contact, deferred rebuild cleanup.");
                    FinishPlay(0); return;
            }
            phase++; changedAt = EditorApplication.timeSinceStartup;
        }
        catch (Exception error) { Debug.LogException(error); FinishPlay(1); }
    }

    private static void FinishPlay(int code)
    {
        SessionState.SetBool(PlayKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
    private static readonly (string label, HumanBodyBones from, HumanBodyBones to, string radius)[] Capsules =
    {
        ("Torso", HumanBodyBones.Hips, HumanBodyBones.Chest, "torsoRadius"),
        ("Chest", HumanBodyBones.Chest, HumanBodyBones.UpperChest, "chestRadius"),
        ("Neck", HumanBodyBones.Neck, HumanBodyBones.Head, "neckRadius"),
        ("LeftUpperArm", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, "armRadius"),
        ("LeftLowerArm", HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, "armRadius"),
        ("RightUpperArm", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, "armRadius"),
        ("RightLowerArm", HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, "armRadius"),
        ("LeftUpperLeg", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, "legRadius"),
        ("LeftLowerLeg", HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, "legRadius"),
        ("RightUpperLeg", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, "legRadius"),
        ("RightLowerLeg", HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, "legRadius")
    };
    private static readonly (HumanBodyBones bone, string radius, string offset)[] Spheres =
    {
        (HumanBodyBones.Head, "headRadius", "headOffset"),
        (HumanBodyBones.LeftHand, "handRadius", "leftHandOffset"),
        (HumanBodyBones.RightHand, "handRadius", "rightHandOffset"),
        (HumanBodyBones.LeftFoot, "footRadius", "leftFootOffset"),
        (HumanBodyBones.RightFoot, "footRadius", "rightFootOffset"),
        (HumanBodyBones.UpperChest, "upperChestRadius", "upperChestOffset")
    };

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var parent = new GameObject("Scaled parent");
        var avatar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefab/Models/Mao_MocopiAvatar.prefab"), parent.transform);
        var rig = avatar.GetComponent<MocopiPlayerColliderRig>();
        var animator = avatar.GetComponent<Animator>();
        var probe = new GameObject("Physics contact probe").AddComponent<SphereCollider>();
        probe.radius = 0.001f;
        try
        {
            avatar.layer = PlayerArenaLayers.One;
            rig.RebuildColliders();
            foreach (var sphere in Spheres) Set(rig, sphere.offset, new Vector3(0.023f, -0.017f, 0.031f));
            foreach (Vector3 parentScale in new[] { Vector3.one, Vector3.one * 1.25f, new Vector3(1.2f, 0.8f, 1.7f) })
            foreach (float scale in new[] { 0.5f, 1f, 1.6f, 2f })
            {
                parent.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(13, 32, 8));
                parent.transform.localScale = parentScale;
                avatar.transform.localScale = Vector3.one * scale;
                avatar.transform.localRotation = Quaternion.Euler(12, 21, -7);
                rig.ApplyColliderSettingsToHierarchy();
                Verify(rig, animator, probe, $"parent={parentScale}, avatar={scale}");
            }
            // Radius/offset edits and a pose change must update existing instances without rebuilding.
            var headCollider = Find<SphereCollider>(rig, "Head");
            Set(rig, "headRadius", 0.047f); Set(rig, "armRadius", 0.025f);
            var arm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            arm.localRotation *= Quaternion.Euler(15, 25, 40);
            rig.ApplyColliderSettingsToHierarchy();
            Check(headCollider == Find<SphereCollider>(rig, "Head"), "Setting edits preserve collider identity");
            Verify(rig, animator, probe, "edited pose and radii");
            int count = rig.GetComponentsInChildren<Collider>().Count(c => c.name.EndsWith("_PlayerCollider"));
            Set(rig, "clearExistingGeneratedColliders", false);
            rig.RebuildColliders(); rig.RebuildColliders();
            Check(count == rig.GetComponentsInChildren<Collider>().Count(c => c.name.EndsWith("_PlayerCollider")), "Repeated rebuild does not duplicate contacts");
            ValidateMouse(parent.transform, probe);
            Debug.Log("PLAYER_COLLIDER_METRES_PASSED: actual PhysX surfaces/contact, 0.5/1/1.6/2 avatar scales, uniform/nonuniform rotated parents, offsets, pose changes, inspector radii, arena layers, repeated rebuild and mouse radius.");
        }
        finally { Object.DestroyImmediate(probe.gameObject); Object.DestroyImmediate(parent); }
    }

    private static void Verify(MocopiPlayerColliderRig rig, Animator animator, SphereCollider probe, string context)
    {
        Physics.SyncTransforms();
        foreach (var spec in Capsules)
        {
            var from = animator.GetBoneTransform(spec.from); var to = animator.GetBoneTransform(spec.to);
            if (from == null || to == null) continue;
            var collider = Find<CapsuleCollider>(rig, spec.label);
            Check(collider != null, "Capsule exists: " + spec.label);
            Vector3 center = (from.position + to.position) * 0.5f;
            Vector3 axis = (to.position - from.position).normalized;
            float radius = (float)Get(rig, spec.radius);
            float height = Mathf.Max(radius * 2, Vector3.Distance(from.position, to.position));
            Vector3 side = Vector3.Cross(axis, Vector3.forward).normalized;
            if (side.sqrMagnitude < 0.1f) side = Vector3.Cross(axis, Vector3.up).normalized;
            Near(collider.ClosestPoint(center + side * (height + 1)), center + side * radius, spec.label + " radial surface: " + context);
            Near(collider.ClosestPoint(center + axis * (height + 1)), center + axis * height * 0.5f, spec.label + " axial surface: " + context);
            Contact(collider, probe, center + side * (radius - 0.003f), true, spec.label);
            Contact(collider, probe, center + side * (radius + 0.003f), false, spec.label);
            Check(collider.gameObject.layer == PlayerArenaLayers.One, "Generated capsule keeps arena layer");
        }
        foreach (var spec in Spheres)
        {
            var bone = animator.GetBoneTransform(spec.bone);
            if (bone == null) continue;
            var collider = Find<SphereCollider>(rig, spec.bone.ToString());
            Vector3 center = bone.position + bone.rotation * (Vector3)Get(rig, spec.offset);
            float radius = (float)Get(rig, spec.radius);
            Near(collider.bounds.center, center, spec.bone + " offset: " + context);
            foreach (var direction in new[] { Vector3.right, Vector3.up, Vector3.forward, new Vector3(1, 2, 3).normalized })
                Near(collider.ClosestPoint(center + direction), center + direction * radius, spec.bone + " surface: " + context);
            Contact(collider, probe, center + Vector3.right * (radius - 0.003f), true, spec.bone.ToString());
            Contact(collider, probe, center + Vector3.right * (radius + 0.003f), false, spec.bone.ToString());
            Check(collider.gameObject.layer == PlayerArenaLayers.One, "Generated sphere keeps arena layer");
        }
    }

    private static void ValidateMouse(Transform parent, SphereCollider probe)
    {
        var camera = new GameObject("Mouse test camera").AddComponent<Camera>();
        var root = new GameObject("Mouse test"); root.transform.SetParent(parent, false);
        var mouse = root.AddComponent<DebugMousePlayerCollider>();
        try
        {
            mouse.Configure(camera, 0.08f, 0, false, 0, 0.1f, false, Color.white);
            Physics.SyncTransforms();
            var collider = root.GetComponent<SphereCollider>();
            Near(collider.ClosestPoint(root.transform.position + Vector3.right), root.transform.position + Vector3.right * 0.08f, "Mouse radius is also in metres");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject); }
    }
    private static void Contact(Collider target, SphereCollider probe, Vector3 position, bool expected, string context)
    {
        bool actual = Physics.ComputePenetration(target, target.transform.position, target.transform.rotation,
            probe, position, Quaternion.identity, out _, out _);
        Check(actual == expected, context + " actual contact boundary expected=" + expected);
    }
    private static T Find<T>(MocopiPlayerColliderRig rig, string label) where T : Collider => rig.GetComponentsInChildren<T>().FirstOrDefault(c => c.name == label + "_PlayerCollider");
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Near(Vector3 actual, Vector3 expected, string reason) => Check(Vector3.Distance(actual, expected) < 0.0008f, reason + $" expected={expected:F5} actual={actual:F5}");
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
}
