using UnityEngine;
using UnityEngine.UI;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Holds and applies a cube mask for one wall.
/// </summary>
public class WallMaskData : MonoBehaviour
{
    private enum MaskSource
    {
        Alphabet,
        MocopiPose,
        SavedMask
    }

    [SerializeField] private int wallHeight = 52;
    [SerializeField] private int wallWidth = 42;
    [SerializeField] private char currentLetter = 'A';
    [SerializeField] private int holeExpansionCells = 2;
    [SerializeField] private MaskSource maskSource = MaskSource.Alphabet;

    [Header("Text Mask")]
    [SerializeField] private bool useCanvasTextMask;
    [SerializeField] private Text canvasTextMaskSource;
    [SerializeField] private int textMaskTextureSize = 512;
    [SerializeField] private float textMaskPaddingRatio = 0.08f;
    [SerializeField] private float textMaskAlphaThreshold = 0.1f;

    [Header("mocopi Pose Mask")]
    [SerializeField] private Animator poseMaskAnimator;
    [SerializeField] private Transform poseMaskProjection;
    [SerializeField] private float poseBodyRadius = 0.12f;
    [SerializeField] private float poseHandFootRadius = 0.09f;
    [SerializeField] private float poseHeadRadius = 0.16f;

    [Header("Saved Mask")]
    [SerializeField] private string maskSaveFileName = "mocopi_pose_mask.json";

    private bool[,] mask;

    private void Awake()
    {
        FillMask();
    }

    private void Start()
    {
        if (mask == null)
        {
            FillMask();
        }
    }

    public void SetAlphabetHole(char letter)
    {
        maskSource = MaskSource.Alphabet;
        currentLetter = char.ToUpperInvariant(letter);
        RefreshMask(transform);
    }

    public void SetMocopiPoseHole(Animator animator, Transform projectionTransform = null)
    {
        maskSource = MaskSource.MocopiPose;
        poseMaskAnimator = animator;
        poseMaskProjection = projectionTransform;
        RefreshMask(transform);
    }

    public void SetSavedMask(bool[,] savedMask)
    {
        if (savedMask == null)
        {
            return;
        }

        maskSource = MaskSource.SavedMask;
        wallHeight = savedMask.GetLength(0);
        wallWidth = savedMask.GetLength(1);
        mask = savedMask;
    }

    public void ConfigureDimensions(int height, int width)
    {
        wallHeight = Mathf.Max(1, height);
        wallWidth = Mathf.Max(1, width);
        FillMask();
    }

    public void ConfigureDimensionsFromWall(Transform wallTransform)
    {
        WallCube[] cubes = wallTransform.GetComponentsInChildren<WallCube>(true);
        int maxRow = -1;
        int maxColumn = -1;

        foreach (WallCube cube in cubes)
        {
            if (TryParseCubeCoordinates(cube.name, out int row, out int column))
            {
                maxRow = Mathf.Max(maxRow, row);
                maxColumn = Mathf.Max(maxColumn, column);
            }
        }

        if (maxRow >= 0 && maxColumn >= 0)
        {
            ConfigureDimensions(maxRow + 1, maxColumn + 1);
            return;
        }

        if (cubes.Length != wallHeight * wallWidth)
        {
            Debug.LogWarning($"WallMaskData: cube count ({cubes.Length}) does not match mask size ({wallHeight}x{wallWidth}). Rebuild the wall grid prefab.");
        }
    }

    public void SetHoleExpansionCells(int expansionCells)
    {
        holeExpansionCells = Mathf.Max(0, expansionCells);
        RefreshMask(transform);
    }

    public void ApplyMask(Transform wallTransform)
    {
        WallCube[] cubes = wallTransform.GetComponentsInChildren<WallCube>(true);
        if (maskSource != MaskSource.SavedMask && cubes.Length != wallHeight * wallWidth)
        {
            ConfigureDimensionsFromWall(wallTransform);
        }
        else if (maskSource == MaskSource.SavedMask && cubes.Length != wallHeight * wallWidth)
        {
            Debug.LogWarning($"WallMaskData: saved mask size ({wallHeight}x{wallWidth}) does not match cube count ({cubes.Length}). Use the same wall grid size that created the saved mask.");
        }

        RefreshMask(wallTransform);

        bool appliedByName = false;
        foreach (WallCube cube in cubes)
        {
            if (TryParseCubeCoordinates(cube.name, out int row, out int column))
            {
                int y = wallHeight - 1 - row;
                int x = column;
                WallHoleOutline.Apply(cube, mask, y, x);

                if (y >= 0 && y < wallHeight && x >= 0 && x < wallWidth)
                {
                    cube.gameObject.SetActive(mask[y, x]);
                    appliedByName = true;
                }
                else
                {
                    cube.gameObject.SetActive(false);
                }
            }
        }

        if (appliedByName)
        {
            return;
        }

        for (int i = 0; i < cubes.Length; i++)
        {
            if (i < wallHeight * wallWidth)
            {
                int y = wallHeight - 1 - (i / wallWidth);
                int x = i % wallWidth;
                WallHoleOutline.Apply(cubes[i], mask, y, x);
                cubes[i].gameObject.SetActive(mask[y, x]);
            }
            else
            {
                WallHoleOutline.Clear(cubes[i]);
                cubes[i].gameObject.SetActive(false);
            }
        }
    }

    public void SetHole(int y, int x, bool isHole)
    {
        EnsureMask();

        if (y >= 0 && y < wallHeight && x >= 0 && x < wallWidth)
        {
            mask[y, x] = !isHole;
        }
    }

    public void SetHoleRect(int startY, int startX, int height, int width)
    {
        EnsureMask();

        for (int y = startY; y < startY + height && y < wallHeight; y++)
        {
            for (int x = startX; x < startX + width && x < wallWidth; x++)
            {
                if (y >= 0 && x >= 0)
                {
                    mask[y, x] = false;
                }
            }
        }
    }

    public string ExportAsJSON()
    {
        EnsureMask();

        return JsonUtility.ToJson(new SavedMaskData
        {
            wallHeight = wallHeight,
            wallWidth = wallWidth,
            currentLetter = currentLetter.ToString(),
            holeExpansionCells = holeExpansionCells,
            maskSource = maskSource.ToString(),
            rows = EncodeMaskRows(mask)
        }, true);
    }

    public int GetWallHeight() => wallHeight;
    public int GetWallWidth() => wallWidth;
    public char GetCurrentLetter() => currentLetter;
    public bool[,] GetMask() => mask;

    [ContextMenu("Apply Current Mask")]
    public void ApplyCurrentMask()
    {
        ApplyMask(transform);
    }

    [ContextMenu("Save Current Mask Data")]
    public void SaveCurrentMaskData()
    {
        RefreshMask(transform);
        string path = Path.Combine(GetMaskSaveDirectory(),
            $"mocopi_pose_{System.DateTime.Now:yyyyMMdd_HHmmss_fff}_{System.Guid.NewGuid():N}.json");
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ExportAsJSON());
        maskSaveFileName = path;
        Debug.Log($"WallMaskData: saved mask data to {path}");
    }

    [ContextMenu("Load Saved Mask Data")]
    public void LoadSavedMaskData()
    {
        LoadSavedMaskData(GetMaskSavePath());
    }

    public void LoadSavedMaskData(string pathOrFileName)
    {
        string path = Path.IsPathRooted(pathOrFileName)
            ? pathOrFileName
            : Path.Combine(GetMaskSaveDirectory(), pathOrFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"WallMaskData: saved mask data was not found at {path}");
            return;
        }

        string json = File.ReadAllText(path);
        SavedMaskData savedMaskData = JsonUtility.FromJson<SavedMaskData>(json);
        if (!TryDecodeMask(savedMaskData, out bool[,] loadedMask))
        {
            Debug.LogWarning($"WallMaskData: saved mask data at {path} is invalid.");
            return;
        }

        currentLetter = !string.IsNullOrEmpty(savedMaskData.currentLetter) ? savedMaskData.currentLetter[0] : currentLetter;
        holeExpansionCells = Mathf.Max(0, savedMaskData.holeExpansionCells);
        SetSavedMask(loadedMask);
        ApplyMask(transform);
        Debug.Log($"WallMaskData: loaded mask data from {path}");
    }

    public string GetMaskSavePath()
    {
        string fileName = string.IsNullOrWhiteSpace(maskSaveFileName) ? "mocopi_pose_mask.json" : maskSaveFileName;
        return Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Application.persistentDataPath, fileName);
    }

    public string GetMaskSaveDirectory()
    {
        string directory = Path.GetDirectoryName(GetMaskSavePath());
        return string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
    }

    public string[] GetSavedMaskPaths()
    {
        string directory = GetMaskSaveDirectory();
        return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : new string[0];
    }

    public bool TryGetSavedMaskPreview(string path, out SavedMaskPreview preview)
    {
        preview = default;
        if (!File.Exists(path))
        {
            return false;
        }

        if (!TryReadSavedMask(path, out bool[,] decodedMask))
        {
            return false;
        }

        int holeCount = 0;
        int solidCount = 0;
        for (int y = 0; y < decodedMask.GetLength(0); y++)
        {
            for (int x = 0; x < decodedMask.GetLength(1); x++)
            {
                if (decodedMask[y, x])
                {
                    solidCount++;
                }
                else
                {
                    holeCount++;
                }
            }
        }

        preview = new SavedMaskPreview(
            decodedMask.GetLength(0),
            decodedMask.GetLength(1),
            holeCount,
            solidCount,
            EncodeMaskRows(decodedMask));
        return true;
    }

    public static bool TryReadSavedMask(string path, out bool[,] decodedMask)
    {
        decodedMask = null;
        try
        {
            return TryDecodeMask(JsonUtility.FromJson<SavedMaskData>(File.ReadAllText(path)), out decodedMask);
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.UnauthorizedAccessException || exception is System.ArgumentException)
        {
            return false;
        }
    }

    private void EnsureMask()
    {
        if (mask == null)
        {
            FillMask();
        }
    }

    private void RefreshMask(Transform wallTransform)
    {
        if (maskSource == MaskSource.SavedMask)
        {
            EnsureMask();
            return;
        }

        if (maskSource == MaskSource.MocopiPose)
        {
            Transform projection = poseMaskProjection != null ? poseMaskProjection : wallTransform;
            GetWallLocalSize(wallTransform, out float wallWidthMeters, out float wallHeightMeters);
            mask = MaskPatternGenerator.GenerateHumanoidPoseHole(
                wallHeight,
                wallWidth,
                poseMaskAnimator,
                projection,
                wallWidthMeters,
                wallHeightMeters,
                poseBodyRadius,
                poseHandFootRadius,
                poseHeadRadius,
                holeExpansionCells);
            return;
        }

        if (useCanvasTextMask && canvasTextMaskSource != null)
        {
            mask = MaskPatternGenerator.GenerateTextHole(
                wallHeight,
                wallWidth,
                currentLetter,
                canvasTextMaskSource,
                textMaskTextureSize,
                textMaskPaddingRatio,
                textMaskAlphaThreshold,
                holeExpansionCells);
            return;
        }

        mask = MaskPatternGenerator.GenerateAlphabetHole(wallHeight, wallWidth, currentLetter, 1, holeExpansionCells);
    }

    private static string[] EncodeMaskRows(bool[,] sourceMask)
    {
        int wallHeight = sourceMask.GetLength(0), wallWidth = sourceMask.GetLength(1);
        string[] rows = new string[wallHeight];
        for (int y = 0; y < wallHeight; y++)
        {
            char[] row = new char[wallWidth];
            for (int x = 0; x < wallWidth; x++)
            {
                row[x] = sourceMask[y, x] ? '1' : '0';
            }

            rows[y] = new string(row);
        }

        return rows;
    }

    private static bool TryDecodeMask(SavedMaskData savedMaskData, out bool[,] decodedMask)
    {
        decodedMask = null;
        if (savedMaskData.rows == null ||
            savedMaskData.wallHeight <= 0 ||
            savedMaskData.wallWidth <= 0 ||
            (long)savedMaskData.wallHeight * savedMaskData.wallWidth > 1048576 ||
            savedMaskData.rows.Length != savedMaskData.wallHeight)
        {
            return false;
        }

        decodedMask = new bool[savedMaskData.wallHeight, savedMaskData.wallWidth];
        for (int y = 0; y < savedMaskData.wallHeight; y++)
        {
            string row = savedMaskData.rows[y];
            if (string.IsNullOrEmpty(row) || row.Length != savedMaskData.wallWidth)
            {
                return false;
            }

            for (int x = 0; x < savedMaskData.wallWidth; x++)
            {
                if (row[x] != '0' && row[x] != '1') { decodedMask = null; return false; }
                decodedMask[y, x] = row[x] == '1';
            }
        }

        return true;
    }

    private void GetWallLocalSize(Transform wallTransform, out float width, out float height)
    {
        width = wallWidth;
        height = wallHeight;

        if (wallTransform == null)
        {
            return;
        }

        WallCube[] cubes = wallTransform.GetComponentsInChildren<WallCube>(true);
        if (cubes.Length == 0)
        {
            return;
        }

        bool hasBounds = false;
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        foreach (WallCube cube in cubes)
        {
            Vector3 localPosition = cube.transform.localPosition;
            Vector3 localScale = cube.transform.localScale;
            float halfWidth = Mathf.Abs(localScale.x) * 0.5f;
            float halfHeight = Mathf.Abs(localScale.y) * 0.5f;

            if (!hasBounds)
            {
                minX = localPosition.x - halfWidth;
                maxX = localPosition.x + halfWidth;
                minY = localPosition.y - halfHeight;
                maxY = localPosition.y + halfHeight;
                hasBounds = true;
                continue;
            }

            minX = Mathf.Min(minX, localPosition.x - halfWidth);
            maxX = Mathf.Max(maxX, localPosition.x + halfWidth);
            minY = Mathf.Min(minY, localPosition.y - halfHeight);
            maxY = Mathf.Max(maxY, localPosition.y + halfHeight);
        }

        width = Mathf.Max(0.001f, maxX - minX);
        height = Mathf.Max(0.001f, maxY - minY);
    }

    private void FillMask()
    {
        mask = new bool[wallHeight, wallWidth];

        for (int y = 0; y < wallHeight; y++)
        {
            for (int x = 0; x < wallWidth; x++)
            {
                mask[y, x] = true;
            }
        }
    }

    private bool TryParseCubeCoordinates(string cubeName, out int row, out int column)
    {
        row = -1;
        column = -1;

        if (string.IsNullOrEmpty(cubeName) || !cubeName.StartsWith("Cube_"))
        {
            return false;
        }

        string[] parts = cubeName.Split('_');
        return parts.Length == 3 && int.TryParse(parts[1], out row) && int.TryParse(parts[2], out column);
    }

    [System.Serializable]
    private struct SavedMaskData
    {
        public int wallHeight;
        public int wallWidth;
        public string currentLetter;
        public int holeExpansionCells;
        public string maskSource;
        public string[] rows;
    }

    public readonly struct SavedMaskPreview
    {
        public SavedMaskPreview(int height, int width, int holeCount, int solidCount, string[] rows)
        {
            Height = height;
            Width = width;
            HoleCount = holeCount;
            SolidCount = solidCount;
            Rows = rows;
        }

        public int Height { get; }
        public int Width { get; }
        public int HoleCount { get; }
        public int SolidCount { get; }
        public int TotalCells => HoleCount + SolidCount;
        public string[] Rows { get; }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WallMaskData))]
public class WallMaskDataEditor : Editor
{
    private const float PreviewMaxWidth = 220f;
    private const float PreviewMinCellSize = 2f;
    private static readonly Color PreviewFrameColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    private static readonly Color PreviewSolidColor = new Color(0.2f, 0.55f, 0.85f, 1f);
    private static readonly Color PreviewHoleColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    private static readonly Color PreviewGridColor = new Color(0f, 0f, 0f, 0.18f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WallMaskData maskData = (WallMaskData)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            $"Mask Grid: {maskData.GetWallHeight()} rows x {maskData.GetWallWidth()} columns\n" +
            "This grid is taken from the cubes built by WallBuilder. Canvas Text is sampled into this same grid.",
            MessageType.Info);

        if (GUILayout.Button("Apply Current Mask", GUILayout.Height(32)))
        {
            maskData.ApplyCurrentMask();
            EditorUtility.SetDirty(maskData);
        }

        if (GUILayout.Button("Save Current Mask Data", GUILayout.Height(32)))
        {
            maskData.SaveCurrentMaskData();
            EditorUtility.SetDirty(maskData);
        }

        if (GUILayout.Button("Load Saved Mask Data", GUILayout.Height(32)))
        {
            maskData.LoadSavedMaskData();
            EditorUtility.SetDirty(maskData);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Saved Mask Data", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(maskData.GetMaskSaveDirectory(), EditorStyles.textField, GUILayout.Height(18));

        string[] savedMaskPaths = maskData.GetSavedMaskPaths();
        if (savedMaskPaths.Length == 0)
        {
            EditorGUILayout.HelpBox("No saved mask JSON files were found in this folder.", MessageType.Info);
            return;
        }

        foreach (string savedMaskPath in savedMaskPaths)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Path.GetFileName(savedMaskPath), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(File.GetLastWriteTime(savedMaskPath).ToString("yyyy-MM-dd HH:mm"), GUILayout.Width(120));

            if (GUILayout.Button("Load", GUILayout.Width(60)))
            {
                maskData.LoadSavedMaskData(savedMaskPath);
                EditorUtility.SetDirty(maskData);
            }

            if (GUILayout.Button("Copy Path", GUILayout.Width(80)))
            {
                EditorGUIUtility.systemCopyBuffer = savedMaskPath;
            }

            EditorGUILayout.EndHorizontal();

            if (maskData.TryGetSavedMaskPreview(savedMaskPath, out WallMaskData.SavedMaskPreview preview))
            {
                float holeRatio = preview.TotalCells > 0 ? preview.HoleCount / (float)preview.TotalCells : 0f;
                EditorGUILayout.LabelField(
                    $"{preview.Height} x {preview.Width} cells   Holes: {preview.HoleCount} ({holeRatio:P0})   Solids: {preview.SolidCount}");
                DrawMaskPreview(preview);
            }
            else
            {
                EditorGUILayout.HelpBox("This JSON could not be read as saved wall mask data.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }

    private static void DrawMaskPreview(WallMaskData.SavedMaskPreview preview)
    {
        if (preview.Rows == null || preview.Height <= 0 || preview.Width <= 0)
        {
            return;
        }

        float availableWidth = Mathf.Max(80f, EditorGUIUtility.currentViewWidth - 48f);
        float previewWidth = Mathf.Min(PreviewMaxWidth, availableWidth);
        float cellSize = Mathf.Max(PreviewMinCellSize, previewWidth / preview.Width);
        float previewHeight = cellSize * preview.Height;
        Rect rect = GUILayoutUtility.GetRect(cellSize * preview.Width, previewHeight, GUILayout.ExpandWidth(false));

        EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), PreviewFrameColor);

        for (int y = 0; y < preview.Height; y++)
        {
            string row = preview.Rows[y];
            if (string.IsNullOrEmpty(row))
            {
                continue;
            }

            for (int x = 0; x < preview.Width && x < row.Length; x++)
            {
                Rect cellRect = new Rect(
                    rect.x + x * cellSize,
                    rect.y + y * cellSize,
                    Mathf.Ceil(cellSize),
                    Mathf.Ceil(cellSize));
                EditorGUI.DrawRect(cellRect, row[x] == '1' ? PreviewSolidColor : PreviewHoleColor);
            }
        }

        if (cellSize >= 5f)
        {
            DrawPreviewGrid(rect, preview.Width, preview.Height, cellSize);
        }
    }

    private static void DrawPreviewGrid(Rect rect, int width, int height, float cellSize)
    {
        for (int x = 0; x <= width; x++)
        {
            float lineX = rect.x + x * cellSize;
            EditorGUI.DrawRect(new Rect(lineX, rect.y, 1f, rect.height), PreviewGridColor);
        }

        for (int y = 0; y <= height; y++)
        {
            float lineY = rect.y + y * cellSize;
            EditorGUI.DrawRect(new Rect(rect.x, lineY, rect.width, 1f), PreviewGridColor);
        }
    }
}
#endif
