using UnityEngine;

/// <summary>
/// Draws ready-pose guide orbs on both hands and merges them when the hands touch.
/// </summary>
public class ReadyHandOrbVisualizer : MonoBehaviour
{
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");

    [SerializeField] private Animator targetAnimator;
    [SerializeField] private Material orbMaterial;
    [SerializeField] private bool visibleOnStart;
    [SerializeField] private float sphereRadius = 0.11f;
    [SerializeField] private float mergedSphereRadius = 0.16f;
    [SerializeField] private float mergeDistance = 0.18f;
    [SerializeField] private Color separatedColor = new Color(0.1f, 0.75f, 1f, 0.9f);
    [SerializeField] private Color mergedColor = new Color(1f, 0.86f, 0.08f, 1f);
    [SerializeField, Range(0f, 6f)] private float glow = 2.2f;

    private readonly Vector3[] vertices = new Vector3[2];
    private readonly Color[] colors = new Color[2];
    private readonly int[] singleIndex = { 0 };
    private readonly int[] indices = { 0, 1 };
    private Mesh pointMesh;
    private Material runtimeMaterial;
    private bool isVisible;

    private void Awake()
    {
        EnsureMesh();
        EnsureMaterial();
        isVisible = visibleOnStart;
    }

    private void OnEnable()
    {
        EnsureMesh();
        EnsureMaterial();
    }

    private void OnDestroy()
    {
        if (pointMesh != null)
        {
            Destroy(pointMesh);
        }

        if (runtimeMaterial != null && runtimeMaterial != orbMaterial)
        {
            Destroy(runtimeMaterial);
        }
    }

    private void LateUpdate()
    {
        if (!isVisible)
        {
            return;
        }

        if (targetAnimator == null)
        {
            targetAnimator = FindAnyObjectByType<Animator>();
        }

        DrawHandOrbs();
    }

    public void SetTargetAnimator(Animator animator)
    {
        targetAnimator = animator;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
    }

    public void SetMergeDistance(float distance)
    {
        mergeDistance = Mathf.Max(0f, distance);
    }

    private void DrawHandOrbs()
    {
        if (targetAnimator == null || !targetAnimator.isHuman)
        {
            return;
        }

        Transform leftHand = targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (leftHand == null || rightHand == null)
        {
            return;
        }

        Vector3 leftPosition = leftHand.position;
        Vector3 rightPosition = rightHand.position;
        bool merged = Vector3.Distance(leftPosition, rightPosition) <= mergeDistance;

        if (merged)
        {
            vertices[0] = (leftPosition + rightPosition) * 0.5f;
            colors[0] = mergedColor;
            pointMesh.Clear();
            pointMesh.SetVertices(vertices, 0, 1);
            pointMesh.SetColors(colors, 0, 1);
            pointMesh.SetIndices(singleIndex, MeshTopology.Points, 0);
            runtimeMaterial.SetFloat(RadiusId, mergedSphereRadius);
        }
        else
        {
            vertices[0] = leftPosition;
            vertices[1] = rightPosition;
            colors[0] = separatedColor;
            colors[1] = separatedColor;
            pointMesh.Clear();
            pointMesh.SetVertices(vertices, 0, 2);
            pointMesh.SetColors(colors, 0, 2);
            pointMesh.SetIndices(indices, MeshTopology.Points, 0);
            runtimeMaterial.SetFloat(RadiusId, sphereRadius);
        }

        runtimeMaterial.SetFloat(GlowId, glow);
        Graphics.DrawMesh(pointMesh, Matrix4x4.identity, runtimeMaterial, gameObject.layer);
    }

    private void EnsureMesh()
    {
        if (pointMesh != null)
        {
            return;
        }

        pointMesh = new Mesh
        {
            name = "ReadyHandOrbPoints"
        };
        pointMesh.MarkDynamic();
    }

    private void EnsureMaterial()
    {
        if (runtimeMaterial != null)
        {
            return;
        }

        if (orbMaterial != null)
        {
            runtimeMaterial = new Material(orbMaterial);
            return;
        }

        Shader shader = Shader.Find("Custom/ReadyHandOrbGeometry");
        if (shader != null)
        {
            runtimeMaterial = new Material(shader);
        }
        else
        {
            enabled = false;
        }
    }
}
