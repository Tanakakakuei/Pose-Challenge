using UnityEngine;

/// <summary>
/// A single solid cube in a wall. It records player contact and bounces away on hit.
/// </summary>
public class WallCube : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [Header("Bounce")]
    [SerializeField] private float bounceForce = 2f;
    [SerializeField] private float upwardForce = 2f;
    [SerializeField] private float fallbackBackwardForce = 0f;

    private Rigidbody rb;
    private bool isHit;
    private int playerLayer = -1;
    private GameObject originalWall;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerLayer = LayerMask.NameToLayer(PlayerTag);
        WallMaskData wallMaskData = GetComponentInParent<WallMaskData>();
        originalWall = wallMaskData != null ? wallMaskData.gameObject : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHit || !IsPlayerCollider(other))
        {
            return;
        }

        isHit = true;

        if (TryGetRigidbody())
        {
            ReleaseFromWall();
            rb.AddForce(GetBounceImpulse(other), ForceMode.Impulse);
        }
    }

    public bool IsHit => isHit;
    public GameObject OriginalWall => originalWall;

    public void Reset()
    {
        isHit = false;

        if (TryGetRigidbody())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            ClearVelocity();
        }
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (!PlayerArenaLayers.CanInteract(gameObject, other.gameObject)) return false;
        if (playerLayer >= 0 && IsLayerInHierarchy(other.transform, playerLayer))
        {
            return true;
        }

        return other.CompareTag(PlayerTag) || other.GetComponentInParent<MocopiPlayerColliderRig>() != null;
    }

    private Vector3 GetBounceImpulse(Collider playerCollider)
    {
        Vector3 direction = transform.position - playerCollider.ClosestPoint(transform.position);

        if (direction.sqrMagnitude < 0.0001f)
        {
            MocopiPlayerColliderRig rig = playerCollider.GetComponentInParent<MocopiPlayerColliderRig>();
            if (rig != null)
            {
                direction = transform.position - rig.transform.position;
            }
        }

        direction.y = Mathf.Max(direction.y, 0f);

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -transform.forward;
        }

        Vector3 impulse = direction.normalized * bounceForce;
        impulse += Vector3.up * upwardForce;

        if (fallbackBackwardForce > 0f)
        {
            impulse += -transform.forward * fallbackBackwardForce;
        }

        return impulse;
    }

    private bool TryGetRigidbody()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        return rb != null;
    }

    private void ReleaseFromWall()
    {
        transform.SetParent(null, true);
        rb.isKinematic = false;
        rb.useGravity = true;
        ClearVelocity();
    }

    private void ClearVelocity()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private bool IsLayerInHierarchy(Transform current, int layer)
    {
        while (current != null)
        {
            if (current.gameObject.layer == layer)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
