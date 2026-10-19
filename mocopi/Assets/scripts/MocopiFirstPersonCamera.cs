using UnityEngine;

/// <summary>
/// Links a camera to a mocopi humanoid head bone for first-person view.
/// </summary>
public class MocopiFirstPersonCamera : MonoBehaviour
{
    private const string DefaultAvatarName = "MocopiAvatar";
    private const string PlayerTag = "Player";

    [Header("Target")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private Transform headTarget;
    [SerializeField] private bool autoResolveTarget = true;

    [Header("Camera Pose")]
    [SerializeField] private Vector3 localPositionOffset = new Vector3(0f, 0.02f, 0.08f);
    [SerializeField] private Vector3 localEulerOffset = Vector3.zero;
    [SerializeField] private bool followHeadRotation = true;

    [Header("Smoothing")]
    [Min(0f)]
    [SerializeField] private float positionSmoothSpeed = 20f;
    [Min(0f)]
    [SerializeField] private float rotationSmoothSpeed = 20f;

    private bool hasWarnedMissingHead;

    private void Start()
    {
        ResolveHeadTarget();
    }

    private void LateUpdate()
    {
        if (headTarget == null && autoResolveTarget)
        {
            ResolveHeadTarget();
        }

        if (headTarget == null)
        {
            WarnMissingHeadOnce();
            return;
        }

        Quaternion targetRotation = followHeadRotation
            ? headTarget.rotation * Quaternion.Euler(localEulerOffset)
            : transform.rotation;
        Vector3 targetPosition = headTarget.TransformPoint(localPositionOffset);

        transform.position = positionSmoothSpeed <= 0f
            ? targetPosition
            : Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.deltaTime);

        transform.rotation = rotationSmoothSpeed <= 0f
            ? targetRotation
            : Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    [ContextMenu("Resolve Head Target")]
    public void ResolveHeadTarget()
    {
        if (headTarget != null)
        {
            return;
        }

        if (targetAnimator == null)
        {
            targetAnimator = FindTargetAnimator();
        }

        if (targetAnimator != null && targetAnimator.isHuman)
        {
            headTarget = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    public void SetTargetAnimator(Animator animator)
    {
        targetAnimator = animator;
        headTarget = null;
        ResolveHeadTarget();
    }

    public void SetHeadTarget(Transform target)
    {
        headTarget = target;
    }

    private Animator FindTargetAnimator()
    {
        MocopiPlayerColliderRig rig = FindAnyObjectByType<MocopiPlayerColliderRig>();
        if (rig != null)
        {
            Animator rigAnimator = rig.GetComponent<Animator>();
            if (rigAnimator != null)
            {
                return rigAnimator;
            }
        }

        GameObject byName = GameObject.Find(DefaultAvatarName);
        if (byName != null && byName.TryGetComponent(out Animator namedAnimator))
        {
            return namedAnimator;
        }

        GameObject byTag = GameObject.FindGameObjectWithTag(PlayerTag);
        if (byTag != null && byTag.TryGetComponent(out Animator taggedAnimator))
        {
            return taggedAnimator;
        }

        return FindAnyObjectByType<Animator>();
    }

    private void WarnMissingHeadOnce()
    {
        if (hasWarnedMissingHead)
        {
            return;
        }

        Debug.LogWarning("MocopiFirstPersonCamera: Head target was not found. Assign a humanoid Animator or Head Transform in the Inspector.");
        hasWarnedMissingHead = true;
    }
}
