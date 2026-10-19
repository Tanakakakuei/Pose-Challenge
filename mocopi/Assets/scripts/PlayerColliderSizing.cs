using UnityEngine;

/// <summary>Keep collider dimensions in metres while retaining the bone hierarchy.</summary>
public static class PlayerColliderSizing
{
    public static void UseWorldMetres(Transform target)
    {
        // Measure after rotation: a capsule's local axes need not match its parent's axes.
        target.localScale = Vector3.one;
        Vector3 inherited = target.lossyScale;
        target.localScale = new Vector3(Inverse(inherited.x), Inverse(inherited.y), Inverse(inherited.z));
    }

    private static float Inverse(float scale) => 1f / Mathf.Max(0.000001f, Mathf.Abs(scale));

    // Offsets follow the bone's orientation, but not its size.
    public static Vector3 Offset(Transform bone, Vector3 metres) => bone.rotation * metres;
}
