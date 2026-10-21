using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Debris rejects everything unless an explicitly allowed surface overrides it.</summary>
public static class WallDebrisCollision
{
    public const int PlayerOneDebris = 10;
    public const int PlayerTwoDebris = 11;
    private const int DebrisMask = (1 << PlayerOneDebris) | (1 << PlayerTwoDebris);
    private static PhysicsMaterial material;
    public static int LayerForArena(int layer) => layer == PlayerArenaLayers.Two ? PlayerTwoDebris : PlayerOneDebris;

    public static PhysicsMaterial Material
    {
        get
        {
            if (material == null)
                material = new PhysicsMaterial("Wall debris - soft bounce")
                {
                    dynamicFriction = 0.48f, staticFriction = 0.6f, bounciness = 0.12f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            return material;
        }
    }

    public static void AllowContact(Collider collider)
    {
        if (collider == null) return;
        int mask = 1 << LayerForArena(collider.gameObject.layer);
        collider.includeLayers = (collider.includeLayers.value & ~DebrisMask) | mask;
        collider.excludeLayers = (collider.excludeLayers.value & ~DebrisMask) | (DebrisMask & ~mask);
        collider.layerOverridePriority = 20;
    }

    public static void ConfigureScene(Scene scene, Collider[] explicitFloors)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            bool player = collider.GetComponentInParent<MocopiPlayerColliderRig>() != null ||
                collider.GetComponent<DebugMousePlayerCollider>() != null || collider.CompareTag("Player");
            // Both authored GameMode floors are named Plane. Explicit references support new stages.
            bool floor = !collider.isTrigger && (explicitFloors != null && explicitFloors.Length > 0
                ? System.Array.IndexOf(explicitFloors, collider) >= 0
                : collider.name == "Plane" || collider.name == "Floor");
            if (player || floor) AllowContact(collider);
        }
    }
}
