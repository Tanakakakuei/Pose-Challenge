using UnityEngine;

/// <summary>A wall cell becomes independent physical debris on its first player contact.</summary>
public class WallCube : MonoBehaviour
{
    [Header("Detached cube")]
    [SerializeField, Min(0.1f)] private float lifeTime = 7f;
    [SerializeField, Min(0f)] private float lifeTimeVariation = 1.5f;
    [SerializeField, Min(0f)] private float releaseSpeed = 0.45f;
    [SerializeField, Min(0f)] private float liftSpeed = 0.15f;
    [SerializeField, Min(0f)] private float tumbleSpeed = 4f;

    private Rigidbody rb;
    private BoxCollider wallTrigger;
    private GameObject debrisShape;
    private bool isHit;
    private GameObject originalWall;
    private float expiresAt;

    public bool IsHit => isHit;
    public GameObject OriginalWall => originalWall;
    public WallManager Owner { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wallTrigger = GetComponent<BoxCollider>();
        var mask = GetComponentInParent<WallMaskData>();
        originalWall = mask != null ? mask.gameObject : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isHit || !IsPlayerCollider(other) || rb == null || wallTrigger == null) return;
        isHit = true;
        // Detached debris must not carry a glowing fragment of the target outline.
        WallHoleOutline.Clear(this);
        WallHitTracker hits = null;
        if (originalWall != null)
        {
            hits = originalWall.GetComponent<WallHitTracker>();
            if (hits == null) hits = originalWall.AddComponent<WallHitTracker>();
            hits.RecordHit();
            Owner = hits.Owner;
        }

        Vector3 away = transform.position - other.ClosestPoint(transform.position);
        if (away.sqrMagnitude < 0.0001f) away = transform.position - other.bounds.center;
        away.y = 0;
        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
        away.Normalize();
        Vector3 inheritedVelocity = hits != null ? hits.Velocity : Vector3.zero;
        transform.SetParent(null, true);
        // Keep the renderer on its arena layer for split-screen visibility.
        // Only the solid collider uses the separate debris physics layer.
        wallTrigger.enabled = false;
        debrisShape = new GameObject("DebrisCollider");
        debrisShape.transform.SetParent(transform, false);
        debrisShape.layer = WallDebrisCollision.LayerForArena(gameObject.layer);
        var shape = debrisShape.AddComponent<BoxCollider>();
        shape.center = wallTrigger.center;
        shape.size = wallTrigger.size;
        shape.contactOffset = 0.001f;
        shape.sharedMaterial = WallDebrisCollision.Material;
        shape.excludeLayers = ~0;
        shape.layerOverridePriority = 10;
        WallDebrisCollision.AllowContact(other);

        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 0.08f;
        rb.linearDamping = 0.12f;
        rb.angularDamping = 0.35f;
        rb.maxAngularVelocity = 12f;
        rb.maxDepenetrationVelocity = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.solverIterations = 8;
        rb.solverVelocityIterations = 2;
        rb.linearVelocity = inheritedVelocity + away * releaseSpeed * Random.Range(0.8f, 1.2f)
            + Vector3.up * liftSpeed;
        Vector3 axis = Vector3.Cross(Vector3.up, away) + Random.insideUnitSphere * 0.35f;
        rb.angularVelocity = axis.normalized * tumbleSpeed * Random.Range(0.65f, 1.2f);
        expiresAt = Time.unscaledTime + Mathf.Max(0.1f, lifeTime + Random.Range(-lifeTimeVariation, lifeTimeVariation));
    }

    private void Update()
    {
        // Expire even when the result screen pauses gameplay.
        if (isHit && Time.unscaledTime >= expiresAt) Destroy(gameObject);
    }

    public void Reset()
    {
        isHit = false;
        if (debrisShape != null) Destroy(debrisShape);
        if (wallTrigger != null) wallTrigger.enabled = true;
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;
        if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null || !PlayerArenaLayers.CanInteract(gameObject, other.gameObject)) return false;
        return other.CompareTag("Player") || other.GetComponentInParent<MocopiPlayerColliderRig>() != null;
    }
}
