using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor helper for building a rectangular wall from small cube triggers.
/// </summary>
public class WallBuilder : MonoBehaviour
{
    private enum WallSizeMode
    {
        CellCount,
        PhysicalSize
    }

    [Header("Size Mode")]
    [SerializeField] private WallSizeMode sizeMode = WallSizeMode.PhysicalSize;
    [SerializeField] private Vector2 wallSizeMeters = new Vector2(2.1f, 2.6f);

    [Header("Wall Settings")]
    [SerializeField] private int rows = 52;
    [SerializeField] private int columns = 42;
    [SerializeField] private float cubeSize = 0.05f;
    [SerializeField] private Material cubeMaterial;

    [Header("Preview")]
    [SerializeField] private bool showSizePreview = true;
    [SerializeField] private Color previewColor = new Color(0.1f, 0.8f, 1f, 0.85f);

    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        cubeSize = Mathf.Max(0.001f, cubeSize);
        wallSizeMeters.x = Mathf.Max(cubeSize, wallSizeMeters.x);
        wallSizeMeters.y = Mathf.Max(cubeSize, wallSizeMeters.y);
    }

    [ContextMenu("Build Wall Grid")]
    public void BuildWallGrid()
    {
        ClearWall();

        int resolvedRows = GetResolvedRows();
        int resolvedColumns = GetResolvedColumns();
        float startX = -(resolvedColumns * cubeSize) / 2f;
        float startY = -(resolvedRows * cubeSize) / 2f;

        for (int row = 0; row < resolvedRows; row++)
        {
            for (int col = 0; col < resolvedColumns; col++)
            {
                Vector3 position = new Vector3(
                    startX + col * cubeSize + cubeSize / 2f,
                    startY + row * cubeSize + cubeSize / 2f,
                    0f
                );

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Cube_{row}_{col}";
                cube.transform.SetParent(transform, false);
                cube.transform.localPosition = position;
                cube.transform.localScale = Vector3.one * cubeSize;

                BoxCollider boxCollider = cube.GetComponent<BoxCollider>();
                boxCollider.isTrigger = true;

                Rigidbody rb = cube.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                cube.AddComponent<WallCube>();
                cube.AddComponent<CubeImpactEffect>();

                if (cubeMaterial != null)
                {
                    cube.GetComponent<Renderer>().material = cubeMaterial;
                }
            }
        }

        WallMaskData maskData = GetComponent<WallMaskData>();
        if (maskData == null)
        {
            maskData = gameObject.AddComponent<WallMaskData>();
        }

        maskData.ConfigureDimensions(resolvedRows, resolvedColumns);
        maskData.SetAlphabetHole('A');
        maskData.ApplyMask(transform);

        Debug.Log($"Wall grid created: {resolvedRows}x{resolvedColumns} = {resolvedRows * resolvedColumns} cubes, size {GetResolvedWidth():0.###}m x {GetResolvedHeight():0.###}m");
    }

    [ContextMenu("Clear Wall")]
    public void ClearWall()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    [ContextMenu("Copy Resolved Size To Cell Count")]
    public void CopyResolvedSizeToCellCount()
    {
        rows = GetResolvedRows();
        columns = GetResolvedColumns();
        sizeMode = WallSizeMode.CellCount;
    }

    public int GetResolvedRows()
    {
        return sizeMode == WallSizeMode.PhysicalSize
            ? Mathf.Max(1, Mathf.RoundToInt(wallSizeMeters.y / cubeSize))
            : rows;
    }

    public int GetResolvedColumns()
    {
        return sizeMode == WallSizeMode.PhysicalSize
            ? Mathf.Max(1, Mathf.RoundToInt(wallSizeMeters.x / cubeSize))
            : columns;
    }

    public float GetResolvedWidth()
    {
        return GetResolvedColumns() * cubeSize;
    }

    public float GetResolvedHeight()
    {
        return GetResolvedRows() * cubeSize;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSizePreview)
        {
            return;
        }

        Gizmos.color = previewColor;
        DrawPreviewRectangle();
    }

    private void DrawPreviewRectangle()
    {
        float halfWidth = GetResolvedWidth() * 0.5f;
        float halfHeight = GetResolvedHeight() * 0.5f;

        Vector3 topLeft = transform.TransformPoint(new Vector3(-halfWidth, halfHeight, 0f));
        Vector3 topRight = transform.TransformPoint(new Vector3(halfWidth, halfHeight, 0f));
        Vector3 bottomRight = transform.TransformPoint(new Vector3(halfWidth, -halfHeight, 0f));
        Vector3 bottomLeft = transform.TransformPoint(new Vector3(-halfWidth, -halfHeight, 0f));

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WallBuilder))]
public class WallBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WallBuilder wallBuilder = (WallBuilder)target;
        int rows = wallBuilder.GetResolvedRows();
        int columns = wallBuilder.GetResolvedColumns();
        float width = wallBuilder.GetResolvedWidth();
        float height = wallBuilder.GetResolvedHeight();

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            $"Generated Grid: {rows} rows x {columns} columns\n" +
            $"Actual Wall Size: {width:0.###}m x {height:0.###}m\n" +
            $"Mask Resolution: {rows} x {columns} cells",
            MessageType.Info);

        GUILayout.Space(10);
        if (GUILayout.Button("Build Wall Grid", GUILayout.Height(40)))
        {
            wallBuilder.BuildWallGrid();
        }

        if (GUILayout.Button("Copy Resolved Size To Cell Count", GUILayout.Height(30)))
        {
            wallBuilder.CopyResolvedSizeToCellCount();
            EditorUtility.SetDirty(wallBuilder);
        }

        if (GUILayout.Button("Clear Wall", GUILayout.Height(30)))
        {
            wallBuilder.ClearWall();
        }
    }
}
#endif
