using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TouchableUIPanelGrid : MonoBehaviour
{
    private const string GeneratedPrefix = "TouchablePanel_";

    [Header("Panels")]
    [SerializeField] private string labels = "Start\nRetry\nNext";
    [SerializeField, Min(1)] private int columns = 3;
    [SerializeField] private Vector2 panelSizeMeters = new Vector2(0.45f, 0.18f);
    [SerializeField] private Vector2 spacingMeters = new Vector2(0.06f, 0.06f);
    [SerializeField, Min(0.01f)] private float triggerDepthMeters = 0.08f;

    [Header("Font Style")]
    [SerializeField] private Font font;
    [SerializeField, Min(1)] private int fontSize = 48;
    [SerializeField] private Color fontColor = Color.white;
    [SerializeField] private Color fontOutlineColor = new Color(0.1f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 2f)] private float fontOutlineBrightness = 1f;
    [SerializeField, Range(0f, 1f)] private float fontOutlineAlpha = 1f;
    [SerializeField, Range(0f, 32f)] private float fontOutlineThicknessPixels = 6f;

    [Header("Panel Style")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.10f, 0.13f, 0.85f);
    [SerializeField] private Color touchedColor = new Color(0.18f, 0.45f, 0.95f, 0.95f);
    [SerializeField] private bool showPanelBackground = true;
    [SerializeField, Range(0f, 1f)] private float panelOpacity = 0.85f;
    [SerializeField, Range(0f, 1f)] private float touchedPanelOpacity = 0.95f;
    [SerializeField] private bool showPanelOutline = true;
    [SerializeField] private Color panelOutlineColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField, Range(0f, 2f)] private float panelOutlineBrightness = 1f;
    [SerializeField, Range(0f, 1f)] private float panelOutlineAlpha = 1f;
    [SerializeField, Range(0f, 32f)] private float panelOutlineThicknessPixels = 4f;
    [SerializeField, Min(1f)] private float pixelsPerMeter = 500f;

    [Header("Editor")]
    [SerializeField] private bool rebuildOnValidate;
    private bool refreshPending;

    private void Reset()
    {
        RebuildPanels();
    }

    private void OnValidate()
    {
        refreshPending = true;
    }

    private void Update()
    {
        if (!refreshPending) return;
        refreshPending = false;
        if (rebuildOnValidate)
        {
            RebuildPanels();
        }
        else
        {
            ApplyStyleToExistingPanels();
        }
    }

    public void RebuildPanels()
    {
        string[] panelLabels = GetPanelLabels();
        ClearGeneratedPanels();

        columns = Mathf.Max(1, columns);
        panelSizeMeters.x = Mathf.Max(0.01f, panelSizeMeters.x);
        panelSizeMeters.y = Mathf.Max(0.01f, panelSizeMeters.y);

        for (int i = 0; i < panelLabels.Length; i++)
        {
            GameObject panelObject = new GameObject($"{GeneratedPrefix}{i + 1:00}_{panelLabels[i]}");
            panelObject.transform.SetParent(transform, false);
            panelObject.transform.localPosition = GetPanelPosition(i, panelLabels.Length);
            panelObject.transform.localRotation = Quaternion.identity;

            TouchableUIPanel panel = panelObject.AddComponent<TouchableUIPanel>();
            panel.SetLabel(panelLabels[i]);
            ApplyStyle(panel);
        }
    }

    public void ApplyStyleToExistingPanels()
    {
        TouchableUIPanel[] panels = GetComponentsInChildren<TouchableUIPanel>(true);
        foreach (TouchableUIPanel panel in panels)
        {
            if (panel != null)
            {
                ApplyStyle(panel);
            }
        }
    }

    private void ApplyStyle(TouchableUIPanel panel)
    {
        panel.ConfigureVisuals(
            panelSizeMeters,
            triggerDepthMeters,
            pixelsPerMeter,
            font,
            fontSize,
            fontColor,
            panelColor,
            touchedColor,
            showPanelBackground,
            panelOpacity,
            touchedPanelOpacity,
            showPanelOutline,
            panelOutlineColor,
            panelOutlineBrightness,
            panelOutlineAlpha,
            panelOutlineThicknessPixels,
            fontOutlineColor,
            fontOutlineBrightness,
            fontOutlineAlpha,
            fontOutlineThicknessPixels);
    }

    private string[] GetPanelLabels()
    {
        return labels.Split(new[] { "\r\n", "\n", "," }, StringSplitOptions.RemoveEmptyEntries);
    }

    private Vector3 GetPanelPosition(int index, int totalCount)
    {
        int column = index % columns;
        int row = index / columns;
        int rowCount = Mathf.CeilToInt(totalCount / (float)columns);
        int columnsInThisRow = Mathf.Min(columns, totalCount - row * columns);

        float stepX = panelSizeMeters.x + spacingMeters.x;
        float stepY = panelSizeMeters.y + spacingMeters.y;
        float rowWidth = (columnsInThisRow - 1) * stepX;
        float gridHeight = (rowCount - 1) * stepY;
        float x = column * stepX - rowWidth * 0.5f;
        float y = gridHeight * 0.5f - row * stepY;

        return new Vector3(x, y, 0f);
    }

    private void ClearGeneratedPanels()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(GeneratedPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            DestroyGeneratedObject(child.gameObject);
        }
    }

    private void DestroyGeneratedObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TouchableUIPanelGrid))]
public class TouchableUIPanelGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TouchableUIPanelGrid grid = (TouchableUIPanelGrid)target;
        if (GUILayout.Button("Rebuild Touchable Panels", GUILayout.Height(32)))
        {
            grid.RebuildPanels();
            EditorUtility.SetDirty(grid);
        }

        if (GUILayout.Button("Apply Style To Existing Panels", GUILayout.Height(28)))
        {
            grid.ApplyStyleToExistingPanels();
            EditorUtility.SetDirty(grid);
        }
    }
}
#endif
