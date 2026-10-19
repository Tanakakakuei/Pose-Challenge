using UnityEngine;

/// <summary>
/// Smoothly follows the mocopi avatar.
/// </summary>
public class FollowCamera : MonoBehaviour
{
    private const string DefaultAvatarName = "MocopiAvatar";
    private const string PlayerTag = "Player";

    [SerializeField] private Transform targetAvatar;
    [SerializeField] private Vector3 offsetPosition = new Vector3(0, 1.5f, -2f);
    [Min(0f)]
    [SerializeField] private float smoothSpeed = 5f;

    private bool hasWarnedMissingAvatar;

    private void Start()
    {
        TryResolveAvatar();
    }

    private void LateUpdate()
    {
        if (targetAvatar == null && !TryResolveAvatar())
        {
            WarnMissingAvatarOnce();
            return;
        }

        Vector3 targetPosition = targetAvatar.position + offsetPosition;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(targetAvatar.position + Vector3.up);
    }

    private bool TryResolveAvatar()
    {
        GameObject byName = GameObject.Find(DefaultAvatarName);
        if (byName != null)
        {
            targetAvatar = byName.transform;
            return true;
        }

        GameObject byTag = GameObject.FindGameObjectWithTag(PlayerTag);
        if (byTag != null)
        {
            targetAvatar = byTag.transform;
            return true;
        }

        return false;
    }

    private void WarnMissingAvatarOnce()
    {
        if (hasWarnedMissingAvatar)
        {
            return;
        }

        Debug.LogWarning("FollowCamera: targetAvatar is not assigned. Add MocopiAvatar to the scene or assign it in the Inspector.");
        hasWarnedMissingAvatar = true;
    }

    public void SetCameraOffset(Vector3 newOffset)
    {
        offsetPosition = newOffset;
    }

    public void SetSmoothSpeed(float speed)
    {
        smoothSpeed = Mathf.Max(0f, speed);
    }
}
