using System;
using UnityEngine;

/// <summary>One shared 2D soundtrack and spectrum for both player arenas.</summary>
[DefaultExecutionOrder(-900), RequireComponent(typeof(AudioSource))]
public sealed class GameMusicDirector : MonoBehaviour
{
    public const int BandCount = 16;
    public const int HistoryRows = 36;
    public MocopiLocalGameLoopController controller;
    public AudioClip music;
    [Range(0, 1)] public float volume = 0.65f;
    [Min(0.1f)] public float fadeOutSeconds = 3f;
    [Min(1)] public float spectrumGain = 180f;
    private AudioSource source;
    private readonly float[] spectrum = new float[1024];
    private readonly float[] levels = new float[BandCount];
    private readonly float[] targets = new float[BandCount];
    private readonly Color32[] history = new Color32[BandCount * HistoryRows];
    private Texture2D historyTexture;
    private bool playing, fading;
    private float fadeElapsed, fadeStart, envelope, historyElapsed;
    public AudioSource Source => source;
    public Texture2D SpectrumHistory => historyTexture;
    public float Visibility => envelope;
    public float Energy { get; private set; }
    public float Peak { get; private set; }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.dopplerLevel = 0;
        source.loop = true;
        source.ignoreListenerPause = true;
        source.priority = 32;
        source.clip = music;
        source.volume = 0;
        historyTexture = new Texture2D(BandCount, HistoryRows, TextureFormat.RGBA32, false, true)
        { name = "Music spectrum history", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        ClearSpectrum();
    }

    private void OnEnable()
    {
        if (controller == null) return;
        controller.OnStateChanged.AddListener(HandleState);
        HandleState(controller.CurrentState);
    }
    private void OnDisable()
    {
        if (controller != null) controller.OnStateChanged.RemoveListener(HandleState);
        playing = fading = false; envelope = 0;
        if (source != null) { source.Stop(); source.volume = 0; }
    }

    private void HandleState(MocopiLocalGameLoopController.GameLoopState state)
    {
        bool nextPlaying = state == MocopiLocalGameLoopController.GameLoopState.Playing;
        if (nextPlaying && !playing)
        {
            fading = false; fadeElapsed = 0; envelope = 1;
            ClearSpectrum();
            source.Stop(); source.clip = music; source.volume = volume;
            if (music != null) source.Play();
        }
        else if (!nextPlaying && playing)
        {
            fading = true; fadeElapsed = 0; fadeStart = envelope;
        }
        playing = nextPlaying;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (fading)
        {
            fadeElapsed += dt;
            envelope = fadeStart * (1 - Mathf.Clamp01(fadeElapsed / Mathf.Max(0.1f, fadeOutSeconds)));
            if (envelope <= 0) { fading = false; source.Stop(); ClearSpectrum(); }
        }
        source.volume = volume * envelope;
        if (!source.isPlaying) { Energy = 0; return; }
        source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        AnalyseSpectrum(spectrum, AudioSettings.outputSampleRate, spectrumGain, targets);
        float energy = 0;
        for (int i = 0; i < BandCount; i++)
        {
            float speed = targets[i] > levels[i] ? 25 : 7;
            levels[i] = Mathf.Lerp(levels[i], targets[i], 1 - Mathf.Exp(-speed * dt));
            energy += levels[i]; Peak = Mathf.Max(Peak, levels[i]);
        }
        Energy = energy / BandCount;
        historyElapsed += dt;
        if (historyElapsed < 1f / 24) return;
        int steps = Mathf.Min(HistoryRows, Mathf.FloorToInt(historyElapsed * 24));
        historyElapsed %= 1f / 24;
        for (int step = 0; step < steps; step++)
        {
            Array.Copy(history, 0, history, BandCount, history.Length - BandCount);
            for (int band = 0; band < BandCount; band++)
                history[band] = new Color32((byte)Mathf.RoundToInt(levels[band] * 255), 0, 0, 255);
        }
        historyTexture.SetPixels32(history); historyTexture.Apply(false);
    }

    public static void AnalyseSpectrum(float[] input, int sampleRate, float gain, float[] output)
    {
        float nyquist = Mathf.Max(1, sampleRate * 0.5f);
        for (int band = 0; band < BandCount; band++)
        {
            float low = 40 * Mathf.Pow(300, band / (float)BandCount);
            float high = 40 * Mathf.Pow(300, (band + 1f) / BandCount);
            int first = Mathf.Clamp(Mathf.FloorToInt(low / nyquist * input.Length), 1, input.Length - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt(high / nyquist * input.Length), first + 1, input.Length);
            float power = 0;
            for (int bin = first; bin < last; bin++) power += input[bin] * input[bin];
            output[band] = Mathf.Clamp01(Mathf.Log10(1 + Mathf.Sqrt(power / (last - first)) * gain) * 0.85f);
        }
    }

    private void ClearSpectrum()
    {
        Array.Clear(levels, 0, levels.Length); Array.Clear(history, 0, history.Length);
        Energy = Peak = historyElapsed = 0;
        if (historyTexture != null) { historyTexture.SetPixels32(history); historyTexture.Apply(false); }
    }
    private void OnDestroy() { if (historyTexture != null) Destroy(historyTexture); }
}
