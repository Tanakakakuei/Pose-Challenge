using UnityEngine;

/// <summary>Contact totals outlive detached cubes, so debris expiry never changes scoring.</summary>
public sealed class WallHitTracker : MonoBehaviour
{
    public int HitCount { get; private set; }
    public Vector3 Velocity { get; set; }
    public WallManager Owner { get; set; }
    public void RecordHit() => HitCount++;
}
