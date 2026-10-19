using System;
using System.Collections;
using System.Collections.Generic;
using Mocopi.Receiver;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Single-scene flow controller for the mocopi wall game screens.
/// </summary>
public class MocopiGameStateController : MonoBehaviour
{
    public enum GameState
    {
        MocopiConnectionWait,
        AvatarSelection,
        Ready,
        GameStart,
        Result
    }

    [Serializable]
    public class StateChangedEvent : UnityEvent<GameState> { }

    [Header("State Panels")]
    [SerializeField] private GameObject mocopiConnectionWaitPanel;
    [SerializeField] private GameObject avatarSelectionPanel;
    [SerializeField] private GameObject readyPanel;
    [SerializeField] private GameObject gameStartPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("Mocopi Connection Panels")]
    [SerializeField] private GameObject mocopiConnectPromptPanel;
    [SerializeField] private GameObject mocopiConnectCompletePanel;
    [SerializeField] private float connectionCompleteDelay = 1.5f;
    [SerializeField] private bool autoAdvanceAfterConnectionComplete = true;

    [Header("Optional Labels")]
    [SerializeField] private Text stateTitleText;
    [SerializeField] private Text stateMessageText;
    [SerializeField] private Text selectedAvatarText;
    [SerializeField] private Text resultText;

    [Header("Game References")]
    [SerializeField] private WallManager wallManager;
    [SerializeField] private List<GameObject> avatarRoots = new List<GameObject>();
    [SerializeField] private int defaultAvatarIndex;
    [SerializeField] private bool pauseTimeOutsideGame;
    [SerializeField] private GameState initialState = GameState.MocopiConnectionWait;

    [Header("Avatar Selection")]
    [SerializeField] private Transform avatarSpawnPoint;
    [SerializeField] private List<GameObject> avatarPrefabs = new List<GameObject>();
    [SerializeField] private List<AvatarSelectionTapestryPanel> avatarSelectionPanels = new List<AvatarSelectionTapestryPanel>();
    [SerializeField] private float avatarSelectionWaitSeconds = 3f;
    [SerializeField] private Text avatarSelectionCountdownText;

    [Header("Ready Pose")]
    [SerializeField] private Animator readyPoseAnimator;
    [SerializeField] private GameObject readyInstructionPanel;
    [SerializeField] private GameObject readyCountdownPanel;
    [SerializeField] private GameObject readyStartPanel;
    [SerializeField] private Text readyCountdownText;
    [SerializeField] private ReadyHandOrbVisualizer readyHandOrbVisualizer;
    [SerializeField] private float readyHoldSeconds = 5f;
    [SerializeField] private float readyStartDisplaySeconds = 0.75f;
    [SerializeField] private float handTouchDistance = 0.18f;
    [SerializeField] private float handsAboveHeadHeight = 0.08f;
    [SerializeField] private float maxHandsHeadHorizontalDistance = 0.45f;

    [Header("Events")]
    [SerializeField] private StateChangedEvent onStateChanged = new StateChangedEvent();

    private GameState currentState;
    private int selectedAvatarIndex;
    private bool isMocopiConnectionComplete;
    private Coroutine connectionCompleteRoutine;
    private Coroutine avatarSelectionRoutine;
    private Coroutine readyStartRoutine;
    private GameObject spawnedAvatarRoot;
    private float readyPoseHoldTime;
    private bool readyPoseComplete;

    public GameState CurrentState => currentState;
    public int SelectedAvatarIndex => selectedAvatarIndex;
    public StateChangedEvent OnStateChanged => onStateChanged;

    private void Awake()
    {
        if (wallManager == null)
        {
            wallManager = FindAnyObjectByType<WallManager>();
        }

        selectedAvatarIndex = Mathf.Clamp(defaultAvatarIndex, 0, Mathf.Max(0, GetAvatarChoiceCount() - 1));
        HideAllSceneAvatarRoots();
        UpdateSelectedAvatarLabel();
        ConfigureAvatarSelectionPanels();
    }

    private void OnEnable()
    {
        if (wallManager != null)
        {
            wallManager.OnResult += HandleResult;
        }
    }

    private void Start()
    {
        ChangeState(initialState);
    }

    private void OnDisable()
    {
        if (wallManager != null)
        {
            wallManager.OnResult -= HandleResult;
        }
    }

    private void Update()
    {
        if (currentState == GameState.Ready)
        {
            UpdateReadyPose();
        }
    }

    public void ShowMocopiConnectionWait()
    {
        isMocopiConnectionComplete = false;
        ChangeState(GameState.MocopiConnectionWait);
    }

    public void ConfirmMocopiConnected()
    {
        ShowMocopiConnectionComplete();
    }

    public void ShowMocopiConnectionComplete()
    {
        if (currentState != GameState.MocopiConnectionWait)
        {
            ChangeState(GameState.MocopiConnectionWait);
        }

        isMocopiConnectionComplete = true;
        UpdateMocopiConnectionPanels();
        UpdateStateLabels();

        if (connectionCompleteRoutine != null)
        {
            StopCoroutine(connectionCompleteRoutine);
        }

        if (autoAdvanceAfterConnectionComplete)
        {
            connectionCompleteRoutine = StartCoroutine(AdvanceFromConnectionComplete());
        }
    }

    public void ContinueToAvatarSelection()
    {
        ChangeState(GameState.AvatarSelection);
    }

    public void ShowAvatarSelection()
    {
        ChangeState(GameState.AvatarSelection);
    }

    public void SelectAvatar(int avatarIndex)
    {
        int avatarCount = GetAvatarChoiceCount();
        if (avatarCount == 0)
        {
            selectedAvatarIndex = 0;
            UpdateSelectedAvatarLabel();
            UpdateAvatarSelectionPanels();
            return;
        }

        selectedAvatarIndex = Mathf.Clamp(avatarIndex, 0, avatarCount - 1);
        ApplySelectedAvatar();
        UpdateSelectedAvatarLabel();
        UpdateAvatarSelectionPanels();
    }

    public void ChooseAvatar(int avatarIndex)
    {
        if (currentState != GameState.AvatarSelection)
        {
            ChangeState(GameState.AvatarSelection);
        }

        SelectAvatar(avatarIndex);
        BeginAvatarSelectionWait();
    }

    public void SelectNextAvatar()
    {
        int avatarCount = GetAvatarChoiceCount();
        if (avatarCount == 0)
        {
            return;
        }

        SelectAvatar((selectedAvatarIndex + 1) % avatarCount);
    }

    public void SelectPreviousAvatar()
    {
        int avatarCount = GetAvatarChoiceCount();
        if (avatarCount == 0)
        {
            return;
        }

        SelectAvatar((selectedAvatarIndex - 1 + avatarCount) % avatarCount);
    }

    public void ConfirmAvatarSelection()
    {
        BeginAvatarSelectionWait();
    }

    public void ShowReady()
    {
        ChangeState(GameState.Ready);
    }

    public void StartGame()
    {
        ChangeState(GameState.GameStart);
    }

    public void ShowResult()
    {
        ChangeState(GameState.Result);
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        if (wallManager != null)
        {
            wallManager.ResetGame();
        }

        ChangeState(GameState.Ready);
    }

    private void ChangeState(GameState nextState)
    {
        if (connectionCompleteRoutine != null)
        {
            StopCoroutine(connectionCompleteRoutine);
            connectionCompleteRoutine = null;
        }

        if (avatarSelectionRoutine != null)
        {
            StopCoroutine(avatarSelectionRoutine);
            avatarSelectionRoutine = null;
        }

        if (readyStartRoutine != null)
        {
            StopCoroutine(readyStartRoutine);
            readyStartRoutine = null;
        }

        currentState = nextState;

        SetActive(mocopiConnectionWaitPanel, nextState == GameState.MocopiConnectionWait);
        SetActive(avatarSelectionPanel, nextState == GameState.AvatarSelection);
        SetActive(readyPanel, nextState == GameState.Ready);
        SetActive(gameStartPanel, nextState == GameState.GameStart);
        SetActive(resultPanel, nextState == GameState.Result);

        if (nextState == GameState.MocopiConnectionWait && !isMocopiConnectionComplete)
        {
            UpdateMocopiConnectionPanels();
        }
        else if (nextState != GameState.MocopiConnectionWait)
        {
            SetActive(mocopiConnectPromptPanel, false);
            SetActive(mocopiConnectCompletePanel, false);
        }

        if (nextState == GameState.MocopiConnectionWait)
        {
            HideAllSceneAvatarRoots();
            DestroySpawnedAvatar();
            ClearAvatarSelectionCountdown();
        }
        else if (nextState == GameState.AvatarSelection)
        {
            PrepareAvatarSelection();
        }
        else if (nextState == GameState.Ready)
        {
            PrepareReadyState();
        }
        else
        {
            ClearReadyPanels();
        }

        if (wallManager != null)
        {
            if (nextState == GameState.GameStart)
            {
                wallManager.BeginGame();
            }
            else
            {
                wallManager.StopGame();
            }
        }

        Time.timeScale = pauseTimeOutsideGame && nextState != GameState.GameStart ? 0f : 1f;
        UpdateStateLabels();
        onStateChanged.Invoke(currentState);
    }

    private IEnumerator AdvanceFromConnectionComplete()
    {
        float waitSeconds = Mathf.Max(0f, connectionCompleteDelay);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }

        connectionCompleteRoutine = null;
        ContinueToAvatarSelection();
    }

    private void BeginAvatarSelectionWait()
    {
        if (avatarSelectionRoutine != null)
        {
            StopCoroutine(avatarSelectionRoutine);
        }

        avatarSelectionRoutine = StartCoroutine(AdvanceFromAvatarSelection());
    }

    private IEnumerator AdvanceFromAvatarSelection()
    {
        float remaining = Mathf.Max(0f, avatarSelectionWaitSeconds);

        while (remaining > 0f)
        {
            string countdown = Mathf.CeilToInt(remaining).ToString();
            SetAvatarSelectionCountdown(countdown);
            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
        }

        SetAvatarSelectionCountdown("Ready");
        avatarSelectionRoutine = null;
        ChangeState(GameState.Ready);
    }

    private void UpdateMocopiConnectionPanels()
    {
        SetActive(mocopiConnectPromptPanel, !isMocopiConnectionComplete);
        SetActive(mocopiConnectCompletePanel, isMocopiConnectionComplete);
    }

    private void HandleResult(WallManager.ResultData result)
    {
        if (resultText != null)
        {
            resultText.text =
                $"Result\n" +
                $"Score: {result.Score}/{result.TotalWalls}\n" +
                $"Hits: {result.TotalContacts}\n" +
                $"Safe: {result.SafeWalls}  -1: {result.FailedWalls}";
        }

        ChangeState(GameState.Result);
    }

    private void PrepareReadyState()
    {
        readyPoseAnimator = ResolveReadyPoseAnimator();
        readyPoseHoldTime = 0f;
        readyPoseComplete = false;
        SetActive(readyInstructionPanel, true);
        SetActive(readyCountdownPanel, false);
        SetActive(readyStartPanel, false);
        if (readyHandOrbVisualizer != null)
        {
            readyHandOrbVisualizer.SetTargetAnimator(readyPoseAnimator);
            readyHandOrbVisualizer.SetMergeDistance(handTouchDistance);
            readyHandOrbVisualizer.SetVisible(true);
        }
        UpdateReadyCountdownText(readyHoldSeconds);
    }

    private void UpdateReadyPose()
    {
        if (readyPoseComplete)
        {
            return;
        }

        if (readyPoseAnimator == null)
        {
            readyPoseAnimator = ResolveReadyPoseAnimator();
        }

        if (IsReadyPoseHeld())
        {
            readyPoseHoldTime = Mathf.Min(readyHoldSeconds, readyPoseHoldTime + Time.unscaledDeltaTime);
            SetActive(readyInstructionPanel, false);
            SetActive(readyCountdownPanel, true);
            SetActive(readyStartPanel, false);
            UpdateReadyCountdownText(readyHoldSeconds - readyPoseHoldTime);

            if (readyPoseHoldTime >= readyHoldSeconds)
            {
                CompleteReadyPose();
            }
        }
        else
        {
            readyPoseHoldTime = 0f;
            SetActive(readyInstructionPanel, true);
            SetActive(readyCountdownPanel, false);
            SetActive(readyStartPanel, false);
            UpdateReadyCountdownText(readyHoldSeconds);
        }
    }

    private bool IsReadyPoseHeld()
    {
        if (readyPoseAnimator == null || !readyPoseAnimator.isHuman)
        {
            return false;
        }

        Transform head = readyPoseAnimator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftHand = readyPoseAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = readyPoseAnimator.GetBoneTransform(HumanBodyBones.RightHand);
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

    private void CompleteReadyPose()
    {
        readyPoseComplete = true;
        readyPoseHoldTime = readyHoldSeconds;
        SetActive(readyInstructionPanel, false);
        SetActive(readyCountdownPanel, false);
        SetActive(readyStartPanel, true);
        UpdateReadyCountdownText(0f);

        if (readyStartRoutine != null)
        {
            StopCoroutine(readyStartRoutine);
        }

        readyStartRoutine = StartCoroutine(AdvanceFromReadyStart());
    }

    private IEnumerator AdvanceFromReadyStart()
    {
        float waitSeconds = Mathf.Max(0f, readyStartDisplaySeconds);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }

        readyStartRoutine = null;
        ChangeState(GameState.GameStart);
    }

    private void UpdateReadyCountdownText(float remainingSeconds)
    {
        if (readyCountdownText != null)
        {
            readyCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)).ToString();
        }
    }

    private void ClearReadyPanels()
    {
        SetActive(readyInstructionPanel, false);
        SetActive(readyCountdownPanel, false);
        SetActive(readyStartPanel, false);
        if (readyHandOrbVisualizer != null)
        {
            readyHandOrbVisualizer.SetVisible(false);
        }
    }

    private Animator ResolveReadyPoseAnimator()
    {
        if (readyPoseAnimator != null && readyPoseAnimator.isHuman && readyPoseAnimator.gameObject.activeInHierarchy)
        {
            return readyPoseAnimator;
        }

        if (spawnedAvatarRoot != null && spawnedAvatarRoot.activeInHierarchy)
        {
            Animator spawnedAnimator = spawnedAvatarRoot.GetComponentInChildren<Animator>();
            if (spawnedAnimator != null && spawnedAnimator.isHuman)
            {
                return spawnedAnimator;
            }
        }

        if (avatarRoots.Count > 0)
        {
            int clampedIndex = Mathf.Clamp(selectedAvatarIndex, 0, avatarRoots.Count - 1);
            GameObject selectedRoot = avatarRoots[clampedIndex];
            if (selectedRoot != null && selectedRoot.activeInHierarchy)
            {
                Animator selectedAnimator = selectedRoot.GetComponentInChildren<Animator>();
                if (selectedAnimator != null && selectedAnimator.isHuman)
                {
                    return selectedAnimator;
                }
            }
        }

        MocopiPlayerColliderRig rig = FindAnyObjectByType<MocopiPlayerColliderRig>();
        if (rig != null && rig.TryGetComponent(out Animator rigAnimator) && rigAnimator.isHuman)
        {
            return rigAnimator;
        }

        return FindAnyObjectByType<Animator>();
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

        UpdateSelectedAvatarLabel();
    }

    private void PrepareAvatarSelection()
    {
        selectedAvatarIndex = Mathf.Clamp(selectedAvatarIndex, 0, Mathf.Max(0, GetAvatarChoiceCount() - 1));
        ConfigureAvatarSelectionPanels();
        ApplySelectedAvatar();
        UpdateAvatarSelectionPanels();
        ClearAvatarSelectionCountdown();
    }

    private void ConfigureAvatarSelectionPanels()
    {
        for (int i = 0; i < avatarSelectionPanels.Count; i++)
        {
            if (avatarSelectionPanels[i] != null)
            {
                avatarSelectionPanels[i].Configure(this, i);
            }
        }
    }

    private void UpdateAvatarSelectionPanels()
    {
        for (int i = 0; i < avatarSelectionPanels.Count; i++)
        {
            if (avatarSelectionPanels[i] == null)
            {
                continue;
            }

            avatarSelectionPanels[i].SetSelected(i == selectedAvatarIndex);
            avatarSelectionPanels[i].SetCountdown(string.Empty);
        }
    }

    private void SetAvatarSelectionCountdown(string value)
    {
        if (avatarSelectionCountdownText != null)
        {
            avatarSelectionCountdownText.text = value;
        }

        for (int i = 0; i < avatarSelectionPanels.Count; i++)
        {
            if (avatarSelectionPanels[i] != null)
            {
                avatarSelectionPanels[i].SetCountdown(i == selectedAvatarIndex ? value : string.Empty);
            }
        }
    }

    private void ClearAvatarSelectionCountdown()
    {
        SetAvatarSelectionCountdown(string.Empty);
    }

    private int GetAvatarChoiceCount()
    {
        if (avatarPrefabs.Count > 0)
        {
            return avatarPrefabs.Count;
        }

        if (avatarRoots.Count > 0)
        {
            return avatarRoots.Count;
        }

        return avatarSelectionPanels.Count;
    }

    private void SpawnSelectedAvatarPrefab()
    {
        int clampedIndex = Mathf.Clamp(selectedAvatarIndex, 0, avatarPrefabs.Count - 1);
        GameObject selectedPrefab = avatarPrefabs[clampedIndex];

        if (selectedPrefab == null)
        {
            DestroySpawnedAvatar();
            UpdateSelectedAvatarLabel();
            return;
        }

        DestroySpawnedAvatar();

        Vector3 spawnPosition = avatarSpawnPoint != null ? avatarSpawnPoint.position : transform.position;
        Quaternion spawnRotation = avatarSpawnPoint != null ? avatarSpawnPoint.rotation : transform.rotation;
        spawnedAvatarRoot = Instantiate(selectedPrefab, spawnPosition, spawnRotation);
        spawnedAvatarRoot.name = selectedPrefab.name;
        UpdateSelectedAvatarLabel();
    }

    private void DestroySpawnedAvatar()
    {
        if (spawnedAvatarRoot == null)
        {
            return;
        }

        Destroy(spawnedAvatarRoot);
        spawnedAvatarRoot = null;
    }

    private void HideAllSceneAvatarRoots()
    {
        for (int i = 0; i < avatarRoots.Count; i++)
        {
            if (avatarRoots[i] != null)
            {
                avatarRoots[i].SetActive(false);
            }
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

        UpdateSelectedAvatarLabel();
    }

    private void UpdateSelectedAvatarLabel()
    {
        if (selectedAvatarText == null)
        {
            return;
        }

        if (avatarRoots.Count == 0)
        {
            if (spawnedAvatarRoot != null)
            {
                selectedAvatarText.text = $"Avatar: {spawnedAvatarRoot.name}";
                return;
            }

            if (avatarPrefabs.Count > 0)
            {
                GameObject selectedPrefab = avatarPrefabs[Mathf.Clamp(selectedAvatarIndex, 0, avatarPrefabs.Count - 1)];
                selectedAvatarText.text = selectedPrefab != null ? $"Avatar: {selectedPrefab.name}" : "Avatar: none";
                return;
            }

            MocopiAvatar activeMocopiAvatar = MocopiAvatar.Avatars.Count > 0 ? MocopiAvatar.Avatars[0] : null;
            selectedAvatarText.text = activeMocopiAvatar != null ? activeMocopiAvatar.name : "Avatar: none";
            return;
        }

        GameObject selected = avatarRoots[Mathf.Clamp(selectedAvatarIndex, 0, avatarRoots.Count - 1)];
        selectedAvatarText.text = selected != null ? $"Avatar: {selected.name}" : "Avatar: none";
    }

    private string GetStateTitle(GameState state)
    {
        switch (state)
        {
            case GameState.MocopiConnectionWait:
                return "Mocopi Connection";
            case GameState.AvatarSelection:
                return "Avatar Select";
            case GameState.Ready:
                return "Ready";
            case GameState.GameStart:
                return "Game Start";
            case GameState.Result:
                return "Result";
            default:
                return state.ToString();
        }
    }

    private string GetStateMessage(GameState state)
    {
        switch (state)
        {
            case GameState.MocopiConnectionWait:
                return isMocopiConnectionComplete ? "Mocopi connection complete." : "Please connect mocopi.";
            case GameState.AvatarSelection:
                return "Choose the mocopi avatar.";
            case GameState.Ready:
                return "Stand in position and get ready.";
            case GameState.GameStart:
                return "Game running.";
            case GameState.Result:
                return "Run complete.";
            default:
                return string.Empty;
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
