using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Spawns, moves, scores, and resolves alphabet cut-out walls.
/// </summary>
public class WallManager : MonoBehaviour
{
    private const int AlphabetLetterCount = 26;

    [Header("Wall")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Visible wall width in metres. Zero preserves the complete authored grid.")]
    [SerializeField, Min(0f)] private float wallWidthMeters;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float destroyDistance = -10f;
    [SerializeField] private float spawnInterval = 3f;
    [Tooltip("Floors that detached cubes can hit. Empty uses the authored Plane/Floor colliders.")]
    [SerializeField] private Collider[] debrisFloors;

    [Header("Letters")]
    [SerializeField] private bool randomizeLetters = true;
    [SerializeField] private char firstLetter = 'A';

    [Header("Saved pose library")]
    [SerializeField] private bool useSavedMasks;
    [Tooltip("Empty uses the bundled PoseMasks plus the CaptureMode save folder. A specified directory replaces both.")]
    [SerializeField] private string savedMaskDirectory;
    private List<SavedMaskLibrary.Entry> roundMasks = new List<SavedMaskLibrary.Entry>();
    private bool roundPrepared;
    public bool UsesSavedMasks => useSavedMasks && roundMasks.Count > 0;
    public string SavedMaskStatus { get; private set; }

    [Header("mocopi Pose Mask")]
    [SerializeField] private bool useMocopiPoseMask;
    [SerializeField] private Animator poseMaskAnimator;
    [SerializeField] private Transform poseMaskProjection;

    [Header("Result Rules")]
    [SerializeField] private int wallCount = 10;
    [SerializeField] private int safeHitThreshold = 3;
    [SerializeField] private bool pauseOnResult = true;

    [Header("Flow")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool clearWallsOnReset = true;
    [SerializeField] private bool controlledByGameLoop;

    public event Action<char> OnNextLetterChanged;
    public event Action<int> OnContactCountChanged;
    public event Action<int> OnScoreChanged;
    public event Action<int, int> OnProgressChanged;
    public event Action<ResultData> OnResult;

    private readonly List<GameObject> activeWalls = new List<GameObject>();
    private float lastSpawnTime;
    private int contactCount;
    private int score;
    private int spawnedWallCount;
    private int completedWallCount;
    private int safeWallCount;
    private int failedWallCount;
    private int nextLetterIndex;
    private char nextLetter;
    private bool hasWarnedMissingPrefab;
    private bool isResultShown;
    private bool isGameRunning;

    private void Awake()
    {
        // A scene object can be assigned as the spawn template. Keep it out of
        // gameplay so contacts cannot remove cells from every subsequent clone.
        if (wallPrefab != null && wallPrefab.scene.IsValid())
        {
            wallPrefab.SetActive(false);
        }

        firstLetter = NormalizeLetter(firstLetter);
        nextLetterIndex = firstLetter - 'A';
        score = wallCount;
        PrepareNextLetter();
    }

    private void Start()
    {
        EnsureSpawnPoint();
        ValidateWallPrefab();
        lastSpawnTime = Time.time - spawnInterval;
        if (playOnStart && !controlledByGameLoop) BeginGame();
    }

    private void OnValidate()
    {
        firstLetter = NormalizeLetter(firstLetter);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        wallCount = Mathf.Max(1, wallCount);
        safeHitThreshold = Mathf.Max(0, safeHitThreshold);
    }

    private void Update()
    {
        if (isResultShown)
        {
            return;
        }

        if (!isGameRunning)
        {
            return;
        }

        if (ShouldSpawnWall())
        {
            SpawnWall();
            lastSpawnTime = Time.time;
        }

        MoveWalls();
        DestroyPassedWalls();
    }

    private void EnsureSpawnPoint()
    {
        if (spawnPoint == null)
        {
            spawnPoint = transform;
            Debug.LogWarning("WallManager: spawnPoint is not assigned. Using this transform.");
        }
    }

    private void ValidateWallPrefab()
    {
        if (wallPrefab == null)
        {
            Debug.LogError("WallManager: wallPrefab is not assigned.");
        }
    }

    private bool ShouldSpawnWall()
    {
        return spawnedWallCount < wallCount && Time.time - lastSpawnTime > spawnInterval;
    }

    private void SpawnWall()
    {
        if (wallPrefab == null)
        {
            if (!hasWarnedMissingPrefab)
            {
                Debug.LogError("WallManager: wallPrefab is missing. Assign a wall grid prefab in the Inspector.");
                hasWarnedMissingPrefab = true;
            }
            return;
        }

        char wallLetter = nextLetter;
        GameObject newWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);
        PlayerArenaLayers.Inherit(newWall, gameObject);
        var hits = newWall.AddComponent<WallHitTracker>();
        hits.Owner = this;
        hits.Velocity = Vector3.back * moveSpeed;
        // Scene templates are inactive. Run Awake before applying the mask,
        // since WallMaskData initializes its grid there.
        newWall.SetActive(true);
        ApplyWallMask(newWall, wallLetter);
        ApplyWallWidth(newWall);
        activeWalls.Add(newWall);
        spawnedWallCount++;
        OnProgressChanged?.Invoke(completedWallCount, wallCount);

        PrepareNextLetter();
    }

    private void ApplyWallWidth(GameObject wall)
    {
        if (wallWidthMeters <= 0f) return;
        float halfWidth = wallWidthMeters * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(wall.transform.lossyScale.x));
        // Crop the outer cells after fitting the complete hole inside this width.
        // Keep the authored grid dimensions compatible with saved pose files.
        foreach (var cube in wall.GetComponentsInChildren<WallCube>(true))
        {
            var cell = cube.transform;
            float halfCell = Mathf.Abs(cell.localScale.x) * 0.5f;
            float left = Mathf.Max(-halfWidth, cell.localPosition.x - halfCell);
            float right = Mathf.Min(halfWidth, cell.localPosition.x + halfCell);
            if (right - left <= 0.0001f)
            {
                cube.gameObject.SetActive(false);
                continue;
            }
            Vector3 position = cell.localPosition, scale = cell.localScale;
            position.x = (left + right) * 0.5f;
            scale.x = right - left;
            cell.localPosition = position;
            cell.localScale = scale;
        }
    }

    private void ApplyWallMask(GameObject wall, char letter)
    {
        WallMaskData maskData = wall.GetComponent<WallMaskData>();
        if (maskData == null)
        {
            maskData = wall.AddComponent<WallMaskData>();
        }

        maskData.ConfigureDimensionsFromWall(wall.transform);

        if (UsesSavedMasks)
        {
            var entry = roundMasks[spawnedWallCount];
            maskData.SetSavedMask((bool[,])entry.Mask.Clone());
            wall.name = $"Wall_Pose_{spawnedWallCount + 1}_{System.IO.Path.GetFileNameWithoutExtension(entry.Path)}";
        }
        else if (useMocopiPoseMask)
        {
            Animator sourceAnimator = ResolvePoseMaskAnimator();
            Transform projection = poseMaskProjection != null ? poseMaskProjection : wall.transform;
            maskData.SetMocopiPoseHole(sourceAnimator, projection);
            wall.name = "Wall_Pose";
        }
        else
        {
            maskData.SetAlphabetHole(letter);
            wall.name = $"Wall_{letter}";
        }

        float localWidth = wallWidthMeters / Mathf.Max(0.0001f, Mathf.Abs(wall.transform.lossyScale.x));
        maskData.ApplyMask(wall.transform, localWidth);
    }

    private Animator ResolvePoseMaskAnimator()
    {
        if (poseMaskAnimator != null && poseMaskAnimator.isHuman)
        {
            return poseMaskAnimator;
        }

        MocopiPlayerColliderRig rig = FindAnyObjectByType<MocopiPlayerColliderRig>();
        if (rig != null && rig.TryGetComponent(out Animator rigAnimator) && rigAnimator.isHuman)
        {
            return rigAnimator;
        }

        Animator animator = FindAnyObjectByType<Animator>();
        return animator != null && animator.isHuman ? animator : null;
    }

    private void PrepareNextLetter()
    {
        if (spawnedWallCount >= wallCount)
        {
            return;
        }

        if (randomizeLetters)
        {
            nextLetter = MaskPatternGenerator.GetRandomUppercaseLetter();
        }
        else
        {
            nextLetter = (char)('A' + nextLetterIndex);
            nextLetterIndex = (nextLetterIndex + 1) % AlphabetLetterCount;
        }

        OnNextLetterChanged?.Invoke(nextLetter);
    }

    private void MoveWalls()
    {
        foreach (GameObject wall in activeWalls)
        {
            if (wall != null)
            {
                wall.transform.position += Vector3.back * moveSpeed * Time.deltaTime;
                wall.GetComponent<WallHitTracker>().Velocity = Vector3.back * moveSpeed;
            }
        }
    }

    private void DestroyPassedWalls()
    {
        for (int i = activeWalls.Count - 1; i >= 0; i--)
        {
            GameObject wall = activeWalls[i];
            if (wall == null)
            {
                activeWalls.RemoveAt(i);
                continue;
            }

            if (wall.transform.position.z < destroyDistance)
            {
                RecordPassedWall(wall);
                Destroy(wall);
                activeWalls.RemoveAt(i);
                TryShowResult();
            }
        }
    }

    private void RecordPassedWall(GameObject wall)
    {
        int hitCount = CountHitCubes(wall);
        contactCount += hitCount;
        completedWallCount++;
        OnContactCountChanged?.Invoke(contactCount);

        if (hitCount <= safeHitThreshold)
        {
            safeWallCount++;
        }
        else
        {
            failedWallCount++;
            score = Mathf.Max(0, score - 1);
        }

        OnScoreChanged?.Invoke(score);
        OnProgressChanged?.Invoke(completedWallCount, wallCount);

        Debug.Log($"Wall resolved. Letter: {GetWallLetter(wall)}, Hit cubes: {hitCount}, Total contacts: {contactCount}, Score: {score}");
    }

    private int CountHitCubes(GameObject wall)
    {
        var hits = wall.GetComponent<WallHitTracker>();
        if (hits != null) return hits.HitCount;
        int count = 0;
        WallCube[] cubes = wall.GetComponentsInChildren<WallCube>(true);

        foreach (WallCube cube in cubes)
        {
            if (cube.IsHit)
            {
                count++;
            }
        }

        WallCube[] detachedCubes = FindObjectsByType<WallCube>(FindObjectsInactive.Include);
        foreach (WallCube cube in detachedCubes)
        {
            if (cube.OriginalWall == wall && cube.transform.root.gameObject != wall && cube.IsHit)
            {
                count++;
            }
        }

        return count;
    }

    private char GetWallLetter(GameObject wall)
    {
        WallMaskData maskData = wall.GetComponent<WallMaskData>();
        return maskData != null ? maskData.GetCurrentLetter() : '?';
    }

    private void TryShowResult()
    {
        if (isResultShown || completedWallCount < wallCount || activeWalls.Count > 0)
        {
            return;
        }

        isResultShown = true;
        ResultData result = new ResultData(
            score,
            wallCount,
            contactCount,
            safeWallCount,
            failedWallCount,
            safeHitThreshold);

        Debug.Log($"Result. Score: {score}/{wallCount}, Total contacts: {contactCount}, Safe: {safeWallCount}, Failed: {failedWallCount}");
        if (pauseOnResult)
        {
            Time.timeScale = 0f;
        }

        OnResult?.Invoke(result);
    }

    private char NormalizeLetter(char letter)
    {
        char normalized = char.ToUpperInvariant(letter);
        return normalized >= 'A' && normalized <= 'Z' ? normalized : 'A';
    }

    public int GetContactCount() => contactCount;
    public int GetScore() => score;
    public char GetNextLetter() => nextLetter;
    public int GetWallCount() => wallCount;
    public int GetSpawnedWallCount() => spawnedWallCount;
    public int GetCompletedWallCount() => completedWallCount;
    public int GetSafeWallCount() => safeWallCount;
    public int GetFailedWallCount() => failedWallCount;
    public int GetSafeHitThreshold() => safeHitThreshold;
    public bool IsGameRunning() => isGameRunning;
    public bool IsResultShown() => isResultShown;
    public void SetWallCount(int count) => wallCount = Mathf.Max(1, count);
    public void SetPlayOnStart(bool play) => playOnStart = play;
    public void SetControlledByGameLoop(bool controlled) => controlledByGameLoop = controlled;
    public void SetPauseOnResult(bool pause) => pauseOnResult = pause;

    public void BeginGame()
    {
        WallDebrisCollision.ConfigureScene(gameObject.scene, debrisFloors);
        Time.timeScale = 1f;
        if (isResultShown || completedWallCount >= wallCount)
        {
            ResetGame(clearWallsOnReset);
        }

        EnsureSpawnPoint();
        if (!roundPrepared) PrepareSavedRound();
        isGameRunning = true;
        lastSpawnTime = Time.time - spawnInterval;
        OnScoreChanged?.Invoke(score);
        OnContactCountChanged?.Invoke(contactCount);
        OnNextLetterChanged?.Invoke(nextLetter);
        OnProgressChanged?.Invoke(completedWallCount, wallCount);
    }

    public void StopGame()
    {
        isGameRunning = false;
    }

    public void ResetGame(bool destroyActiveWalls = true)
    {
        Time.timeScale = 1f;
        isGameRunning = false;
        isResultShown = false;
        contactCount = 0;
        spawnedWallCount = 0;
        completedWallCount = 0;
        safeWallCount = 0;
        failedWallCount = 0;
        score = wallCount;
        hasWarnedMissingPrefab = false;
        nextLetterIndex = NormalizeLetter(firstLetter) - 'A';
        roundPrepared = false;
        roundMasks.Clear();

        if (destroyActiveWalls)
        {
            ClearActiveWalls();
        }
        else
        {
            activeWalls.Clear();
        }

        PrepareNextLetter();
        lastSpawnTime = Time.time - spawnInterval;
        OnScoreChanged?.Invoke(score);
        OnContactCountChanged?.Invoke(contactCount);
        OnNextLetterChanged?.Invoke(nextLetter);
        OnProgressChanged?.Invoke(completedWallCount, wallCount);
    }

    private void PrepareSavedRound()
    {
        roundPrepared = true;
        roundMasks.Clear();
        SavedMaskStatus = string.Empty;
        if (!useSavedMasks || wallPrefab == null) return;
        // Inspect actual named cells, including the inactive cells in the prefab's preview mask.
        int rows = 0, columns = 0;
        foreach (var cube in wallPrefab.GetComponentsInChildren<WallCube>(true))
        {
            string[] parts = cube.name.Split('_');
            if (parts.Length == 3 && parts[0] == "Cube" &&
                int.TryParse(parts[1], out int row) && int.TryParse(parts[2], out int column))
            {
                rows = Mathf.Max(rows, row + 1);
                columns = Mathf.Max(columns, column + 1);
            }
        }
        bool useDefaultLibrary = string.IsNullOrWhiteSpace(savedMaskDirectory);
        string directory = useDefaultLibrary
            ? SavedMaskLibrary.BundledDirectory + " + " + Application.persistentDataPath : savedMaskDirectory;
        var library = useDefaultLibrary ? SavedMaskLibrary.LoadDefault(rows, columns)
            : SavedMaskLibrary.Load(directory, rows, columns);
        roundMasks = SavedMaskLibrary.SelectRound(library, wallCount, new System.Random());
        SavedMaskStatus = library.Count == 0
            ? $"保存済みの {rows}×{columns} の型がないため、従来の出題を使用します。"
            : $"保存済み {library.Count} 個から {wallCount} 問" +
              (library.Count < wallCount ? "（全種類の出題後に再抽選）" : "（重複なし）");
        Debug.Log($"WallManager: {SavedMaskStatus} Folder: {directory}", this);
    }

    public void ClearActiveWalls()
    {
        for (int i = activeWalls.Count - 1; i >= 0; i--)
        {
            if (activeWalls[i] != null)
            {
                ClearDetachedCubes(activeWalls[i]);
                Destroy(activeWalls[i]);
            }
        }

        activeWalls.Clear();
        // Includes debris whose original wall has already passed and been destroyed.
        foreach (var cube in FindObjectsByType<WallCube>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cube.IsHit && cube.Owner == this) Destroy(cube.gameObject);
    }

    private void ClearDetachedCubes(GameObject wall)
    {
        // Hit cubes are reparented to the scene, so destroying only the wall left debris between rounds.
        foreach (var cube in FindObjectsByType<WallCube>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (cube.OriginalWall == wall && !cube.transform.IsChildOf(wall.transform))
                Destroy(cube.gameObject);
    }

    public readonly struct ResultData
    {
        public ResultData(int score, int totalWalls, int totalContacts, int safeWalls, int failedWalls, int safeHitThreshold)
        {
            Score = score;
            TotalWalls = totalWalls;
            TotalContacts = totalContacts;
            SafeWalls = safeWalls;
            FailedWalls = failedWalls;
            SafeHitThreshold = safeHitThreshold;
        }

        public int Score { get; }
        public int TotalWalls { get; }
        public int TotalContacts { get; }
        public int SafeWalls { get; }
        public int FailedWalls { get; }
        public int SafeHitThreshold { get; }
    }
}
