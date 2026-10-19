using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds lightweight body colliders to a mocopi-driven humanoid avatar.
/// Attach this to the same GameObject as the Animator/MocopiAvatar.
/// </summary>
[RequireComponent(typeof(Animator))]
[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public class MocopiPlayerColliderRig : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string ColliderNameSuffix = "_PlayerCollider";

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool clearExistingGeneratedColliders = true;

    [Header("Capsule Radii (world metres)")]
    [Range(0.001f, 0.3f)]
    [SerializeField] private float armRadius = 0.06f;
    [Range(0.001f, 0.3f)]
    [SerializeField] private float legRadius = 0.075f;
    [Range(0.001f, 0.4f)]
    [SerializeField] private float torsoRadius = 0.13f;
    [Range(0.001f, 0.4f)]
    [SerializeField] private float chestRadius = 0.14f;
    [Range(0.001f, 0.4f)]
    [SerializeField] private float upperChestRadius = 0.13f;
    [Range(0.001f, 0.2f)]
    [SerializeField] private float neckRadius = 0.055f;

    [Header("Sphere Radii (world metres)")]
    [Range(0.001f, 0.3f)]
    [SerializeField] private float headRadius = 0.12f;
    [Range(0.001f, 0.2f)]
    [SerializeField] private float handRadius = 0.055f;
    [Range(0.001f, 0.2f)]
    [SerializeField] private float footRadius = 0.075f;

    [Header("Sphere Offsets (metres along bone axes)")]
    [SerializeField] private Vector3 headOffset = Vector3.zero;
    [SerializeField] private Vector3 upperChestOffset = Vector3.zero;
    [SerializeField] private Vector3 leftHandOffset = Vector3.zero;
    [SerializeField] private Vector3 rightHandOffset = Vector3.zero;
    [SerializeField] private Vector3 leftFootOffset = Vector3.zero;
    [SerializeField] private Vector3 rightFootOffset = Vector3.zero;

    [Header("Debug")]
    [SerializeField] private bool showColliderGizmos = true;
    [SerializeField] private bool showGizmosOnlyWhenSelected = true;
    [SerializeField] private bool showPreviewWhenNoCollidersExist = true;
    [SerializeField] private Color capsuleGizmoColor = new Color(0.1f, 0.8f, 1f, 0.85f);
    [SerializeField] private Color sphereGizmoColor = new Color(1f, 0.75f, 0.1f, 0.85f);

    private readonly List<GameObject> colliderObjects = new List<GameObject>();
    private readonly Dictionary<string, Collider> generatedColliders = new Dictionary<string, Collider>();
    private Animator animator;
    private int playerLayer = -1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        if (Application.IsPlaying(gameObject)) EnsureRigidbody();
    }

    private void Start()
    {
        if (Application.IsPlaying(gameObject) && rebuildOnStart)
        {
            RebuildColliders();
        }
    }

    private void OnValidate()
    {
        armRadius = Mathf.Max(0.001f, armRadius);
        legRadius = Mathf.Max(0.001f, legRadius);
        torsoRadius = Mathf.Max(0.001f, torsoRadius);
        chestRadius = Mathf.Max(0.001f, chestRadius);
        upperChestRadius = Mathf.Max(0.001f, upperChestRadius);
        neckRadius = Mathf.Max(0.001f, neckRadius);
        headRadius = Mathf.Max(0.001f, headRadius);
        handRadius = Mathf.Max(0.001f, handRadius);
        footRadius = Mathf.Max(0.001f, footRadius);

        // ExecuteAlways refreshes existing colliders on the next editor update.
        // Do not change the hierarchy from OnValidate (it can run during deserialization).
    }

    private void OnEnable() => generatedColliders.Clear();
    private void LateUpdate() => ApplyColliderSettingsToHierarchy();
    private void FixedUpdate()
    {
        if (Application.IsPlaying(gameObject)) ApplyColliderSettingsToHierarchy();
    }

    private void OnDrawGizmos()
    {
        if (showColliderGizmos && !showGizmosOnlyWhenSelected)
        {
            DrawColliderGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (showColliderGizmos && showGizmosOnlyWhenSelected)
        {
            DrawColliderGizmos();
        }
    }

    [ContextMenu("Rebuild mocopi player colliders")]
    public void RebuildColliders()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null || !animator.isHuman) return;
        generatedColliders.Clear();
        if (clearExistingGeneratedColliders)
        {
            ClearColliders();
        }

        AddTorsoColliders();
        AddHeadAndNeckColliders();
        AddArmColliders();
        AddLegColliders();
    }

    [ContextMenu("Clear mocopi player colliders")]
    public void ClearColliders()
    {
        generatedColliders.Clear();
        colliderObjects.Clear();

        Collider[] existingColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider existingCollider in existingColliders)
        {
            if (existingCollider != null && existingCollider.name.EndsWith(ColliderNameSuffix))
            {
                DestroyGeneratedObject(existingCollider.gameObject);
            }
        }
    }

    [ContextMenu("Apply collider settings to hierarchy")]
    public void ApplyColliderSettingsToHierarchy()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null || !animator.isHuman) return;

        UpdateCapsule("Torso", HumanBodyBones.Hips, HumanBodyBones.Chest, torsoRadius);
        if (!UpdateCapsule("Chest", HumanBodyBones.Chest, HumanBodyBones.UpperChest, chestRadius))
            UpdateSphere("Chest", HumanBodyBones.Chest, chestRadius, Vector3.zero);
        if (!UpdateCapsule("Neck", HumanBodyBones.Neck, HumanBodyBones.Head, neckRadius))
        {
            UpdateCapsule("Neck", HumanBodyBones.Chest, HumanBodyBones.Head, neckRadius);
        }

        UpdateArmCapsules();
        UpdateLegCapsules();

        UpdateSphere(HumanBodyBones.Head.ToString(), HumanBodyBones.Head, headRadius, headOffset);
        UpdateSphere(HumanBodyBones.LeftHand.ToString(), HumanBodyBones.LeftHand, handRadius, leftHandOffset);
        UpdateSphere(HumanBodyBones.RightHand.ToString(), HumanBodyBones.RightHand, handRadius, rightHandOffset);
        UpdateSphere(HumanBodyBones.LeftFoot.ToString(), HumanBodyBones.LeftFoot, footRadius, leftFootOffset);
        UpdateSphere(HumanBodyBones.RightFoot.ToString(), HumanBodyBones.RightFoot, footRadius, rightFootOffset);

        if (!UpdateSphere("UpperChest", HumanBodyBones.UpperChest, upperChestRadius, upperChestOffset))
        {
            UpdateFallbackUpperChestSphere();
        }
    }

    private void AddTorsoColliders()
    {
        AddCapsule(HumanBodyBones.Hips, HumanBodyBones.Chest, torsoRadius, "Torso");

        if (!AddCapsule(HumanBodyBones.Chest, HumanBodyBones.UpperChest, chestRadius, "Chest"))
        {
            AddSphere(HumanBodyBones.Chest, chestRadius, "Chest");
        }

        if (!AddSphere(HumanBodyBones.UpperChest, upperChestRadius, "UpperChest", upperChestOffset))
        {
            AddFallbackUpperChestSphere();
        }
    }

    private bool UpdateCapsule(string label, HumanBodyBones startBone, HumanBodyBones endBone, float radius)
    {
        CapsuleCollider capsule = FindGeneratedCollider<CapsuleCollider>(label);
        if (capsule == null || !TryGetBoneSegment(startBone, endBone, out BoneSegment segment))
        {
            return false;
        }

        if (capsule.transform.parent != segment.Start) capsule.transform.SetParent(segment.Start, false);
        ApplyCapsuleTransform(capsule.transform, segment);
        capsule.center = Vector3.zero;
        capsule.radius = radius;
        capsule.height = Mathf.Max(radius * 2f, segment.Distance);
        capsule.direction = 1;
        capsule.isTrigger = false;
        ApplyPlayerLayer(capsule.gameObject);
        return true;
    }

    private bool UpdateSphere(string label, HumanBodyBones bone, float radius, Vector3 localOffset)
    {
        SphereCollider sphere = FindGeneratedCollider<SphereCollider>(label);
        Transform boneTransform = animator != null ? animator.GetBoneTransform(bone) : null;
        if (sphere == null || boneTransform == null)
        {
            return false;
        }

        if (sphere.transform.parent != boneTransform) sphere.transform.SetParent(boneTransform, false);
        ApplySphereTransform(sphere.transform, boneTransform, boneTransform.position, localOffset);
        sphere.center = Vector3.zero;
        sphere.radius = radius;
        sphere.isTrigger = false;
        ApplyPlayerLayer(sphere.gameObject);
        return true;
    }

    private bool UpdateFallbackUpperChestSphere()
    {
        SphereCollider sphere = FindGeneratedCollider<SphereCollider>("UpperChest");
        Transform chest = animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
        Transform head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (sphere == null || chest == null || head == null)
        {
            return false;
        }

        if (sphere.transform.parent != chest) sphere.transform.SetParent(chest, false);
        ApplySphereTransform(sphere.transform, chest, Vector3.Lerp(chest.position, head.position, 0.35f), upperChestOffset);
        sphere.center = Vector3.zero;
        sphere.radius = upperChestRadius;
        sphere.isTrigger = false;
        ApplyPlayerLayer(sphere.gameObject);
        return true;
    }

    private void AddHeadAndNeckColliders()
    {
        if (!AddCapsule(HumanBodyBones.Neck, HumanBodyBones.Head, neckRadius, "Neck"))
        {
            AddCapsule(HumanBodyBones.Chest, HumanBodyBones.Head, neckRadius, "Neck");
        }

        AddSphere(HumanBodyBones.Head, headRadius, HumanBodyBones.Head.ToString(), headOffset);
    }

    private void AddArmColliders()
    {
        AddLimbColliders(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, armRadius, handRadius);
        AddLimbColliders(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, armRadius, handRadius);
    }

    private void AddLegColliders()
    {
        AddLimbColliders(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, legRadius, footRadius);
        AddLimbColliders(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, legRadius, footRadius);
    }

    private void AddLimbColliders(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, float capsuleRadius, float endRadius)
    {
        AddCapsule(upper, lower, capsuleRadius, upper.ToString());
        AddCapsule(lower, end, capsuleRadius, lower.ToString());
        AddSphere(end, endRadius, end.ToString(), GetEndSphereOffset(end));
    }

    private void UpdateArmCapsules()
    {
        UpdateLimbCapsules(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, armRadius);
        UpdateLimbCapsules(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, armRadius);
    }

    private void UpdateLegCapsules()
    {
        UpdateLimbCapsules(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, legRadius);
        UpdateLimbCapsules(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, legRadius);
    }

    private void UpdateLimbCapsules(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, float capsuleRadius)
    {
        UpdateCapsule(upper.ToString(), upper, lower, capsuleRadius);
        UpdateCapsule(lower.ToString(), lower, end, capsuleRadius);
    }

    private Vector3 GetEndSphereOffset(HumanBodyBones bone)
    {
        return bone switch
        {
            HumanBodyBones.LeftHand => leftHandOffset,
            HumanBodyBones.RightHand => rightHandOffset,
            HumanBodyBones.LeftFoot => leftFootOffset,
            HumanBodyBones.RightFoot => rightFootOffset,
            _ => Vector3.zero
        };
    }

    private bool AddCapsule(HumanBodyBones startBone, HumanBodyBones endBone, float radius, string label)
    {
        if (UpdateCapsule(label, startBone, endBone, radius)) return true;
        if (!TryGetBoneSegment(startBone, endBone, out BoneSegment segment))
        {
            return false;
        }

        GameObject colliderObject = CreateColliderObject(segment.Start, $"{label}{ColliderNameSuffix}");
        ApplyCapsuleTransform(colliderObject.transform, segment);

        CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
        capsule.isTrigger = false;
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = Mathf.Max(radius * 2f, segment.Distance);

        generatedColliders[colliderObject.name] = capsule;
        colliderObjects.Add(colliderObject);
        return true;
    }

    private bool TryGetBoneSegment(HumanBodyBones startBone, HumanBodyBones endBone, out BoneSegment segment)
    {
        segment = default;

        Transform start = animator != null ? animator.GetBoneTransform(startBone) : null;
        Transform end = animator != null ? animator.GetBoneTransform(endBone) : null;
        if (start == null || end == null)
        {
            return false;
        }

        Vector3 direction = end.position - start.position;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        segment = new BoneSegment(start, direction, distance);
        return true;
    }

    private void ApplyCapsuleTransform(Transform target, BoneSegment segment)
    {
        target.position = segment.Start.position + segment.Direction * 0.5f;
        target.rotation = Quaternion.FromToRotation(Vector3.up, segment.Axis);
        PlayerColliderSizing.UseWorldMetres(target);
    }

    private static void ApplySphereTransform(Transform target, Transform bone, Vector3 origin, Vector3 offset)
    {
        target.SetPositionAndRotation(origin + PlayerColliderSizing.Offset(bone, offset), bone.rotation);
        PlayerColliderSizing.UseWorldMetres(target);
    }

    private void AddFallbackUpperChestSphere()
    {
        if (UpdateFallbackUpperChestSphere()) return;
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (chest == null || head == null)
        {
            return;
        }

        GameObject colliderObject = CreateColliderObject(chest, $"UpperChest{ColliderNameSuffix}");
        ApplySphereTransform(colliderObject.transform, chest, Vector3.Lerp(chest.position, head.position, 0.35f), upperChestOffset);

        SphereCollider sphere = colliderObject.AddComponent<SphereCollider>();
        sphere.isTrigger = false;
        sphere.radius = upperChestRadius;

        generatedColliders[colliderObject.name] = sphere;
        colliderObjects.Add(colliderObject);
    }

    private bool AddSphere(HumanBodyBones bone, float radius)
    {
        return AddSphere(bone, radius, bone.ToString(), Vector3.zero);
    }

    private bool AddSphere(HumanBodyBones bone, float radius, string label)
    {
        return AddSphere(bone, radius, label, Vector3.zero);
    }

    private bool AddSphere(HumanBodyBones bone, float radius, string label, Vector3 localOffset)
    {
        if (UpdateSphere(label, bone, radius, localOffset)) return true;
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            return false;
        }

        GameObject colliderObject = CreateColliderObject(boneTransform, $"{label}{ColliderNameSuffix}");
        ApplySphereTransform(colliderObject.transform, boneTransform, boneTransform.position, localOffset);

        SphereCollider sphere = colliderObject.AddComponent<SphereCollider>();
        sphere.isTrigger = false;
        sphere.radius = radius;

        generatedColliders[colliderObject.name] = sphere;
        colliderObjects.Add(colliderObject);
        return true;
    }

    private GameObject CreateColliderObject(Transform parent, string objectName)
    {
        GameObject colliderObject = new GameObject(objectName);
        colliderObject.transform.SetParent(parent, false);
        colliderObject.transform.localScale = Vector3.one;

        ApplyPlayerLayer(colliderObject);

        return colliderObject;
    }

    private T FindGeneratedCollider<T>(string label) where T : Collider
    {
        string objectName = $"{label}{ColliderNameSuffix}";
        if (generatedColliders.TryGetValue(objectName, out Collider cached) && cached != null)
            return cached as T;
        T[] colliders = GetComponentsInChildren<T>(true);
        foreach (T targetCollider in colliders)
        {
            if (targetCollider != null && targetCollider.gameObject.activeSelf && targetCollider.name == objectName)
            {
                generatedColliders[objectName] = targetCollider;
                return targetCollider;
            }
        }

        return null;
    }

    private void ApplyPlayerLayer(GameObject target)
    {
        if (target != null && PlayerArenaLayers.IsArena(gameObject.layer))
        {
            target.layer = gameObject.layer;
            return;
        }
        if (playerLayer < 0)
        {
            playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        }

        if (playerLayer >= 0 && target != null)
        {
            target.layer = playerLayer;
        }
    }

    private void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            // Deferred destruction must not leave an old collider active during a rebuild.
            target.SetActive(false);
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void EnsureRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void DrawColliderGizmos()
    {
        bool drewGeneratedColliders = DrawGeneratedColliderGizmos();
        if (!drewGeneratedColliders && showPreviewWhenNoCollidersExist)
        {
            DrawPreviewColliderGizmos();
        }
    }

    private bool DrawGeneratedColliderGizmos()
    {
        bool drewAnyCollider = false;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null || !targetCollider.name.EndsWith(ColliderNameSuffix))
            {
                continue;
            }

            if (targetCollider is CapsuleCollider capsule)
            {
                DrawCapsuleGizmo(capsule);
                drewAnyCollider = true;
            }
            else if (targetCollider is SphereCollider sphere)
            {
                DrawSphereGizmo(sphere);
                drewAnyCollider = true;
            }
        }

        return drewAnyCollider;
    }

    private void DrawPreviewColliderGizmos()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null || !animator.isHuman)
        {
            return;
        }

        Gizmos.color = capsuleGizmoColor;
        DrawPreviewCapsule(HumanBodyBones.Hips, HumanBodyBones.Chest, torsoRadius);
        if (!DrawPreviewCapsule(HumanBodyBones.Chest, HumanBodyBones.UpperChest, chestRadius))
        {
            Gizmos.color = sphereGizmoColor;
            DrawPreviewSphere(HumanBodyBones.Chest, chestRadius, Vector3.zero);
            Gizmos.color = capsuleGizmoColor;
        }
        DrawPreviewNeckCapsule();
        DrawPreviewArmCapsules();
        DrawPreviewLegCapsules();

        Gizmos.color = sphereGizmoColor;
        DrawPreviewSphere(HumanBodyBones.Head, headRadius, headOffset);
        DrawPreviewSphere(HumanBodyBones.LeftHand, handRadius, leftHandOffset);
        DrawPreviewSphere(HumanBodyBones.RightHand, handRadius, rightHandOffset);
        DrawPreviewSphere(HumanBodyBones.LeftFoot, footRadius, leftFootOffset);
        DrawPreviewSphere(HumanBodyBones.RightFoot, footRadius, rightFootOffset);

        if (!DrawPreviewSphere(HumanBodyBones.UpperChest, upperChestRadius, upperChestOffset))
        {
            DrawPreviewFallbackUpperChestSphere();
        }
    }

    private void DrawPreviewNeckCapsule()
    {
        if (!DrawPreviewCapsule(HumanBodyBones.Neck, HumanBodyBones.Head, neckRadius))
        {
            DrawPreviewCapsule(HumanBodyBones.Chest, HumanBodyBones.Head, neckRadius);
        }
    }

    private void DrawPreviewArmCapsules()
    {
        DrawPreviewLimbCapsules(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, armRadius);
        DrawPreviewLimbCapsules(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, armRadius);
    }

    private void DrawPreviewLegCapsules()
    {
        DrawPreviewLimbCapsules(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, legRadius);
        DrawPreviewLimbCapsules(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, legRadius);
    }

    private void DrawPreviewLimbCapsules(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, float capsuleRadius)
    {
        DrawPreviewCapsule(upper, lower, capsuleRadius);
        DrawPreviewCapsule(lower, end, capsuleRadius);
    }

    private bool DrawPreviewCapsule(HumanBodyBones startBone, HumanBodyBones endBone, float radius)
    {
        if (!TryGetBoneSegment(startBone, endBone, out BoneSegment segment))
        {
            return false;
        }

        Vector3 center = segment.Start.position + segment.Direction * 0.5f;
        DrawWireCapsule(center, segment.Axis, radius, Mathf.Max(radius * 2f, segment.Distance));
        return true;
    }

    private bool DrawPreviewSphere(HumanBodyBones bone, float radius, Vector3 localOffset)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            return false;
        }

        Gizmos.DrawWireSphere(boneTransform.position + PlayerColliderSizing.Offset(boneTransform, localOffset), radius);
        return true;
    }

    private void DrawPreviewFallbackUpperChestSphere()
    {
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (chest == null || head == null)
        {
            return;
        }

        Vector3 center = Vector3.Lerp(chest.position, head.position, 0.35f) + PlayerColliderSizing.Offset(chest, upperChestOffset);
        Gizmos.DrawWireSphere(center, upperChestRadius);
    }

    private void DrawSphereGizmo(SphereCollider sphere)
    {
        Gizmos.color = sphereGizmoColor;
        Vector3 center = sphere.transform.TransformPoint(sphere.center);
        float radius = sphere.radius * GetLargestAxisScale(sphere.transform.lossyScale);
        Gizmos.DrawWireSphere(center, radius);
    }

    private void DrawCapsuleGizmo(CapsuleCollider capsule)
    {
        Gizmos.color = capsuleGizmoColor;

        Transform capsuleTransform = capsule.transform;
        Vector3 center = capsuleTransform.TransformPoint(capsule.center);
        Vector3 axis = GetCapsuleAxis(capsuleTransform, capsule.direction);
        Vector3 lossyScale = capsuleTransform.lossyScale;
        float radius = capsule.radius * GetCapsuleRadiusScale(lossyScale, capsule.direction);
        float height = Mathf.Max(capsule.height * GetCapsuleHeightScale(lossyScale, capsule.direction), radius * 2f);

        DrawWireCapsule(center, axis, radius, height);
    }

    private void DrawWireCapsule(Vector3 center, Vector3 axis, float radius, float height)
    {
        float halfLineLength = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 top = center + axis * halfLineLength;
        Vector3 bottom = center - axis * halfLineLength;

        Gizmos.DrawWireSphere(top, radius);
        Gizmos.DrawWireSphere(bottom, radius);

        Vector3 sideA = Vector3.Cross(axis, Vector3.up);
        if (sideA.sqrMagnitude < 0.001f)
        {
            sideA = Vector3.Cross(axis, Vector3.right);
        }

        sideA.Normalize();
        Vector3 sideB = Vector3.Cross(axis, sideA).normalized;

        Gizmos.DrawLine(top + sideA * radius, bottom + sideA * radius);
        Gizmos.DrawLine(top - sideA * radius, bottom - sideA * radius);
        Gizmos.DrawLine(top + sideB * radius, bottom + sideB * radius);
        Gizmos.DrawLine(top - sideB * radius, bottom - sideB * radius);
    }

    private Vector3 GetCapsuleAxis(Transform capsuleTransform, int direction)
    {
        return direction switch
        {
            0 => capsuleTransform.right,
            1 => capsuleTransform.up,
            2 => capsuleTransform.forward,
            _ => capsuleTransform.up
        };
    }

    private float GetCapsuleHeightScale(Vector3 lossyScale, int direction)
    {
        return direction switch
        {
            0 => Mathf.Abs(lossyScale.x),
            1 => Mathf.Abs(lossyScale.y),
            2 => Mathf.Abs(lossyScale.z),
            _ => Mathf.Abs(lossyScale.y)
        };
    }

    private float GetCapsuleRadiusScale(Vector3 lossyScale, int direction)
    {
        return direction switch
        {
            0 => Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)),
            1 => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z)),
            2 => Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y)),
            _ => GetLargestAxisScale(lossyScale)
        };
    }

    private float GetLargestAxisScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private readonly struct BoneSegment
    {
        public BoneSegment(Transform start, Vector3 direction, float distance)
        {
            Start = start;
            Direction = direction;
            Distance = distance;
            Axis = direction.normalized;
        }

        public Transform Start { get; }
        public Vector3 Direction { get; }
        public float Distance { get; }
        public Vector3 Axis { get; }
    }
}
