using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Run against a project copy; checks the real music and captures both views.</summary>
public static class ArenaMusicValidation
{
    private const string Running = "ArenaMusicValidation.Running";
    private static MocopiLocalGameLoopController loop;
    private static GameMusicDirector music;
    private static int phase;
    private static double started, phaseAt;
    private static float previousTravel, measuredPeak;

    public static void Run()
    {
        EditorSceneManager.OpenScene(GameModeValidation.ScenePath);
        SessionState.SetBool(Running, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        started = phaseAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }
    private static void Next(int value) { phase = value; phaseAt = EditorApplication.timeSinceStartup; }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 100) throw new Exception("Audio floor test timed out at phase " + phase);
            if (loop == null) loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
            if (music == null) music = Object.FindFirstObjectByType<GameMusicDirector>();
            if (loop == null || music == null) return;
            double elapsed = EditorApplication.timeSinceStartup - phaseAt;
            switch (phase)
            {
                case 0:
                    if (elapsed < 1) return;
                    Check(music.music != null && music.music.name == "Tick Tock Tantrum", "Requested soundtrack is assigned");
                    Check(!music.Source.isPlaying, "No music during selection");
                    ValidateSpectrumMapping();
                    loop.SetCoopMode(); loop.ConfirmAvatarSelection(); loop.SetCoopMode(); loop.StartDebugRound();
                    Check(loop.CurrentState == MocopiLocalGameLoopController.GameLoopState.Countdown, "Countdown started");
                    Next(1); break;
                case 1:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Playing)
                    { Check(!music.Source.isPlaying, "Countdown remains silent"); return; }
                    Check(music.Source.isPlaying && music.Source.time < 0.5f, "Soundtrack starts with Playing, from its beginning");
                    Next(2); break;
                case 2:
                    if (elapsed < 6) return;
                    Check(music.Source.isPlaying && music.Source.time > 4, "Actual clip playback advances");
                    measuredPeak = music.Peak;
                    Check(measuredPeak > 0.001f, "Real soundtrack produces a nonzero spectrum");
                    Check(music.SpectrumHistory.GetPixels32().Any(p => p.r > 0), "Audio history reaches the floor texture");
                    ValidateArenas();
                    GameModeValidation.CaptureView(loop, "music-floor-coop");
                    previousTravel = Object.FindFirstObjectByType<ArenaMusicVisuals>().Travelled;
                    Next(3); break;
                case 3:
                    if (elapsed < 0.8) return;
                    Check(Object.FindFirstObjectByType<ArenaMusicVisuals>().Travelled > previousTravel + 10, "Light trails flow continuously");
                    GameModeValidation.CaptureView(loop, "music-floor-motion");
                    // Use the controller's actual end transition, including stop/score/UI hooks.
                    typeof(MocopiLocalGameLoopController).GetMethod("ChangeState", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(loop, new object[] { MocopiLocalGameLoopController.GameLoopState.Result });
                    Time.timeScale = 0;
                    Next(4); break;
                case 4:
                    if (elapsed < 1.5) return;
                    Check(music.Source.isPlaying, "Music continues during fade");
                    Check(music.Source.volume > music.volume * 0.35f && music.Source.volume < music.volume * 0.65f, "Volume is halfway down after 1.5 real seconds, even when paused");
                    Next(5); break;
                case 5:
                    if (elapsed < 1.7) return;
                    Check(!music.Source.isPlaying && music.Source.volume == 0 && music.Visibility == 0, "Music stops after three real seconds");
                    Debug.Log("ARENA_MUSIC_FADE_PASSED: exact track, silent countdown, start sync, real spectrum peak=" + measuredPeak + ", 3-second unscaled fade and stop.");
                    Time.timeScale = 1;
                    loop.Retry(); loop.StartDebugRound();
                    Next(6); break;
                case 6:
                    if (loop.CurrentState != MocopiLocalGameLoopController.GameLoopState.Playing) return;
                    Check(music.Source.isPlaying && music.Source.time < 0.5f, "Retry starts the song from the beginning");
                    loop.OnStateChanged.Invoke(MocopiLocalGameLoopController.GameLoopState.Result);
                    Next(7); break;
                case 7:
                    if (elapsed < 0.7) return;
                    loop.OnStateChanged.Invoke(MocopiLocalGameLoopController.GameLoopState.Playing);
                    Next(8); break;
                case 8:
                    if (elapsed < 3.3) return;
                    Check(music.Source.isPlaying && Mathf.Abs(music.Source.volume - music.volume) < 0.001f, "An interrupted old fade cannot stop a new round");
                    loop.ReturnToAvatarSelection(); loop.SetSingleMode();
                    Next(9); break;
                case 9:
                    if (elapsed < 3.3) return;
                    Check(!music.Source.isPlaying, "Returning to selection fades and stops the track");
                    Check(Object.FindObjectsByType<ArenaMusicVisuals>(FindObjectsSortMode.None).Length == 1, "Solo hides the second spectral stage");
                    Debug.Log("ARENA_MUSIC_PLAYMODE_PASSED: real audio analysis, two isolated translucent rings, open-bottom tunnel lights, 3-second fade at timeScale zero, restart, interrupted fade and menu return.");
                    Finish(0); break;
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void ValidateArenas()
    {
        var visuals = Object.FindObjectsByType<ArenaMusicVisuals>(FindObjectsSortMode.None);
        Check(visuals.Length == 2, "Both stages have visuals");
        Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.clip == music.music) == 1, "2P uses one shared soundtrack");
        foreach (var visual in visuals)
        {
            Check(visual.GetComponentsInChildren<Collider>().Length == 0, "Audio floor never changes gameplay collision");
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                Check(renderer.gameObject.layer == visual.gameObject.layer && PlayerArenaLayers.IsArena(renderer.gameObject.layer), "Effect meshes stay in their own camera layer");
            var tunnel = visual.transform.Find("Tunnel Light Trails").GetComponent<MeshFilter>().sharedMesh;
            Check(visual.transform.Find("Flat Light Trails") == null, "No moving floor lights remain");
            Check(tunnel.vertexCount == 20 && tunnel.vertices.All(v => v.y >= 0.79f && (Mathf.Abs(v.x) > 4.4f)), "Lights follow the side walls and ceiling, leaving the floor open");
            Check(tunnel.vertices.Any(v => v.y > 7.8f), "Tunnel ceiling has passing lights");
            var mesh = visual.transform.Find("Audio Floor").GetComponent<MeshFilter>().sharedMesh;
            float radius = visual.circleDiameter * 0.5f;
            Check(mesh.vertexCount == 64 * 20 && mesh.vertices.All(v => {
                float distance = new Vector2(v.x,v.z).magnitude;
                return distance > radius * 0.75f && distance < radius * 0.94f;
            }), "Low 3D bars form a local ring around a clear player center");
            Check(visual.maximumBarHeight <= 0.3f, "Audio response stays low around the feet");
            Check(visual.tileOpacity <= 0.3f && visual.transform.Find("Audio Floor").GetComponent<MeshRenderer>().sharedMaterial.renderQueue > 3000, "Wide tiles render translucently");
            Check(visual.transform.Find("Electric Halo") != null && visual.haloShader != null, "Soft electric circle is configured");
        }
    }
    private static void ValidateSpectrumMapping()
    {
        var bins = new float[1024]; var low = new float[16]; var high = new float[16];
        bins[4] = 0.1f; GameMusicDirector.AnalyseSpectrum(bins, 48000, 180, low);
        Array.Clear(bins, 0, bins.Length); bins[340] = 0.1f; GameMusicDirector.AnalyseSpectrum(bins, 48000, 180, high);
        Check(Array.IndexOf(low, low.Max()) < Array.IndexOf(high, high.Max()), "Bass and treble drive different floor bands");
    }
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Finish(int code)
    {
        Time.timeScale = 1;
        SessionState.SetBool(Running, false); EditorApplication.update -= Tick; EditorApplication.Exit(code);
    }
}
