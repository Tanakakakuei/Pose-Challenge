using UnityEngine;

/// <summary>
/// Debug-only player collider that follows the mouse on a world-space Z plane.
/// It uses the Player layer/tag when available so wall cubes and touch panels
/// can be tested without mocopi hardware.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class DebugMousePlayerCollider : MonoBehaviour
{
    private const string PlayerName = "Player";

    [SerializeField] private Camera targetCamera;
    [Tooltip("World-space radius in metres. Separate from the avatar's body collider radii.")]
    [SerializeField] private float radius = 0.18f;
    [SerializeField] private float targetPlaneZ;
    [SerializeField] private bool hideWhenMouseOutsideCamera = true;
    [SerializeField] private bool requireMouseButton = true;
    [SerializeField] private int mouseButton;
    [SerializeField] private float clickActiveSeconds = 0.08f;
    [SerializeField] private bool showVisual = true;
    [SerializeField] private Color visualColor = new Color(0f, 0.85f, 1f, 0.35f);

    private SphereCollider sphereCollider;
    private Rigidbody sphereRigidbody;
    private MeshRenderer visualRenderer;
    private bool initialized;
    private float clickActiveUntilTime;
    private bool settingsPending;

    private void Awake()
    {
        CacheComponents();
        ApplyPlayerIdentity();
        ApplySettings();
    }

    private void OnEnable()
    {
        CacheComponents();
        ApplyPlayerIdentity();
        ApplySettings();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        settingsPending = true;
    }

    private void Update()
    {
        if (settingsPending) { CacheComponents(); ApplySettings(); settingsPending = false; }
        PlayerColliderSizing.UseWorldMetres(transform);
        Camera activeCamera = targetCamera != null ? targetCamera : Camera.main;
        if (activeCamera == null)
        {
            SetColliderVisible(false);
            return;
        }

        if (hideWhenMouseOutsideCamera && !activeCamera.pixelRect.Contains(Input.mousePosition))
        {
            SetColliderVisible(false);
            return;
        }

        if (requireMouseButton)
        {
            if (Input.GetMouseButtonDown(mouseButton))
            {
                clickActiveUntilTime = Time.unscaledTime + Mathf.Max(0.01f, clickActiveSeconds);
            }

            if (!Input.GetMouseButton(mouseButton) && Time.unscaledTime > clickActiveUntilTime)
            {
                SetColliderVisible(false);
                return;
            }
        }

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, targetPlaneZ));
        if (!plane.Raycast(ray, out float distance))
        {
            SetColliderVisible(false);
            return;
        }

        transform.position = ray.GetPoint(distance);
        SetColliderVisible(true);
    }

    public void Configure(
        Camera camera,
        float colliderRadius,
        float planeZ,
        bool useMouseButton,
        int button,
        float activeSeconds,
        bool displayVisual,
        Color color)
    {
        targetCamera = camera;
        radius = Mathf.Max(0.01f, colliderRadius);
        targetPlaneZ = planeZ;
        requireMouseButton = useMouseButton;
        mouseButton = Mathf.Max(0, button);
        clickActiveSeconds = Mathf.Max(0.01f, activeSeconds);
        showVisual = displayVisual;
        visualColor = color;
        CacheComponents();
        ApplyPlayerIdentity();
        ApplySettings();
        if (targetCamera != null) PlayerArenaLayers.Inherit(gameObject, targetCamera.gameObject);
    }

    private void CacheComponents()
    {
        if (sphereCollider == null)
        {
            sphereCollider = GetComponent<SphereCollider>();
        }

        if (sphereRigidbody == null)
        {
            sphereRigidbody = GetComponent<Rigidbody>();
        }
    }

    public void SetViewCamera(Camera camera, int layer)
    {
        targetCamera = camera;
        PlayerArenaLayers.Assign(gameObject, layer);
    }

    private void ApplyPlayerIdentity()
    {
        if (initialized)
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer(PlayerName);
        if (playerLayer >= 0)
        {
            gameObject.layer = playerLayer;
        }

        try
        {
            gameObject.tag = PlayerName;
        }
        catch (UnityException)
        {
            // The layer path is enough for this project when the Player tag is absent.
        }

        initialized = true;
    }

    private void ApplySettings()
    {
        radius = Mathf.Max(0.01f, radius);
        PlayerColliderSizing.UseWorldMetres(transform);

        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = radius;
        }

        if (sphereRigidbody != null)
        {
            sphereRigidbody.isKinematic = true;
            sphereRigidbody.useGravity = false;
        }

        EnsureVisual();
    }

    private void EnsureVisual()
    {
        if (!showVisual)
        {
            if (visualRenderer != null)
            {
                visualRenderer.gameObject.SetActive(false);
            }

            return;
        }

        if (visualRenderer == null)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "DebugMouseColliderVisual";
            visual.transform.SetParent(transform, false);
            visualRenderer = visual.GetComponent<MeshRenderer>();
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(visualCollider);
                }
                else
                {
                    DestroyImmediate(visualCollider);
                }
            }
        }

        visualRenderer.gameObject.SetActive(true);
        visualRenderer.transform.localPosition = Vector3.zero;
        visualRenderer.transform.localRotation = Quaternion.identity;
        visualRenderer.transform.localScale = Vector3.one * radius * 2f;

        Material material = visualRenderer.sharedMaterial;
        if (material == null || material.name != "Debug Mouse Collider Material")
        {
            material = new Material(Shader.Find("Standard"));
            material.name = "Debug Mouse Collider Material";
            visualRenderer.sharedMaterial = material;
        }

        material.color = visualColor;
    }

    private void SetColliderVisible(bool visible)
    {
        if (sphereCollider != null)
        {
            sphereCollider.enabled = visible;
        }

        if (visualRenderer != null)
        {
            visualRenderer.enabled = visible && showVisual;
        }
    }
}
