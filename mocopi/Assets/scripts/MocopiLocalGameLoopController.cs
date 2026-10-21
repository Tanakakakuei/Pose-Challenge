using System;
using System.Collections;
using System.Collections.Generic;
using Mocopi.Receiver;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Single-scene game loop for local 1P, 2P co-op, and 2P versus play.
/// Give each player its own camera and WallManager lane for split-screen modes.
/// </summary>
public class MocopiLocalGameLoopController : MonoBehaviour
{
    public enum GameMode
    {
        Single,
        Coop,
        Versus
    }

    public enum GameLoopState
    {
        MocopiConnectionWait,
        AvatarSelection,
        GameModeSelection,
        Ready,
        Countdown,
        Playing,
        Result,

        [Obsolete("Use GameModeSelection. This alias is kept for old Inspector references.")]
        ModeSelection = GameModeSelection
    }

    public enum SplitScreenOrientation
    {
        Vertical,
        Horizontal
    }

    [Serializable]
    public class StateChangedEvent : UnityEvent<GameLoopState> { }

    [Serializable]
    public class LocalPlayerSlot
    {
        [SerializeField] private string displayName = "1P";
        [SerializeField] private Camera playerCamera;
        [SerializeField] private WallManager wallManager;
        [SerializeField] private Transform avatarSpawnPoint;
        [SerializeField] private List<GameObject> avatarRoots = new List<GameObject>();
        [SerializeField] private List<GameObject> avatarPrefabs = new List<GameObject>();
        [SerializeField] private Animator readyPoseAnimator;
        [SerializeField] private ReadyHandOrbVisualizer readyHandOrbVisualizer;
        [SerializeField] private Text statusText;
        [SerializeField] private Text scoreText;

        private GameObject spawnedAvatarRoot;
        private DebugMousePlayerCollider debugMouseCollider;
        private int selectedAvatarIndex;
        private bool readyConfirmed;
        private WallManager.ResultData lastResult;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? "Player" : displayName.Trim();
        public Camera PlayerCamera => playerCamera;
        public WallManager WallManager => wallManager;
        public Animator ReadyPoseAnimator => ResolveReadyPoseAnimator();
        public bool ReadyConfirmed => readyConfirmed;
        public WallManager.ResultData LastResult => lastResult;
        public MocopiAvatar CurrentMocopiAvatar => ResolveMocopiAvatar();
        public string AvatarDisplayName => CurrentMocopiAvatar != null
            ? CurrentMocopiAvatar.name.Replace(DisplayName + "_", string.Empty) : "Avatar";

        public void SetActive(bool active)
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(active);
            }

            if (wallManager != null)
            {
                wallManager.gameObject.SetActive(active);
                if (!active)
                {
                    wallManager.StopGame();
                }
            }

            SetAvatarActive(active);
            SetStatus(active ? string.Empty : "Inactive");
            SetScore(string.Empty);
        }

        public void SetDebugMouseCollision(
            bool active,
            Transform parent,
            float colliderRadius,
            float planeZ,
            bool requireMouseButton,
            int mouseButton,
            float clickActiveSeconds,
            bool showVisual,
            Color visualColor)
        {
            if (!active)
            {
                if (debugMouseCollider != null)
                {
                    debugMouseCollider.gameObject.SetActive(false);
                }

                return;
            }

            if (debugMouseCollider == null)
            {
                GameObject debugObject = new GameObject($"{DisplayName}_DebugMousePlayerCollider");
                if (parent != null)
                {
                    debugObject.transform.SetParent(parent, false);
                }

                debugMouseCollider = debugObject.AddComponent<DebugMousePlayerCollider>();
            }

            debugMouseCollider.Configure(
                playerCamera,
                colliderRadius,
                planeZ,
                requireMouseButton,
                mouseButton,
                clickActiveSeconds,
                showVisual,
                visualColor);
            debugMouseCollider.gameObject.SetActive(true);
        }

        public void PrepareAvatarSelection()
        {
            selectedAvatarIndex = Mathf.Clamp(selectedAvatarIndex, 0, Mathf.Max(0, GetAvatarChoiceCount() - 1));
            ApplySelectedAvatar();
            readyConfirmed = false;
            lastResult = default;
            SetStatus($"{DisplayName}: choose avatar");
        }

        public void ChooseAvatar(int index)
        {
            int avatarCount = GetAvatarChoiceCount();
            selectedAvatarIndex = avatarCount > 0 ? Mathf.Clamp(index, 0, avatarCount - 1) : 0;
            ApplySelectedAvatar();
            SetStatus($"{DisplayName}: avatar {selectedAvatarIndex + 1}");
        }

        public void ChooseNextAvatar()
        {
            int avatarCount = GetAvatarChoiceCount();
            if (avatarCount == 0)
            {
                return;
            }

            ChooseAvatar((selectedAvatarIndex + 1) % avatarCount);
        }

        public void ChoosePreviousAvatar()
        {
            int avatarCount = GetAvatarChoiceCount();
            if (avatarCount == 0)
            {
                return;
            }

            ChooseAvatar((selectedAvatarIndex - 1 + avatarCount) % avatarCount);
        }

        public void PrepareReady()
        {
            readyConfirmed = false;
            if (readyHandOrbVisualizer != null)
            {
                readyHandOrbVisualizer.SetTargetAnimator(ResolveReadyPoseAnimator());
                readyHandOrbVisualizer.SetVisible(true);
            }

            SetStatus($"{DisplayName}: ready?");
        }

        public void SetReady(bool ready)
        {
            readyConfirmed = ready;
            SetStatus(ready ? $"{DisplayName}: ready" : $"{DisplayName}: ready?");
        }

        public void ClearReadyVisuals()
        {
            if (readyHandOrbVisualizer != null)
            {
                readyHandOrbVisualizer.SetVisible(false);
            }
        }

        public void ResetRound()
        {
            lastResult = default;
            if (wallManager != null)
            {
                wallManager.ResetGame();
            }

            RefreshScore();
        }

        public void BeginRound()
        {
            if (wallManager != null)
            {
                wallManager.BeginGame();
            }
        }

        public void StopRound()
        {
            if (wallManager != null)
            {
                wallManager.StopGame();
            }
        }

        public void StoreResult(WallManager.ResultData result)
        {
            lastResult = result;
            RefreshScore();
        }

        public void RefreshScore()
        {
            if (wallManager == null)
            {
                SetScore(string.Empty);
                return;
            }

            SetScore($"{DisplayName}  {wallManager.GetScore()}/{wallManager.GetWallCount()}  Hits {wallManager.GetContactCount()}");
        }

        public bool HasRecentMocopiFrame(float timeoutSeconds)
        {
            MocopiAvatar avatar = ResolveMocopiAvatar();
            return avatar != null && MocopiConnectionMonitor.HasRecentFrame(avatar, timeoutSeconds);
        }

        public string GetConnectionStatusText(float timeoutSeconds)
        {
            MocopiAvatar avatar = ResolveMocopiAvatar();
            if (avatar == null)
            {
                return $"{DisplayName}: no avatar";
            }

            if (MocopiConnectionMonitor.HasRecentFrame(avatar, timeoutSeconds))
            {
                int port = MocopiConnectionMonitor.GetPort(avatar);
                return port >= 0 ? $"{DisplayName}: Connected ({port})" : $"{DisplayName}: Connected";
            }

            float secondsSinceLastFrame = MocopiConnectionMonitor.GetSecondsSinceLastFrame(avatar);
            return float.IsPositiveInfinity(secondsSinceLastFrame)
                ? $"{DisplayName}: Waiting"
                : $"{DisplayName}: Waiting ({secondsSinceLastFrame:0.0}s)";
        }

        public void SetStatus(string value)
        {
            if (statusText != null)
            {
                statusText.text = value;
            }
        }

        private void SetScore(string value)
        {
            if (scoreText != null)
            {
                scoreText.text = value;
            }
        }

        private int GetAvatarChoiceCount()
        {
            if (avatarPrefabs.Count > 0)
            {
                return avatarPrefabs.Count;
            }

            return avatarRoots.Count;
        }

        private void ApplySelectedAvatar()
        {
            if (avatarPrefabs.Count > 0)
            {
                SpawnSelectedAvatarPrefab();
                return;
            }

            for (int i = 0; i < avatarRoots.Count; i++)
            {
                if (avatarRoots[i] != null)
                {
                    avatarRoots[i].SetActive(i == selectedAvatarIndex);
                }
            }
        }

        private void SpawnSelectedAvatarPrefab()
        {
            DestroySpawnedAvatar();
            GameObject selectedPrefab = avatarPrefabs[Mathf.Clamp(selectedAvatarIndex, 0, avatarPrefabs.Count - 1)];
            if (selectedPrefab == null)
            {
                return;
            }

            Vector3 position = avatarSpawnPoint != null ? avatarSpawnPoint.position : Vector3.zero;
            Quaternion rotation = avatarSpawnPoint != null ? avatarSpawnPoint.rotation : Quaternion.identity;
            spawnedAvatarRoot = UnityEngine.Object.Instantiate(selectedPrefab, position, rotation);
            if (avatarSpawnPoint != null) PlayerArenaLayers.Inherit(spawnedAvatarRoot, avatarSpawnPoint.gameObject);
            spawnedAvatarRoot.name = $"{DisplayName}_{selectedPrefab.name}";
        }

        private void DestroySpawnedAvatar()
        {
            if (spawnedAvatarRoot != null)
            {
                UnityEngine.Object.Destroy(spawnedAvatarRoot);
                spawnedAvatarRoot = null;
            }
        }

        private void SetAvatarActive(bool active)
        {
            if (spawnedAvatarRoot != null)
            {
                spawnedAvatarRoot.SetActive(active);
            }

            for (int i = 0; i < avatarRoots.Count; i++)
            {
                if (avatarRoots[i] != null)
                {
                    avatarRoots[i].SetActive(active && i == selectedAvatarIndex);
                }
            }
        }

        private Animator ResolveReadyPoseAnimator()
        {
            if (readyPoseAnimator != null && readyPoseAnimator.isHuman && readyPoseAnimator.gameObject.activeInHierarchy)
            {
                return readyPoseAnimator;
            }

            if (spawnedAvatarRoot != null)
            {
                Animator spawnedAnimator = spawnedAvatarRoot.GetComponentInChildren<Animator>();
                if (spawnedAnimator != null && spawnedAnimator.isHuman)
                {
                    return spawnedAnimator;
                }
            }

            for (int i = 0; i < avatarRoots.Count; i++)
            {
                if (avatarRoots[i] == null || !avatarRoots[i].activeInHierarchy)
                {
                    continue;
                }

                Animator animator = avatarRoots[i].GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman)
                {
                    return animator;
                }
            }

            return readyPoseAnimator;
        }

        private MocopiAvatar ResolveMocopiAvatar()
        {
            if (spawnedAvatarRoot != null)
            {
                MocopiAvatar spawnedAvatar = spawnedAvatarRoot.GetComponentInChildren<MocopiAvatar>(true);
                if (spawnedAvatar != null)
                {
                    return spawnedAvatar;
                }
            }

            if (avatarRoots.Count > 0)
            {
                int clampedIndex = Mathf.Clamp(selectedAvatarIndex, 0, avatarRoots.Count - 1);
                GameObject selectedRoot = avatarRoots[clampedIndex];
                if (selectedRoot != null)
                {
                    MocopiAvatar selectedAvatar = selectedRoot.GetComponentInChildren<MocopiAvatar>(true);
                    if (selectedAvatar != null)
                    {
                        return selectedAvatar;
                    }
                }
            }

            return null;
        }
    }

    [Header("Mode")]
    [SerializeField] private GameMode gameMode = GameMode.Single;
    [SerializeField, Range(1, 2)] private int selectedPlayerCount = 1;
    [SerializeField] private SplitScreenOrientation splitScreenOrientation = SplitScreenOrientation.Vertical;
    [SerializeField] private bool autoReadyWhenPoseIsUnavailable = true;
    [SerializeField] private bool controlResultPause = true;
    [SerializeField] private bool requireRecentMocopiFrame = true;
    [SerializeField] private float mocopiConnectionTimeoutSeconds = 1f;
    [SerializeField] private float singlePlayerConnectionPoseHoldSeconds = 1.5f;

    [Header("Debug Mode")]
    [SerializeField] private bool debugModeOn;
    [SerializeField] private bool debugSkipMocopiConnection = true;
    [SerializeField] private bool debugAutoReady = true;
    [SerializeField] private bool debugUseMouseCollision = true;
    [Tooltip("Mouse-only world radius in metres. This is independent of MocopiPlayerColliderRig's body radii.")]
    [SerializeField] private float debugMouseColliderRadius = 0.18f;
    [SerializeField] private float debugMousePlaneZ;
    [SerializeField] private bool debugMouseRequiresClick = true;
    [SerializeField] private int debugMouseButton;
    [SerializeField] private float debugMouseClickActiveSeconds = 0.08f;
    [SerializeField] private bool debugShowMouseCollider = true;
    [SerializeField] private Color debugMouseColliderColor = new Color(0f, 0.85f, 1f, 0.35f);
    private bool debugColliderSettingsPending;

    [Header("State Panels")]
    [Tooltip("mocopi connection status screen. 2P starts automatically when both players are connected. 1P starts from the overhead circle pose.")]
    [SerializeField] private GameObject mocopiConnectionWaitPanel;
    [Tooltip("Avatar/character selection screen. This is not the game mode selection screen.")]
    [SerializeField] private GameObject avatarSelectionPanel;
    [FormerlySerializedAs("modeSelectionPanel")]
    [Tooltip("2P only: choose Co-op or Versus. This used to be called Mode Selection; it is not Model/Avatar selection.")]
    [SerializeField] private GameObject gameModeSelectionPanel;
    [Tooltip("Ready-pose screen. Active players make the overhead circle pose here.")]
    [SerializeField] private GameObject readyPanel;
    [Tooltip("Countdown shown immediately before walls start.")]
    [SerializeField] private GameObject countdownPanel;
    [Tooltip("Shown while WallManagers are running.")]
    [SerializeField] private GameObject playingPanel;
    [Tooltip("Round result and retry/menu buttons.")]
    [SerializeField] private GameObject resultPanel;

    [Header("Labels")]
    [SerializeField] private Text stateTitleText;
    [SerializeField] private Text stateMessageText;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text resultText;

    [Header("Players")]
    [SerializeField] private List<LocalPlayerSlot> players = new List<LocalPlayerSlot>();

    [Header("Ready Pose")]
    [SerializeField] private float readyHoldSeconds = 2f;
    [SerializeField] private float handTouchDistance = 0.18f;
    [SerializeField] private float handsAboveHeadHeight = 0.08f;
    [SerializeField] private float maxHandsHeadHorizontalDistance = 0.45f;

    public const int CountdownSeconds = 5;
    private const float StartDisplaySeconds = 0.75f;
    public const float ResultDelaySeconds = 0.5f;
    private TouchableUIPanel[] countdownTouchPanels;
    private Font countdownFallbackFont;

    [Header("Events")]
    [SerializeField] private StateChangedEvent onStateChanged = new StateChangedEvent();

    private readonly Dictionary<WallManager, int> wallManagerToPlayerIndex = new Dictionary<WallManager, int>();
    private readonly Dictionary<WallManager, Action<WallManager.ResultData>> resultHandlers = new Dictionary<WallManager, Action<WallManager.ResultData>>();
    private readonly Dictionary<WallManager, Action<int>> scoreHandlers = new Dictionary<WallManager, Action<int>>();
    private readonly HashSet<int> finishedPlayers = new HashSet<int>();
    private readonly List<float> readyHoldTimes = new List<float>();
    private GameLoopState currentState;
    private Coroutine countdownRoutine;
    private int finishedPlayerCount;
    private double resultDisplayAt = -1;

    public GameLoopState CurrentState => currentState;
    public GameMode CurrentMode => gameMode;
    public StateChangedEvent OnStateChanged => onStateChanged;
    public IReadOnlyList<LocalPlayerSlot> Players => players;
    public int ActivePlayerCount => GetActivePlayerCount();
    public bool DebugModeOn => debugModeOn;
    public string StateMessage => GetStateMessage(currentState);
    public float GetReadyProgress(int index) => index < readyHoldTimes.Count
        ? Mathf.Clamp01(readyHoldTimes[index] / Mathf.Max(0.01f, readyHoldSeconds)) : 0;

    private void Awake()
    {
        EnsurePlayerLists();
        SubscribeWallManagers();
        ApplyModeLayout();
    }

    private void Start()
    {
        ChangeState(ShouldSkipConnectionWait() ? GameLoopState.AvatarSelection : GameLoopState.MocopiConnectionWait);
    }

    private void OnDestroy()
    {
        StopAllRounds();
        UnsubscribeWallManagers();
        if (countdownFallbackFont != null) Destroy(countdownFallbackFont);
    }

    private void OnValidate() => debugColliderSettingsPending = true;

    private void Update()
    {
        if (currentState == GameLoopState.Playing && resultDisplayAt >= 0 &&
            Time.realtimeSinceStartupAsDouble >= resultDisplayAt)
        {
            ChangeState(GameLoopState.Result);
        }

        if (debugColliderSettingsPending)
        {
            debugColliderSettingsPending = false;
            RefreshDebugMouseColliders();
        }
        if (currentState == GameLoopState.MocopiConnectionWait)
        {
            UpdateConnectionWaitState();
            UpdateStateLabels();
            return;
        }

        if (currentState == GameLoopState.Ready)
        {
            UpdateReadyState();
        }
    }

    public void SetSingleMode()
    {
        SetGameMode(GameMode.Single);
    }

    public void SetCoopMode()
    {
        SetGameMode(GameMode.Coop);
    }

    public void SetVersusMode()
    {
        SetGameMode(GameMode.Versus);
    }

    public void SetGameMode(GameMode mode)
    {
        gameMode = mode;
        selectedPlayerCount = mode == GameMode.Single ? 1 : 2;
        ApplyModeLayout();

        if (currentState == GameLoopState.GameModeSelection)
        {
            ChangeState(GameLoopState.Ready);
        }
        else if (currentState != GameLoopState.MocopiConnectionWait)
        {
            ChangeState(ShouldSkipConnectionWait() ? GameLoopState.AvatarSelection : GameLoopState.MocopiConnectionWait);
        }
        else
        {
            UpdateStateLabels();
        }
    }

    public void SetDebugMode(bool enabled)
    {
        debugModeOn = enabled;
        ApplyModeLayout();

        if (ShouldSkipConnectionWait() && currentState == GameLoopState.MocopiConnectionWait)
        {
            ConfirmMocopiConnected();
        }
    }

    public void ToggleDebugMode()
    {
        SetDebugMode(!debugModeOn);
    }

    public void ConfirmMocopiConnected()
    {
        if (currentState != GameLoopState.MocopiConnectionWait) return;
        int connectedPlayers = GetConnectedPlayerCount();
        if (ShouldSkipConnectionWait())
        {
            BeginConnectedPlayerFlow(gameMode == GameMode.Single ? 1 : 2);
            return;
        }

        if (connectedPlayers > 0 && IsPlayerMocopiConnected(0))
            BeginConnectedPlayerFlow(connectedPlayers >= 2 ? 2 : 1);
    }

    public void ConfirmTwoPlayerModeSelection()
    {
        if (currentState != GameLoopState.GameModeSelection) return;
        if (GetActivePlayerCount() >= 2)
        {
            if (gameMode == GameMode.Single)
            {
                gameMode = GameMode.Coop;
            }

            ChangeState(GameLoopState.Ready);
        }
    }

    private void BeginConnectedPlayerFlow(int playerCount)
    {
        selectedPlayerCount = Mathf.Clamp(playerCount, 1, Mathf.Min(2, players.Count));
        gameMode = selectedPlayerCount == 1 ? GameMode.Single : GameMode.Coop;
        ApplyModeLayout();
        ChangeState(GameLoopState.AvatarSelection);
    }

    public void ChooseAvatarForPlayer1(int avatarIndex)
    {
        ChooseAvatarForPlayer(0, avatarIndex);
    }

    public void ChooseAvatarForPlayer2(int avatarIndex)
    {
        ChooseAvatarForPlayer(1, avatarIndex);
    }

    public void ChooseAvatarForPlayer(int playerIndex, int avatarIndex)
    {
        if (currentState != GameLoopState.AvatarSelection) return;
        LocalPlayerSlot player = GetActivePlayer(playerIndex);
        if (player != null)
        {
            player.ChooseAvatar(avatarIndex);
        }
    }

    public void ConfirmAvatarSelection()
    {
        if (currentState != GameLoopState.AvatarSelection) return;
        ChangeState(GetActivePlayerCount() >= 2 ? GameLoopState.GameModeSelection : GameLoopState.Ready);
    }

    public void MarkPlayer1Ready()
    {
        SetPlayerReady(0, true);
    }

    public void MarkPlayer2Ready()
    {
        SetPlayerReady(1, true);
    }

    public void SetPlayerReady(int playerIndex, bool ready)
    {
        if (currentState != GameLoopState.Ready) return;
        LocalPlayerSlot player = GetActivePlayer(playerIndex);
        if (player != null)
        {
            player.SetReady(ready);
        }

        if (AllActivePlayersReady())
        {
            ChangeState(GameLoopState.Countdown);
        }
    }

    public void Retry()
    {
        if (currentState != GameLoopState.Result) return;
        ChangeState(GameLoopState.Ready);
    }

    public void StartDebugRound()
    {
        if (!debugModeOn || currentState != GameLoopState.Ready) return;
        for (int i = 0; i < GetActivePlayerCount(); i++) players[i].SetReady(true);
        ChangeState(GameLoopState.Countdown);
    }

    public void ReturnToAvatarSelection()
    {
        ChangeState(GameLoopState.AvatarSelection);
    }

    public void ReturnToConnectionWait()
    {
        ChangeState(GameLoopState.MocopiConnectionWait);
    }

    private void ChangeState(GameLoopState nextState)
    {
        // Leaving the round also cancels any pending result transition.
        resultDisplayAt = -1;
        StopCountdown();
        foreach (var player in players) player.ClearReadyVisuals();
        currentState = nextState;

        SetActive(mocopiConnectionWaitPanel, nextState == GameLoopState.MocopiConnectionWait);
        SetActive(avatarSelectionPanel, nextState == GameLoopState.AvatarSelection);
        SetActive(gameModeSelectionPanel, nextState == GameLoopState.GameModeSelection);
        SetActive(readyPanel, nextState == GameLoopState.Ready);
        SetActive(countdownPanel, nextState == GameLoopState.Countdown);
        SetActive(playingPanel, nextState == GameLoopState.Playing);
        SetActive(resultPanel, nextState == GameLoopState.Result);

        if (nextState == GameLoopState.MocopiConnectionWait)
        {
            StopAllRounds();
            ResetRounds();
            // Both receivers must stay alive while detecting a second player.
            foreach (var player in players) player.PrepareAvatarSelection();
        }
        else if (nextState == GameLoopState.AvatarSelection)
        {
            StopAllRounds();
            ResetRounds();
            PrepareAvatarSelection();
        }
        else if (nextState == GameLoopState.GameModeSelection)
        {
            StopAllRounds();
            gameMode = gameMode == GameMode.Single ? GameMode.Coop : gameMode;
            ApplyModeLayout();
        }
        else if (nextState == GameLoopState.Ready)
        {
            StopAllRounds();
            ResetRounds();
            PrepareReady();
        }
        else if (nextState == GameLoopState.Countdown)
        {
            PrepareCountdown();
        }
        else if (nextState == GameLoopState.Playing)
        {
            BeginRounds();
        }
        else if (nextState == GameLoopState.Result)
        {
            StopAllRounds();
            ShowResult();
        }

        UpdateStateLabels();
        onStateChanged.Invoke(currentState);
    }

    private void PrepareAvatarSelection()
    {
        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            players[i].PrepareAvatarSelection();
            var camera = players[i].PlayerCamera;
            if (camera != null && camera.TryGetComponent<MocopiFirstPersonCamera>(out var follow))
                follow.SetTargetAnimator(players[i].ReadyPoseAnimator);
        }
    }

    private void UpdateConnectionWaitState()
    {
        if (ShouldSkipConnectionWait())
        {
            BeginConnectedPlayerFlow(gameMode == GameMode.Single ? 1 : 2);
            return;
        }

        int connectedPlayers = GetConnectedPlayerCount();
        if (connectedPlayers >= 2)
        {
            BeginConnectedPlayerFlow(2);
            return;
        }

        EnsurePlayerLists();
        if (connectedPlayers == 1 && IsPlayerMocopiConnected(0))
        {
            if (IsReadyPoseHeld(players[0].ReadyPoseAnimator))
            {
                readyHoldTimes[0] = Mathf.Min(singlePlayerConnectionPoseHoldSeconds, readyHoldTimes[0] + Time.unscaledDeltaTime);
                players[0].SetStatus($"1P start: {Mathf.CeilToInt(singlePlayerConnectionPoseHoldSeconds - readyHoldTimes[0])}");
                if (readyHoldTimes[0] >= singlePlayerConnectionPoseHoldSeconds)
                {
                    BeginConnectedPlayerFlow(1);
                }
            }
            else
            {
                readyHoldTimes[0] = 0f;
                players[0].SetStatus("1P: make overhead circle");
            }
        }
        else
        {
            if (readyHoldTimes.Count > 0)
            {
                readyHoldTimes[0] = 0f;
            }
        }
    }

    private void PrepareReady()
    {
        EnsurePlayerLists();
        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            readyHoldTimes[i] = 0f;
            players[i].PrepareReady();
        }
    }

    private void UpdateReadyState()
    {
        if (debugModeOn && debugAutoReady)
        {
            for (int i = 0; i < GetActivePlayerCount(); i++)
            {
                players[i].SetReady(true);
            }

            ChangeState(GameLoopState.Countdown);
            return;
        }

        bool allReady = true;

        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            LocalPlayerSlot player = players[i];
            if (player.ReadyConfirmed)
            {
                continue;
            }

            if (IsReadyPoseHeld(player.ReadyPoseAnimator))
            {
                readyHoldTimes[i] = Mathf.Min(readyHoldSeconds, readyHoldTimes[i] + Time.unscaledDeltaTime);
                player.SetStatus($"{player.DisplayName}: {Mathf.CeilToInt(readyHoldSeconds - readyHoldTimes[i])}");
                if (readyHoldTimes[i] >= readyHoldSeconds)
                {
                    player.SetReady(true);
                }
            }
            else if (autoReadyWhenPoseIsUnavailable && player.ReadyPoseAnimator == null)
            {
                player.SetReady(true);
            }
            else
            {
                readyHoldTimes[i] = 0f;
                player.SetStatus($"{player.DisplayName}: ready pose");
            }

            allReady &= player.ReadyConfirmed;
        }

        if (allReady)
        {
            ChangeState(GameLoopState.Countdown);
        }
    }

    private bool IsReadyPoseHeld(Animator animator)
    {
        if (animator == null || !animator.isHuman)
        {
            return false;
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (head == null || leftHand == null || rightHand == null)
        {
            return false;
        }

        Vector3 leftHandPosition = leftHand.position;
        Vector3 rightHandPosition = rightHand.position;
        Vector3 headPosition = head.position;
        Vector3 handsMidpoint = (leftHandPosition + rightHandPosition) * 0.5f;
        Vector2 horizontalOffset = new Vector2(handsMidpoint.x - headPosition.x, handsMidpoint.z - headPosition.z);

        bool handsTouching = Vector3.Distance(leftHandPosition, rightHandPosition) <= handTouchDistance;
        bool handsAboveHead = leftHandPosition.y >= headPosition.y + handsAboveHeadHeight &&
                              rightHandPosition.y >= headPosition.y + handsAboveHeadHeight;
        bool handsOverHead = horizontalOffset.magnitude <= maxHandsHeadHorizontalDistance;
        return handsTouching && handsAboveHead && handsOverHead;
    }

    private void PrepareCountdown()
    {
        // Older scenes display the countdown with a world-space TouchableUIPanel
        // rather than an assigned Text. Support both without requiring scene rewiring.
        if (countdownText == null && countdownPanel != null)
        {
            countdownTouchPanels = countdownPanel.GetComponentsInChildren<TouchableUIPanel>(true);
            if (countdownFallbackFont == null)
                countdownFallbackFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP" }, 64);
            foreach (var panel in countdownTouchPanels)
            {
                var label = panel.GetComponentInChildren<Text>(true);
                if (label == null || label.font == null || label.font.name == "LegacyRuntime" || label.font.name == "Arial")
                    panel.SetFont(countdownFallbackFont);
            }
        }
        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            players[i].ClearReadyVisuals();
        }

        countdownRoutine = StartCoroutine(CountdownAndStart());
    }

    private IEnumerator CountdownAndStart()
    {
        int remaining = CountdownSeconds;
        while (remaining > 0)
        {
            SetCountdownDisplay(remaining.ToString());

            yield return new WaitForSecondsRealtime(1f);
            remaining--;
        }

        SetCountdownDisplay("スタート！");

        yield return new WaitForSecondsRealtime(StartDisplaySeconds);
        countdownRoutine = null;
        ChangeState(GameLoopState.Playing);
    }

    private void SetCountdownDisplay(string value)
    {
        if (countdownText != null)
        {
            countdownText.text = value;
        }
        else if (countdownTouchPanels != null)
        {
            foreach (var panel in countdownTouchPanels)
                if (panel != null) panel.SetLabel(value);
        }
    }

    private void StopCountdown()
    {
        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
            SetCountdownDisplay(string.Empty);
        }
    }

    private void BeginRounds()
    {
        finishedPlayerCount = 0;
        finishedPlayers.Clear();
        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            players[i].BeginRound();
        }
    }

    private void StopAllRounds()
    {
        for (int i = 0; i < players.Count; i++)
        {
            players[i].StopRound();
        }
    }

    private void ResetRounds()
    {
        finishedPlayerCount = 0;
        finishedPlayers.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            players[i].ResetRound();
        }
    }

    private void HandlePlayerResult(WallManager.ResultData result, int playerIndex)
    {
        if (currentState != GameLoopState.Playing || playerIndex < 0 || playerIndex >= GetActivePlayerCount() || !finishedPlayers.Add(playerIndex))
        {
            return;
        }

        players[playerIndex].StoreResult(result);
        finishedPlayerCount++;

        if (finishedPlayerCount >= GetActivePlayerCount())
        {
            StopAllRounds();
            resultDisplayAt = Time.realtimeSinceStartupAsDouble + ResultDelaySeconds;
        }
    }

    private void ShowResult()
    {
        if (resultText == null)
        {
            return;
        }

        if (gameMode == GameMode.Single)
        {
            WallManager.ResultData result = players[0].LastResult;
            resultText.text =
                $"<size=42>{result.Score} / {result.TotalWalls}</size>\n" +
                $"クリア {result.SafeWalls} 回   /   ミス {result.FailedWalls} 回\n" +
                $"接触したブロック：{result.TotalContacts} 個";
            return;
        }

        WallManager.ResultData p1 = players[0].LastResult;
        WallManager.ResultData p2 = players[1].LastResult;

        if (gameMode == GameMode.Coop)
        {
            int totalScore = p1.Score + p2.Score;
            int totalWalls = p1.TotalWalls + p2.TotalWalls;
            int totalHits = p1.TotalContacts + p2.TotalContacts;
            resultText.text =
                $"チームスコア  {totalScore} / {totalWalls}\n" +
                $"1P: {p1.Score}/{p1.TotalWalls}  2P: {p2.Score}/{p2.TotalWalls}\n" +
                $"接触したブロック：{totalHits} 個";
            return;
        }

        string winner = p1.Score == p2.Score ? "引き分け！" : p1.Score > p2.Score ? "1P の勝ち！" : "2P の勝ち！";
        resultText.text =
            $"{winner}\n" +
            $"1P: {p1.Score}/{p1.TotalWalls}  Hits {p1.TotalContacts}\n" +
            $"2P: {p2.Score}/{p2.TotalWalls}  Hits {p2.TotalContacts}";
    }

    private void SubscribeWallManagers()
    {
        UnsubscribeWallManagers();
        for (int i = 0; i < players.Count; i++)
        {
            WallManager wallManager = players[i].WallManager;
            if (wallManager == null || wallManagerToPlayerIndex.ContainsKey(wallManager))
            {
                continue;
            }

            if (controlResultPause)
            {
                wallManager.SetPauseOnResult(false);
            }

            wallManager.SetControlledByGameLoop(true);
            wallManager.SetPlayOnStart(false);
            wallManager.StopGame();

            int playerIndex = i;
            wallManagerToPlayerIndex.Add(wallManager, playerIndex);
            Action<WallManager.ResultData> resultHandler = result => HandlePlayerResult(result, playerIndex);
            Action<int> scoreHandler = _ => players[playerIndex].RefreshScore();
            resultHandlers.Add(wallManager, resultHandler);
            scoreHandlers.Add(wallManager, scoreHandler);
            wallManager.OnResult += resultHandler;
            wallManager.OnScoreChanged += scoreHandler;
            wallManager.OnContactCountChanged += scoreHandler;
        }
    }

    private void UnsubscribeWallManagers()
    {
        foreach (var pair in resultHandlers)
            if (pair.Key != null) pair.Key.OnResult -= pair.Value;
        foreach (var pair in scoreHandlers)
            if (pair.Key != null)
            {
                pair.Key.OnScoreChanged -= pair.Value;
                pair.Key.OnContactCountChanged -= pair.Value;
            }
        resultHandlers.Clear();
        scoreHandlers.Clear();
        wallManagerToPlayerIndex.Clear();
    }

    private void ApplyModeLayout()
    {
        int activePlayerCount = GetActivePlayerCount();
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SetActive(i < activePlayerCount);
        }
        RefreshDebugMouseColliders();

        if (activePlayerCount <= 0)
        {
            return;
        }

        if (activePlayerCount == 1)
        {
            SetCameraRect(players[0].PlayerCamera, new Rect(0f, 0f, 1f, 1f));
            return;
        }

        if (splitScreenOrientation == SplitScreenOrientation.Vertical)
        {
            SetCameraRect(players[0].PlayerCamera, new Rect(0f, 0f, 0.5f, 1f));
            SetCameraRect(players[1].PlayerCamera, new Rect(0.5f, 0f, 0.5f, 1f));
        }
        else
        {
            SetCameraRect(players[0].PlayerCamera, new Rect(0f, 0.5f, 1f, 0.5f));
            SetCameraRect(players[1].PlayerCamera, new Rect(0f, 0f, 1f, 0.5f));
        }
    }

    private void RefreshDebugMouseColliders()
    {
        for (int i = 0; i < players.Count; i++)
            players[i].SetDebugMouseCollision(
                debugModeOn && debugUseMouseCollision && i < GetActivePlayerCount(),
                transform, debugMouseColliderRadius, debugMousePlaneZ, debugMouseRequiresClick,
                debugMouseButton, debugMouseClickActiveSeconds, debugShowMouseCollider, debugMouseColliderColor);
    }

    private bool HasRequiredMocopiCount()
    {
        if (ShouldSkipConnectionWait())
        {
            return true;
        }

        if (!requireRecentMocopiFrame)
        {
            return MocopiAvatar.Avatars.Count >= GetActivePlayerCount();
        }

        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            if (!players[i].HasRecentMocopiFrame(mocopiConnectionTimeoutSeconds))
            {
                return false;
            }
        }

        return true;
    }

    private int GetConnectedPlayerCount()
    {
        int maxPlayersToCheck = Mathf.Min(2, players.Count);
        int connectedCount = 0;
        for (int i = 0; i < maxPlayersToCheck; i++)
        {
            if (IsPlayerMocopiConnected(i))
            {
                connectedCount++;
            }
        }

        return connectedCount;
    }

    private bool IsPlayerMocopiConnected(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            return false;
        }

        if (!requireRecentMocopiFrame)
        {
            return players[playerIndex].CurrentMocopiAvatar != null;
        }

        return players[playerIndex].HasRecentMocopiFrame(mocopiConnectionTimeoutSeconds);
    }

    private bool ShouldSkipConnectionWait()
    {
        return debugModeOn && debugSkipMocopiConnection;
    }

    private bool AllActivePlayersReady()
    {
        for (int i = 0; i < GetActivePlayerCount(); i++)
        {
            if (!players[i].ReadyConfirmed)
            {
                return false;
            }
        }

        return true;
    }

    private LocalPlayerSlot GetActivePlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= GetActivePlayerCount())
        {
            return null;
        }

        return players[playerIndex];
    }

    private int GetActivePlayerCount()
    {
        if (players.Count == 0)
        {
            return 0;
        }

        return Mathf.Clamp(selectedPlayerCount, 1, Mathf.Min(2, players.Count));
    }

    private void EnsurePlayerLists()
    {
        while (readyHoldTimes.Count < players.Count)
        {
            readyHoldTimes.Add(0f);
        }
    }

    private void UpdateStateLabels()
    {
        if (stateTitleText != null)
        {
            stateTitleText.text = GetStateTitle(currentState);
        }

        if (stateMessageText != null)
        {
            stateMessageText.text = GetStateMessage(currentState);
        }
    }

    private string GetStateTitle(GameLoopState state)
    {
        switch (state)
        {
            case GameLoopState.MocopiConnectionWait:
                return "mocopi を接続";
            case GameLoopState.AvatarSelection:
                return "アバターを確認";
            case GameLoopState.GameModeSelection:
                return "遊び方を選ぼう";
            case GameLoopState.Ready:
                return "準備はいい？";
            case GameLoopState.Countdown:
                return "Countdown";
            case GameLoopState.Playing:
                return "Playing";
            case GameLoopState.Result:
                return "ラウンド完了！";
            default:
                return state.ToString();
        }
    }

    private string GetStateMessage(GameLoopState state)
    {
        switch (state)
        {
            case GameLoopState.MocopiConnectionWait:
                return GetConnectionStateMessage();
            case GameLoopState.AvatarSelection:
                return GetActivePlayerCount() == 1
                    ? $"1 PLAYER  /  {players[0].AvatarDisplayName}\nポーズを合わせて、10 枚の壁をくぐろう"
                    : $"1P / {players[0].AvatarDisplayName}   2P / {players[1].AvatarDisplayName}\n次の画面で「協力」か「対戦」を選べます";
            case GameLoopState.GameModeSelection:
                return "協力：2 人の合計点でチャレンジ\n対戦：クリアした壁の数で勝負";
            case GameLoopState.Ready:
                return "Hold the ready pose.";
            case GameLoopState.Countdown:
                return "Starting.";
            case GameLoopState.Playing:
                return gameMode == GameMode.Single ? "1P run." : "Split-screen run.";
            case GameLoopState.Result:
                return "Run complete.";
            default:
                return string.Empty;
        }
    }

    private string GetConnectionStateMessage()
    {
        if (debugModeOn && debugSkipMocopiConnection)
        {
            return "Debug mode: mocopi connection skipped.";
        }

        List<string> lines = new List<string>();
        lines.Add("Connect mocopi player(s).");

        int maxPlayersToShow = Mathf.Min(2, players.Count);
        for (int i = 0; i < maxPlayersToShow; i++)
        {
            lines.Add(players[i].GetConnectionStatusText(mocopiConnectionTimeoutSeconds));
        }

        int connectedPlayers = GetConnectedPlayerCount();
        if (connectedPlayers >= 2)
        {
            lines.Add("2P detected. Moving to avatar selection.");
        }
        else if (connectedPlayers == 1 && IsPlayerMocopiConnected(0))
        {
            float remaining = Mathf.Max(0f, singlePlayerConnectionPoseHoldSeconds - (readyHoldTimes.Count > 0 ? readyHoldTimes[0] : 0f));
            lines.Add($"For 1P, make an overhead circle. {Mathf.CeilToInt(remaining)}");
        }
        else if (connectedPlayers == 1)
        {
            lines.Add("Waiting for 1P slot, or connect both players for 2P.");
        }

        return string.Join("\n", lines);
    }

    private static void SetCameraRect(Camera targetCamera, Rect rect)
    {
        if (targetCamera != null)
        {
            targetCamera.rect = rect;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
