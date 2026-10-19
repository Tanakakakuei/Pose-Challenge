using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Conservative secondary motion for Mao's weighted accessory bones, after mocopi and before capture.</summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public sealed class MaoSecondaryMotion : MonoBehaviour
{
    [Serializable]
    public class MotionSettings
    {
        [Range(0.5f, 8f)] public float frequency = 3f;
        [Range(0.2f, 1.5f)] public float damping = 0.8f;
        [Range(0f, 40f)] public float maxAngle = 20f;
    }

    [Header("Natural sway")]
    [Range(0f, 1.5f)] [SerializeField] private float motionAmount = 1f;
    [SerializeField] private MotionSettings hair = new MotionSettings { frequency = 2.8f, damping = 0.78f, maxAngle = 24f };
    [Tooltip("Only the last fringe bone moves, sideways in the face plane. The roots and Jaw stay fixed.")]
    [SerializeField] private MotionSettings fringeTips = new MotionSettings { frequency = 4.5f, damping = 1.1f, maxAngle = 3f };
    [SerializeField] private MotionSettings clothing = new MotionSettings { frequency = 3.5f, damping = 0.9f, maxAngle = 14f };
    [SerializeField] private MotionSettings tail = new MotionSettings { frequency = 2.5f, damping = 0.85f, maxAngle = 20f };
    [Tooltip("Gentle air movement in m/s². Set to zero for movement-driven sway only.")]
    [Range(0f, 1f)] [SerializeField] private float airMovement = 0.12f;
    [SerializeField] private bool bodyCollision = true;

    private sealed class Particle
    {
        public Transform bone;
        public Quaternion restRotation, animatedRotation;
        public Vector3 localTip, direction, tip, velocity, previousAnchor;
        public float length;
        public MotionSettings settings;
        public bool fringe;
    }

    private struct BodyCapsule { public Vector3 a, b; public float radius; }
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<BodyCapsule> capsules = new List<BodyCapsule>();
    private readonly HashSet<Transform> humanoidBones = new HashSet<Transform>();
    private readonly HashSet<Transform> weightedBones = new HashSet<Transform>();
    private Animator animator;
    private Transform head;
    private Quaternion faceFrameOffset;
    private Vector3 previousPosition, previousScale;
    private Quaternion previousRotation;
    private bool initialized;
    private float elapsed;

    public int SimulatedBoneCount => particles.Count;

    private void OnEnable() { Initialize(); }

    private void LateUpdate() { Simulate(Time.deltaTime); }

    [ContextMenu("Reset Sway")]
    public void ResetSimulation()
    {
        if (!initialized) Initialize();
        RestorePose();
        foreach (var particle in particles)
        {
            particle.tip = particle.bone.TransformPoint(particle.localTip);
            particle.velocity = Vector3.zero;
            particle.previousAnchor = particle.bone.position;
        }
        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousScale = transform.lossyScale;
    }

    private void Initialize()
    {
        RestorePose();
        particles.Clear();
        weightedBones.Clear();
        humanoidBones.Clear();
        animator = GetComponent<Animator>();
        head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        faceFrameOffset = head != null ? Quaternion.Inverse(head.rotation) * transform.rotation : Quaternion.identity;
        if (animator != null && animator.isHuman)
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) humanoidBones.Add(bone);
            }
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (var bone in renderer.bones)
                if (bone != null) weightedBones.Add(bone);

        // Only actual skinning bones are eligible, never similarly named renderer objects or humanoid bones.
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            switch (bone.name)
            {
                // Start below the root and the auto-rigged Jaw (fronthair2), at the weighted tip only.
                case "fronthair3": AddChain(bone, fringeTips, true); break;
                case "Left_sidehair1": case "Right_sidehair1":
                case "LeftTwin1": case "RightTwin1": AddChain(bone, hair, false); break;
                case "apron1": case "apron4": case "apron7": AddChain(bone, clothing, false); break;
                case "Tail1": AddChain(bone, tail, false); break;
            }
        }
        initialized = true;
        ResetSimulation();
    }

    private void AddChain(Transform bone, MotionSettings settings, bool fringe)
    {
        if (!weightedBones.Contains(bone) || humanoidBones.Contains(bone)) return;
        // Some auto-rigs map a fringe bone to Jaw. Protect its ancestors as well as the mapped bone.
        foreach (var humanoid in humanoidBones) if (humanoid.IsChildOf(bone)) return;
        foreach (var existing in particles) if (existing.bone == bone) return;
        Transform childBone = null;
        foreach (Transform child in bone)
            if (weightedBones.Contains(child) && !humanoidBones.Contains(child)) { childBone = child; break; }
        Vector3 localTip = childBone != null ? childBone.localPosition :
            bone.InverseTransformVector((bone.position - bone.parent.position) * 0.65f);
        if (localTip.sqrMagnitude < 0.000001f) return;
        particles.Add(new Particle { bone = bone, restRotation = bone.localRotation,
            localTip = localTip, settings = settings, fringe = fringe });
        foreach (Transform child in bone) AddChain(child, settings, fringe);
    }

    private void RestorePose()
    {
        foreach (var particle in particles)
            if (particle.bone != null) particle.bone.localRotation = particle.restRotation;
    }

    /// <summary>Steps the same solver used by LateUpdate; exposed for deterministic editor previews.</summary>
    public void Simulate(float deltaTime)
    {
        if (!initialized) Initialize();
        if (deltaTime <= 0f || particles.Count == 0) return;
        RestorePose();
        if (motionAmount <= 0f) { ResetSimulation(); return; }

        float height = GetBodyHeight();
        bool reset = deltaTime > 0.1f || Vector3.Distance(previousPosition, transform.position) > height * 0.35f ||
            Quaternion.Angle(previousRotation, transform.rotation) > 65f ||
            (previousScale - transform.lossyScale).sqrMagnitude > 0.0001f;
        foreach (var particle in particles)
        {
            Vector3 endpoint = particle.bone.TransformPoint(particle.localTip);
            particle.length = Vector3.Distance(particle.bone.position, endpoint);
            particle.direction = (endpoint - particle.bone.position).normalized;
            particle.animatedRotation = particle.bone.rotation;
            reset |= Vector3.Distance(particle.previousAnchor, particle.bone.position) > height * 0.35f;
            particle.previousAnchor = particle.bone.position;
        }
        if (reset) { ResetSimulation(); return; }
        previousPosition = transform.position;
        previousRotation = transform.rotation;
        previousScale = transform.lossyScale;
        BuildBodyCapsules(height);
        Vector3 faceForward = head != null ? head.rotation * faceFrameOffset * Vector3.forward : transform.forward;

        int steps = Mathf.CeilToInt(deltaTime / (1f / 120f));
        float dt = deltaTime / steps;
        for (int step = 0; step < steps; step++)
        {
            elapsed += dt;
            for (int index = 0; index < particles.Count; index++)
            {
                var particle = particles[index];
                // World-space rest directions limit the whole chain, avoiding accumulated joint bends.
                particle.bone.rotation = particle.animatedRotation;
                Vector3 anchor = particle.bone.position;
                Vector3 target = anchor + particle.direction * particle.length;
                float omega = 2f * Mathf.PI * Mathf.Clamp(particle.settings.frequency, 0.5f, 8f);
                float phase = index * 0.37f;
                Vector3 air = new Vector3(Mathf.Sin(elapsed * 1.3f + phase), 0,
                    Mathf.Sin(elapsed * 0.9f + phase)) * airMovement;
                Vector3 acceleration = (target - particle.tip) * (omega * omega) -
                    particle.velocity * (2f * Mathf.Clamp(particle.settings.damping, 0.2f, 1.5f) * omega) +
                    (air * (particle.fringe ? 0.25f : 1f) + Vector3.down * 0.18f) * height;
                particle.velocity += acceleration * dt;
                Vector3 candidate = particle.tip + particle.velocity * dt;
                float maxAngle = Mathf.Clamp(particle.settings.maxAngle, 0, 40) * Mathf.Clamp(motionAmount, 0, 1.5f) * Mathf.Deg2Rad;
                Quaternion swing = Quaternion.identity;
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    if (bodyCollision) candidate = KeepOutsideBody(candidate, target, height * 0.006f);
                    Vector3 direction;
                    if (particle.fringe)
                    {
                        // A hinge around the face's forward axis preserves depth for every tip vertex,
                        // not just the endpoint. Head yaw/pitch carries this plane with the face.
                        Vector3 restLateral = Vector3.ProjectOnPlane(particle.direction, faceForward);
                        Vector3 candidateLateral = Vector3.ProjectOnPlane(candidate - anchor, faceForward);
                        float angle = restLateral.sqrMagnitude > 0.000001f && candidateLateral.sqrMagnitude > 0.000001f
                            ? Vector3.SignedAngle(restLateral, candidateLateral, faceForward) : 0f;
                        swing = Quaternion.AngleAxis(Mathf.Clamp(angle, -maxAngle * Mathf.Rad2Deg, maxAngle * Mathf.Rad2Deg), faceForward);
                        direction = swing * particle.direction;
                    }
                    else
                    {
                        direction = Vector3.RotateTowards(particle.direction,
                            (candidate - anchor).normalized, maxAngle, 0f);
                        swing = Quaternion.FromToRotation(particle.direction, direction);
                    }
                    candidate = anchor + direction * particle.length;
                }
                Vector3 finalDirection = (candidate - anchor).normalized;
                // Remove radial energy introduced by fixed-length/collision constraints.
                particle.velocity = Vector3.ProjectOnPlane((candidate - particle.tip) / dt, finalDirection);
                if (particle.fringe)
                {
                    Vector3 tangent = Vector3.Cross(faceForward, finalDirection).normalized;
                    particle.velocity = tangent * Vector3.Dot(particle.velocity, tangent);
                }
                particle.tip = candidate;
                if (finalDirection.sqrMagnitude > 0.5f)
                    particle.bone.rotation = swing * particle.animatedRotation;
            }
        }
    }

    private float GetBodyHeight()
    {
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (head != null && foot != null) return Mathf.Max(0.25f, Vector3.Distance(head.position, foot.position) * 1.15f);
        }
        return Mathf.Max(0.25f, transform.lossyScale.magnitude / Mathf.Sqrt(3f) * 1.7f);
    }

    private void BuildBodyCapsules(float height)
    {
        capsules.Clear();
        if (!bodyCollision || animator == null || !animator.isHuman) return;
        AddCapsule(HumanBodyBones.Hips, HumanBodyBones.Neck, height * 0.075f);
        AddCapsule(HumanBodyBones.Head, HumanBodyBones.Head, height * 0.065f);
        AddCapsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, height * 0.046f);
        AddCapsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, height * 0.046f);
    }

    private void AddCapsule(HumanBodyBones start, HumanBodyBones end, float radius)
    {
        Transform a = animator.GetBoneTransform(start), b = animator.GetBoneTransform(end);
        if (a != null && b != null) capsules.Add(new BodyCapsule { a = a.position, b = b.position, radius = radius });
    }

    private Vector3 KeepOutsideBody(Vector3 point, Vector3 restPoint, float particleRadius)
    {
        foreach (var capsule in capsules)
        {
            Vector3 restClosest = ClosestPoint(capsule.a, capsule.b, restPoint);
            // Existing clothing may lie inside an approximate capsule. Preserve its authored rest shape.
            float radius = Mathf.Min(capsule.radius + particleRadius, Vector3.Distance(restPoint, restClosest) * 0.98f);
            Vector3 closest = ClosestPoint(capsule.a, capsule.b, point);
            Vector3 offset = point - closest;
            if (offset.sqrMagnitude < radius * radius)
            {
                Vector3 direction = offset.sqrMagnitude > 0.000001f ? offset.normalized : (restPoint - restClosest).normalized;
                point = closest + direction * radius;
            }
        }
        return point;
    }

    private static Vector3 ClosestPoint(Vector3 a, Vector3 b, Vector3 point)
    {
        Vector3 delta = b - a;
        return a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / Mathf.Max(0.000001f, delta.sqrMagnitude));
    }

    private void OnDisable() { RestorePose(); initialized = false; }
}
