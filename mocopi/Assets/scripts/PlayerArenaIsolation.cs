using UnityEngine;

[DefaultExecutionOrder(-1500)]
public class PlayerArenaIsolation : MonoBehaviour
{
    public MocopiLocalGameLoopController controller;
    public GameObject[] environments;
    private bool previousCollisionSetting;
    private int visiblePlayerCount = -1;
    private bool sharedView;
    private Vector3[] arenaPositions, environmentPositions;
    private ArenaMusicVisuals[] musicVisuals;
    public Vector3 SharedCenter { get; private set; }
    private void Awake()
    {
        previousCollisionSetting = Physics.GetIgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two);
        Physics.IgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two, true);
        arenaPositions = new Vector3[environments.Length];
        environmentPositions = new Vector3[environments.Length];
        musicVisuals = new ArenaMusicVisuals[environments.Length];
        for (int i = 0; i < environments.Length; i++)
        {
            arenaPositions[i] = environments[i].transform.parent.position;
            environmentPositions[i] = environments[i].transform.localPosition;
            musicVisuals[i] = environments[i].GetComponentInChildren<ArenaMusicVisuals>(true);
        }
        SharedCenter = controller.Players[0].CurrentMocopiAvatar.transform.position;
    }
    private void OnEnable() => controller.OnStateChanged.AddListener(Refresh);
    private void OnDisable() => controller.OnStateChanged.RemoveListener(Refresh);
    private void OnDestroy() => Physics.IgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two, previousCollisionSetting);
    private void LateUpdate()
    {
        // The player count can change while remaining on the connection screen.
        if (visiblePlayerCount != controller.ViewportCount || sharedView != controller.UsesSharedCoopView) ApplyLayout();
    }
    private void Refresh(MocopiLocalGameLoopController.GameLoopState state)
        => ApplyLayout();

    public void ApplyLayout()
    {
        if (arenaPositions == null) return;
        visiblePlayerCount = controller.ViewportCount;
        sharedView = controller.UsesSharedCoopView;
        for (int i = 0; i < environments.Length; i++)
        {
            var environment = environments[i].transform;
            // Each player keeps a six metre lane and its own collision layer.
            Vector3 offset = sharedView
                ? arenaPositions[0] - arenaPositions[i] + Vector3.right * (i == 0 ? -3f : 3f)
                : Vector3.zero;
            environment.parent.position = arenaPositions[i] + offset;
            environment.localPosition = environmentPositions[i] - (sharedView ? offset : Vector3.zero);
            environment.gameObject.SetActive(i < visiblePlayerCount && (!sharedView || i == 0));
            foreach (var light in environment.GetComponentsInChildren<Light>(true))
                light.cullingMask = sharedView ? (1 << PlayerArenaLayers.One) | (1 << PlayerArenaLayers.Two) : 1 << (PlayerArenaLayers.One + i);
            if (musicVisuals[i] != null) musicVisuals[i].SetSharedFloor(sharedView && i == 0, SharedCenter, 10f);
        }
    }
}

public static class PlayerArenaLayers
{
    public const int One = 8;
    public const int Two = 9;
    public static bool IsArena(int layer) => layer == One || layer == Two;
    public static bool CanInteract(GameObject target, GameObject other) => !IsArena(target.layer) || target.layer == other.layer;
    public static void Inherit(GameObject target, GameObject owner)
    {
        if (owner != null && IsArena(owner.layer)) Assign(target, owner.layer);
    }
    public static void Assign(GameObject target, int layer)
    {
        foreach (var child in target.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }
}
