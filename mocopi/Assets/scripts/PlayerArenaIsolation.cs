using UnityEngine;

[DefaultExecutionOrder(-1500)]
public class PlayerArenaIsolation : MonoBehaviour
{
    public MocopiLocalGameLoopController controller;
    public GameObject[] environments;
    private bool previousCollisionSetting;
    private int visiblePlayerCount = -1;
    private void Awake()
    {
        previousCollisionSetting = Physics.GetIgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two);
        Physics.IgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two, true);
    }
    private void OnEnable() => controller.OnStateChanged.AddListener(Refresh);
    private void OnDisable() => controller.OnStateChanged.RemoveListener(Refresh);
    private void OnDestroy() => Physics.IgnoreLayerCollision(PlayerArenaLayers.One, PlayerArenaLayers.Two, previousCollisionSetting);
    private void LateUpdate()
    {
        // The player count can change while remaining on the connection screen.
        if (visiblePlayerCount != controller.ActivePlayerCount) Refresh(controller.CurrentState);
    }
    private void Refresh(MocopiLocalGameLoopController.GameLoopState state)
    {
        visiblePlayerCount = controller.ActivePlayerCount;
        for (int i = 0; i < environments.Length; i++) environments[i].SetActive(i < visiblePlayerCount);
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
