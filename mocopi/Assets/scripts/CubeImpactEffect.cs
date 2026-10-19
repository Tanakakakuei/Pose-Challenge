using UnityEngine;

/// <summary>
/// Optional visual response when a wall cube is hit.
/// </summary>
public class CubeImpactEffect : MonoBehaviour
{
    [SerializeField] private GameObject impactParticlePrefab;
    [SerializeField] private float impactForce = 0f;
    [SerializeField] private Vector3 impactDirection = Vector3.down;

    private WallCube wallCube;
    private Rigidbody rb;
    private bool hasPlayedEffect;

    private void Awake()
    {
        wallCube = GetComponent<WallCube>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (wallCube != null && wallCube.IsHit && !hasPlayedEffect)
        {
            PlayImpactEffect();
            hasPlayedEffect = true;
        }
    }

    private void PlayImpactEffect()
    {
        if (impactParticlePrefab != null)
        {
            var effect = Instantiate(impactParticlePrefab, transform.position, Quaternion.identity);
            PlayerArenaLayers.Inherit(effect, gameObject);
        }

        if (wallCube == null && rb != null && impactForce > 0f)
        {
            rb.AddForce(impactDirection.normalized * impactForce, ForceMode.Impulse);
        }
    }

    public void ResetEffect()
    {
        hasPlayedEffect = false;
    }
}
