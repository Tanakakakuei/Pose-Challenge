using System;
using System.IO;
using Mocopi.Receiver;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Captures the displayed avatar pose, online or offline, then reviews and saves its mask.</summary>
[DefaultExecutionOrder(1000)]
public class CaptureMaskDataMode : MonoBehaviour
{
    [Header("Capture source")]
    [SerializeField] private MocopiAvatar mocopiAvatar;
    [SerializeField] private Camera captureCamera;
    [Min(0.1f)] [SerializeField] private float connectionTimeout = 2f;

    [Header("Wall grid (match the GameMode WallBuilder)")]
    [Min(1)] [SerializeField] private int rows = 52;
    [Min(1)] [SerializeField] private int columns = 42;
    [Min(0.01f)] [SerializeField] private float cellSize = 0.05f;
    [Min(0.5f)] [SerializeField] private float forwardDistance = 3f;
    [Min(0f)] [SerializeField] private float wallGap = 0.3f;
    [SerializeField] private float floorHeight;
    [SerializeField] private Material wallMaterial;

    [Header("Mesh outline")]
    [Range(1, 8)] [SerializeField] private int captureSamplesPerCell = 4;
    [Range(0f, 0.3f)] [SerializeField] private float outlineMarginMeters;

    [Header("Controls")]
    [SerializeField] private KeyCode captureKey = KeyCode.Space;
    [SerializeField] private KeyCode saveKey = KeyCode.S;
    [SerializeField] private KeyCode retryKey = KeyCode.R;
    [SerializeField] private KeyCode repositionKey = KeyCode.P;
    [Min(0f)] [SerializeField] private float captureDelay = 3f;

    private Transform blankWall;
    private GameObject previewWall;
    private WallMaskData snapshot;
    private Mesh previewMesh;
    private Material ownedMaterial;
    private bool capturePending;
    private float captureAt;
    private bool saved;
    private PoseSilhouette silhouette;
    private int savedMaskCount;
    private CaptureMaskEdits edits;
    private bool[,] outlineMask;
    private Texture2D editorTexture;
    private int brushTool; // 0: add a cube, 1: remove a cube
    private int strokeButton = -1;
    private bool strokeOnGrid;
    private bool strokeSolid;
    private Vector2Int? lastBrushCell;
    private string message = "アバターのポーズを撮影してください。mocopi未接続でも撮影できます。";

    public bool HasPreview => snapshot != null;
    public string LastSavedPath { get; private set; }
    public bool IsConnected => mocopiAvatar != null && mocopiAvatar.isActiveAndEnabled &&
        MocopiConnectionMonitor.HasRecentFrame(mocopiAvatar, connectionTimeout);
    public bool CanCapture => mocopiAvatar != null && mocopiAvatar.gameObject.activeInHierarchy &&
        PoseAnimator != null && PoseAnimator.isHuman && blankWall != null;

    private Animator PoseAnimator => mocopiAvatar != null ? mocopiAvatar.GetComponent<Animator>() : null;
    private float Width => columns * cellSize;
    private float Height => rows * cellSize;

    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        cellSize = Mathf.Max(0.01f, cellSize);
        forwardDistance = Mathf.Max(0.5f, forwardDistance);
        wallGap = Mathf.Max(0f, wallGap);
        connectionTimeout = Mathf.Max(0.1f, connectionTimeout);
        captureDelay = Mathf.Max(0f, captureDelay);
        captureSamplesPerCell = Mathf.Clamp(captureSamplesPerCell, 1, 8);
        outlineMarginMeters = Mathf.Clamp(outlineMarginMeters, 0f, 0.3f);
    }

    private void Start()
    {
        if (mocopiAvatar == null) mocopiAvatar = FindAnyObjectByType<MocopiAvatar>();
        if (captureCamera == null) captureCamera = Camera.main;
        if (PoseAnimator != null) PoseAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (wallMaterial == null)
        {
            ownedMaterial = new Material(Shader.Find("Standard"));
            ownedMaterial.color = new Color(0.88f, 0.91f, 0.95f);
            wallMaterial = ownedMaterial;
        }
        RepositionWalls();
        RefreshSavedCount();
    }

    private void Update()
    {
        bool shortcut = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        if (shortcut && Input.GetKeyDown(KeyCode.Z))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) RedoEdit(); else UndoEdit();
            return;
        }
        if (shortcut && Input.GetKeyDown(KeyCode.Y)) { RedoEdit(); return; }
        if (Input.GetKeyDown(retryKey)) RetryCapture();
        else if (Input.GetKeyDown(repositionKey)) RepositionWalls();
        else if (Input.GetKeyDown(captureKey)) RequestCapture();
        else if (Input.GetKeyDown(saveKey)) SaveCapture();
    }

    private void LateUpdate()
    {
        if (!capturePending) return;
        if (!CanCapture)
        {
            capturePending = false;
            message = "撮影対象のHumanoidアバターと撮影用の壁を確認してください。";
            return;
        }
        // Sample the displayed pose after animation/mocopi updates, including offline captures.
        if (Time.unscaledTime >= captureAt)
        {
            capturePending = false;
            CapturePose();
        }
    }

    public void RepositionWalls()
    {
        if (captureCamera == null || mocopiAvatar == null)
        {
            message = "Capture Camera と Mocopi Avatar を設定してください。";
            return;
        }
        RetryCapture();
        if (blankWall == null)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Capture_BlankWall";
            wall.GetComponent<Collider>().enabled = false;
            Destroy(wall.GetComponent<Collider>());
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            blankWall = new GameObject("Capture_Projection").transform;
            blankWall.SetParent(transform, false);
            wall.transform.SetParent(blankWall, false);
            wall.transform.localScale = new Vector3(Width, Height, cellSize);
        }
        Vector3 forward = Vector3.ProjectOnPlane(captureCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Transform hips = PoseAnimator != null && PoseAnimator.isHuman
            ? PoseAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
        Vector3 origin = hips != null ? hips.position : mocopiAvatar.transform.position;
        origin.y = floorHeight + Height * 0.5f;
        blankWall.SetPositionAndRotation(origin + forward * forwardDistance, Quaternion.LookRotation(forward));
        message = "正面が撮影用の壁です。ポーズを取って撮影してください。";
    }

    public void RequestCapture()
    {
        if (capturePending) return;
        if (!CanCapture)
        {
            message = "撮影対象のHumanoidアバターと撮影用の壁を確認してください。";
            return;
        }
        capturePending = true;
        captureAt = Time.unscaledTime + captureDelay;
        message = IsConnected ? "ポーズを保持してください。" : "未接続：表示中のアバターのポーズを撮影します。";
    }

    private void CapturePose()
    {
        Animator animator = PoseAnimator;
        if (animator == null || !animator.isHuman) return;
        PoseSilhouette captured;
        try
        {
            captured = PoseSilhouette.Capture(mocopiAvatar.transform, blankWall,
                rows, columns, cellSize, captureSamplesPerCell);
        }
        catch (Exception exception)
        {
            message = $"輪郭を取得できませんでした。{exception.Message}";
            Debug.LogException(exception, this);
            return;
        }
        // Every new capture starts with the unexpanded mesh outline.
        bool[,] mask = captured.CreateMask(0f);
        int holes = 0;
        foreach (bool solid in mask) if (!solid) holes++;
        if (holes == 0 || holes == mask.Length)
        {
            message = "有効なポーズ型を取得できませんでした。位置を合わせて撮り直してください。";
            return;
        }

        ClearPreview();
        silhouette = captured;
        outlineMarginMeters = 0f;
        previewWall = new GameObject("Capture_FrozenPreview");
        previewWall.transform.SetParent(transform, false);
        previewWall.transform.SetPositionAndRotation(blankWall.position + blankWall.right * (Width + wallGap), blankWall.rotation);
        snapshot = previewWall.AddComponent<WallMaskData>();
        snapshot.SetHoleExpansionCells(0);
        // SavedMask prevents Apply/Save from ever resampling the moving avatar.
        outlineMask = mask;
        edits = new CaptureMaskEdits(rows, columns);
        previewWall.AddComponent<MeshFilter>();
        previewWall.AddComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        RefreshEditedPreview();
        message = $"輪郭を固定しました（穴 {holes} セル）。余白と右側のグリッドで形を整えてください。";
    }

    public void SetOutlineMargin(float meters)
    {
        if (snapshot == null || silhouette == null || capturePending) return;
        float margin = Mathf.Clamp(meters, 0f, 0.3f);
        if (Mathf.Approximately(outlineMarginMeters, margin)) return;
        bool[,] mask = silhouette.CreateMask(margin);
        // Never let dilation turn the whole wall into an empty mask.
        bool hasSolid = false;
        foreach (bool solid in mask) if (solid) { hasSolid = true; break; }
        if (!hasSolid)
        {
            message = "余白が大きすぎます。壁が残る幅にしてください。";
            return;
        }
        outlineMarginMeters = margin;
        outlineMask = mask;
        EndBrushStroke();
        RefreshEditedPreview();
        message = "余白を調整しました。手直ししたマスは保持しています。";
    }

    private void RefreshEditedPreview()
    {
        bool[,] mask = edits.Apply(outlineMask);
        snapshot.SetSavedMask(mask);
        Mesh replacement = BuildPreviewMesh(mask, cellSize);
        previewWall.GetComponent<MeshFilter>().sharedMesh = replacement;
        DisposePreviewObject(previewMesh);
        previewMesh = replacement;
        if (editorTexture == null)
            editorTexture = new Texture2D(columns, rows, TextureFormat.RGBA32, false)
                { name = "Capture mask editor", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[rows * columns];
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
            pixels[(rows - 1 - y) * columns + x] = mask[y, x]
                ? new Color32(232, 240, 247, 255) : new Color32(24, 47, 63, 255);
        editorTexture.SetPixels32(pixels); editorTexture.Apply(false);
        saved = false;
    }

    // Coordinates are top-down, matching the JSON and the editor grid.
    public bool SetPreviewCell(int row, int column, bool solid)
    {
        if (!CanEdit || row < 0 || column < 0 || row >= rows || column >= columns) return false;
        EndBrushStroke();
        edits.BeginStroke();
        bool changed = edits.Paint(row, column, solid);
        edits.EndStroke();
        if (changed) RefreshEditedPreview();
        return changed;
    }

    private bool CanEdit => HasPreview && edits != null && !capturePending;

    public void UndoEdit()
    {
        if (!CanEdit) return;
        EndBrushStroke();
        if (edits.Undo()) { RefreshEditedPreview(); message = "手直しを1つ戻しました。"; }
    }

    public void RedoEdit()
    {
        if (!CanEdit) return;
        EndBrushStroke();
        if (edits.Redo()) { RefreshEditedPreview(); message = "手直しをやり直しました。"; }
    }

    public void ClearManualEdits()
    {
        if (!CanEdit) return;
        EndBrushStroke();
        if (edits.Clear()) { RefreshEditedPreview(); message = "手直しを解除し、現在の余白の型に戻しました。"; }
    }

    private void EndBrushStroke()
    {
        edits?.EndStroke();
        strokeButton = -1; lastBrushCell = null;
    }

    private void RefreshSavedCount()
    {
        savedMaskCount = SavedMaskLibrary.LoadDefault(rows, columns).Count;
    }

    public void RetryCapture()
    {
        capturePending = false;
        ClearPreview();
        message = "新しいポーズを取って撮影してください。";
    }

    public void SaveCapture()
    {
        if (snapshot == null || saved || capturePending) return;
        EndBrushStroke();
        int solids = 0;
        var finalMask = snapshot.GetMask();
        foreach (bool solid in finalMask) if (solid) solids++;
        if (solids == 0 || solids == finalMask.Length)
        {
            message = "壁と穴の両方が必要です。Cubeを追加・削除してから保存してください。";
            return;
        }
        string path = Path.Combine(snapshot.GetMaskSaveDirectory(),
            $"mocopi_pose_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream)) writer.Write(snapshot.ExportAsJSON());
            LastSavedPath = path;
            saved = true;
            RefreshSavedCount();
            message = "保存しました。別のポーズを撮影するには「撮り直す」を押してください。";
            Debug.Log($"CaptureMaskDataMode: saved mask to {path}");
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            message = $"保存できませんでした。保存先を確認して再試行してください。\n{exception.Message}";
            Debug.LogWarning($"CaptureMaskDataMode: {exception.Message}");
        }
    }

    private void ClearPreview()
    {
        EndBrushStroke();
        if (previewWall != null)
        {
            previewWall.SetActive(false);
            DisposePreviewObject(previewWall);
        }
        DisposePreviewObject(previewMesh);
        DisposePreviewObject(editorTexture);
        editorTexture = null;
        outlineMask = null;
        edits = null;
        previewWall = null;
        previewMesh = null;
        snapshot = null;
        silhouette = null;
        saved = false;
    }

    private static void DisposePreviewObject(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    // A single render mesh: no thousands of physics bodies or destructible WallCubes in capture mode.
    public static Mesh BuildPreviewMesh(bool[,] mask, float size)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.SetActive(false);
        Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        int count = 0;
        foreach (bool solid in mask) if (solid) count++;
        var instances = new CombineInstance[count];
        int index = 0;
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!mask[y, x]) continue;
            instances[index++] = new CombineInstance
            {
                mesh = cubeMesh,
                transform = Matrix4x4.TRS(new Vector3((x + 0.5f - width * 0.5f) * size,
                    (height * 0.5f - y - 0.5f) * size, 0f), Quaternion.identity, Vector3.one * size)
            };
        }
        var mesh = new Mesh { name = "Frozen pose wall", indexFormat = IndexFormat.UInt32 };
        if (instances.Length > 0) mesh.CombineMeshes(instances, true, true);
        if (Application.isPlaying) Destroy(cube); else DestroyImmediate(cube);
        return mesh;
    }

    private void OnGUI()
    {
        Rect controls = new Rect(16, 16, Mathf.Min(560, Screen.width * 0.45f), Mathf.Min(430, Screen.height - 32));
        GUILayout.BeginArea(controls, GUI.skin.box);
        GUILayout.Label("CaptureMaskDataMode / ポーズ型の撮影");
        GUILayout.Label(IsConnected ? "mocopi: 接続中" : "mocopi: 未接続（表示中のポーズを撮影できます）");
        GUILayout.Label(capturePending ? $"撮影まで {Mathf.CeilToInt(captureAt - Time.unscaledTime)} 秒 — ポーズを保持" : message);
        GUILayout.BeginHorizontal();
        GUI.enabled = CanCapture && !capturePending;
        if (GUILayout.Button($"撮影 [{captureKey}]", GUILayout.Height(36))) RequestCapture();
        GUI.enabled = HasPreview && !saved && !capturePending;
        if (GUILayout.Button($"保存 [{saveKey}]", GUILayout.Height(36))) SaveCapture();
        GUI.enabled = true;
        if (GUILayout.Button($"撮り直す [{retryKey}]", GUILayout.Height(36))) RetryCapture();
        GUILayout.EndHorizontal();
        GUI.enabled = HasPreview && !capturePending;
        GUILayout.Label($"輪郭から外側への余白: {outlineMarginMeters * 100f:0.0} cm（0 = 撮影した輪郭）");
        float margin = GUILayout.HorizontalSlider(outlineMarginMeters, 0f, 0.3f);
        if (!Mathf.Approximately(margin, outlineMarginMeters)) SetOutlineMargin(margin);
        GUI.enabled = true;
        GUILayout.Label($"保存済み: {savedMaskCount} 個 / ゲームはランダムに10問（10個以上なら重複なし）");
        GUILayout.Label($"輪郭の取得間隔: {cellSize / captureSamplesPerCell * 100f:0.##} cm / 壁のセル: {cellSize * 100f:0.#} cm");
        if (GUILayout.Button($"現在位置・カメラ方向に壁を配置 [{repositionKey}]")) RepositionWalls();
        GUILayout.Label("壁は固定されます。撮影結果は右隣に表示され、保存するまで保持されます。");
        if (!string.IsNullOrEmpty(LastSavedPath)) GUILayout.Label($"保存先: {LastSavedPath}");
        GUILayout.EndArea();
        if (HasPreview) DrawCellEditor(controls);
    }

    private void DrawCellEditor(Rect controls)
    {
        float panelWidth = Mathf.Min(480, Screen.width * 0.4f);
        Rect panel = new Rect(Screen.width - panelWidth - 16, 16, panelWidth, Mathf.Min(820, Screen.height - 32));
        GUI.Box(panel, GUIContent.none);
        float left = panel.x + 12, width = panel.width - 24;
        GUI.Label(new Rect(left, panel.y + 8, width, 24), "型の手直し / 1マス = " + (cellSize * 100f).ToString("0.#") + " cm");
        GUI.enabled = CanEdit;
        brushTool = GUI.Toolbar(new Rect(left, panel.y + 34, width, 30), brushTool,
            new[] { "Cube追加（穴を狭く）", "Cube削除（穴を広く）" });
        float buttonWidth = (width - 8) / 3;
        GUI.enabled = CanEdit && edits.CanUndo;
        if (GUI.Button(new Rect(left, panel.y + 70, buttonWidth, 28), "元に戻す")) UndoEdit();
        GUI.enabled = CanEdit && edits.CanRedo;
        if (GUI.Button(new Rect(left + buttonWidth + 4, panel.y + 70, buttonWidth, 28), "やり直す")) RedoEdit();
        GUI.enabled = CanEdit && edits.Count > 0;
        if (GUI.Button(new Rect(left + (buttonWidth + 4) * 2, panel.y + 70, buttonWidth, 28), "手直しを解除")) ClearManualEdits();
        GUI.enabled = true;
        GUI.Label(new Rect(left, panel.y + 102, width, 22), "左: 選択ツール / 右: 削除 / ドラッグで連続編集");
        GUI.Label(new Rect(left, panel.y + 123, width, 22), "Ctrl+Z: 戻す / Ctrl+Y: やり直す");
        float size = Mathf.Min(width / columns, Mathf.Max(1, panel.height - 185) / rows);
        Rect grid = new Rect(panel.center.x - columns * size * 0.5f, panel.y + 150, columns * size, rows * size);
        GUI.DrawTexture(grid, editorTexture, ScaleMode.StretchToFill);
        Color previous = GUI.color;
        GUI.color = new Color(0.18f, 0.38f, 0.48f, 0.6f);
        for (int x = 0; x <= columns; x++) GUI.DrawTexture(new Rect(grid.x + x * size, grid.y, 1, grid.height), Texture2D.whiteTexture);
        for (int y = 0; y <= rows; y++) GUI.DrawTexture(new Rect(grid.x, grid.y + y * size, grid.width, 1), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(left, grid.yMax + 7, width, 24), $"白 = Cube / 紺 = 穴   手直し: {edits.Count} マス");

        Vector2 pointer = Event.current.mousePosition;
        bool onGrid = TryGridCell(grid, pointer, rows, columns, out Vector2Int cell);
        bool onWall = !controls.Contains(pointer) && !panel.Contains(pointer) && captureCamera != null &&
            TryPreviewCell(captureCamera.ScreenPointToRay(new Vector3(pointer.x, Screen.height - pointer.y, 0)), out cell);
        if (onGrid) TryGridCell(grid, pointer, rows, columns, out cell);
        if (CanEdit && (onGrid || onWall))
        {
            Rect hover = new Rect(grid.x + cell.x * size, grid.y + cell.y * size, size, size);
            GUI.color = new Color(0.1f, 0.9f, 0.65f, 0.65f);
            GUI.DrawTexture(hover, Texture2D.whiteTexture); GUI.color = previous;
        }
        Event input = Event.current;
        EventType type = input.rawType == EventType.MouseUp ? EventType.MouseUp : input.type;
        if (HandleBrushEvent(type, input.button, onGrid || onWall, onGrid, cell) && input.type != EventType.Used) input.Use();
    }

    public static bool TryGridCell(Rect grid, Vector2 point, int height, int width, out Vector2Int cell)
    {
        cell = default;
        if (grid.width <= 0 || grid.height <= 0 || !grid.Contains(point)) return false;
        cell = new Vector2Int(Mathf.FloorToInt((point.x - grid.x) / grid.width * width),
            Mathf.FloorToInt((point.y - grid.y) / grid.height * height));
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public bool TryPreviewCell(Ray ray, out Vector2Int cell)
    {
        cell = default;
        if (previewWall == null) return false;
        var wall = previewWall.transform;
        float face = Vector3.Dot(ray.origin - wall.position, wall.forward) >= 0 ? 0.5f : -0.5f;
        var plane = new Plane(wall.forward, wall.TransformPoint(new Vector3(0, 0, cellSize * face)));
        if (!plane.Raycast(ray, out float distance)) return false;
        Vector3 local = wall.InverseTransformPoint(ray.GetPoint(distance));
        return TryGridCell(new Rect(-Width * 0.5f, -Height * 0.5f, Width, Height),
            new Vector2(local.x, -local.y), rows, columns, out cell);
    }

    private bool HandleBrushEvent(EventType type, int button, bool hit, bool onGrid, Vector2Int cell)
    {
        if (!CanEdit) { EndBrushStroke(); return false; }
        if (type == EventType.MouseUp && strokeButton == button)
        {
            EndBrushStroke();
            return true;
        }
        if (type == EventType.MouseDown && hit && (button == 0 || button == 1))
        {
            EndBrushStroke();
            edits.BeginStroke();
            strokeButton = button; strokeOnGrid = onGrid;
            strokeSolid = button == 0 && brushTool == 0;
        }
        else if (type != EventType.MouseDrag || strokeButton != button || strokeButton < 0) return false;
        if (!hit || onGrid != strokeOnGrid) { lastBrushCell = null; return false; }
        // Interpolate fast drags so a brush stroke cannot skip cells between input events.
        Vector2Int start = lastBrushCell ?? cell;
        int steps = Mathf.Max(Mathf.Abs(cell.x - start.x), Mathf.Abs(cell.y - start.y));
        bool changed = false;
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 1 : (float)i / steps;
            changed |= edits.Paint(Mathf.RoundToInt(Mathf.Lerp(start.y, cell.y, t)),
                Mathf.RoundToInt(Mathf.Lerp(start.x, cell.x, t)), strokeSolid);
        }
        lastBrushCell = cell;
        if (changed)
        {
            RefreshEditedPreview();
            message = "型を手直ししました。保存で最終形を追加できます。";
        }
        return true;
    }

    private void OnDisable() { capturePending = false; EndBrushStroke(); }
    private void OnApplicationFocus(bool focused) { if (!focused) EndBrushStroke(); }

    private void OnDestroy()
    {
        ClearPreview();
        if (blankWall != null) Destroy(blankWall.gameObject);
        if (ownedMaterial != null) Destroy(ownedMaterial);
    }
}
