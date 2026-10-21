using System;
using UnityEngine;

/// <summary>Lightweight, non-interactive seasonal decoration for one isolated player arena.</summary>
public sealed class HalloweenAtmosphere : MonoBehaviour
{
    [Serializable]
    public struct Bat
    {
        public Transform body, leftWing, rightWing;
        public Vector3 restPosition;
        public float phase;
    }

    public MocopiLocalGameLoopController controller;
    public int playerIndex;
    public Bat[] bats = Array.Empty<Bat>();
    public Renderer[] lanternFaces = Array.Empty<Renderer>();
    public Light[] lanternLights = Array.Empty<Light>();
    public ParticleSystem[] embers = Array.Empty<ParticleSystem>();
    private MaterialPropertyBlock glow;
    private int completed, failed;
    private float celebration;
    private static readonly int Emission = Shader.PropertyToID("_Emission");

    private void Awake() => glow = new MaterialPropertyBlock();
    private void OnEnable()
    {
        if (controller != null) controller.OnStateChanged.AddListener(StateChanged);
    }
    private void OnDisable()
    {
        if (controller != null) controller.OnStateChanged.RemoveListener(StateChanged);
    }
    private void StateChanged(MocopiLocalGameLoopController.GameLoopState state)
    {
        completed = failed = 0;
        if (state == MocopiLocalGameLoopController.GameLoopState.Countdown || state == MocopiLocalGameLoopController.GameLoopState.Result)
            Celebrate();
    }
    public void Celebrate()
    {
        celebration = 1;
        foreach (var particles in embers) if (particles != null) particles.Emit(10);
    }
    private void Update()
    {
        if (controller != null && controller.CurrentState == MocopiLocalGameLoopController.GameLoopState.Playing && playerIndex < controller.ActivePlayerCount)
        {
            var wall = controller.Players[playerIndex].WallManager;
            int now = wall.GetCompletedWallCount(), misses = wall.GetFailedWallCount();
            if (now > completed && misses == failed) Celebrate();
            completed = now; failed = misses;
        }
        celebration = Mathf.MoveTowards(celebration, 0, Time.deltaTime * 0.65f);
        Animate(Time.time, celebration);
    }
    public void Animate(float time, float cheer = 0)
    {
        if (glow == null) glow = new MaterialPropertyBlock();
        for (int i = 0; i < bats.Length; i++)
        {
            var bat = bats[i];
            if (bat.body == null) continue;
            float t = time * (0.65f + i % 3 * 0.12f) + bat.phase;
            bat.body.localPosition = bat.restPosition + new Vector3(Mathf.Sin(t) * 0.6f, Mathf.Sin(t * 1.7f) * 0.2f, Mathf.Cos(t * 0.7f) * 0.35f);
            bat.body.localRotation = Quaternion.Euler(0, Mathf.Sin(t) * 16, Mathf.Sin(t * 0.8f) * 8);
            float flap = Mathf.Sin(time * (7 + i % 4) + bat.phase) * (24 + cheer * 12);
            if (bat.leftWing != null) bat.leftWing.localRotation = Quaternion.Euler(0, 0, -flap);
            if (bat.rightWing != null) bat.rightWing.localRotation = Quaternion.Euler(0, 0, flap);
        }
        for (int i = 0; i < lanternFaces.Length; i++)
        {
            float strength = 1.35f + 0.12f * Mathf.Sin(time * 2.2f + i * 1.8f) + cheer * 0.7f;
            glow.SetColor(Emission, new Color(1, 0.43f, 0.055f) * strength);
            if (lanternFaces[i] != null) lanternFaces[i].SetPropertyBlock(glow);
        }
        for (int i = 0; i < lanternLights.Length; i++)
            if (lanternLights[i] != null) lanternLights[i].intensity = 1.3f + 0.13f * Mathf.Sin(time * 2 + i) + cheer * 0.5f;
    }
}
