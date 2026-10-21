using UnityEngine;

public enum UIActionSound { Automatic, Confirm, Cancel, None }

/// <summary>A shared 2D voice keeps feedback equally loud in both player views and across menus.</summary>
public sealed class UIActionAudio : MonoBehaviour
{
    private static UIActionAudio instance;
    private AudioSource voice;
    private AudioClip confirm, cancel;
    private float lastPlayedAt = float.NegativeInfinity;
    private const float Volume = 0.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static void Play(UIActionSound sound)
    {
        if (!Application.isPlaying || sound == UIActionSound.None) return;
        if (instance == null)
        {
            var root = new GameObject("UI Action Audio");
            instance = root.AddComponent<UIActionAudio>();
            DontDestroyOnLoad(root);
            instance.voice = root.AddComponent<AudioSource>();
            instance.voice.playOnAwake = false;
            instance.voice.spatialBlend = 0;
            instance.voice.volume = Volume;
            instance.voice.priority = 16;
            instance.voice.ignoreListenerPause = true;
            instance.confirm = Resources.Load<AudioClip>("Audio/UI/UIConfirm");
            instance.cancel = Resources.Load<AudioClip>("Audio/UI/UICancel");
        }
        // Simultaneous hands/players must not stack the same feedback into a loud burst.
        if (Time.unscaledTime - instance.lastPlayedAt < 0.06f) return;
        var clip = sound == UIActionSound.Cancel ? instance.cancel : instance.confirm;
        if (clip == null) return;
        instance.lastPlayedAt = Time.unscaledTime;
        instance.voice.clip = clip;
        instance.voice.Play();
    }
}
