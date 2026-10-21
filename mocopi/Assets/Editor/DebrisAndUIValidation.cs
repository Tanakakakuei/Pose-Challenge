using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Run in a disposable project copy: actual PhysX contacts, expiry, score and menu audio.</summary>
public static class DebrisAndUIValidation
{
    private const string Key = "DebrisAndUIValidation.Running";
    private static int phase, clicks;
    private static double began, phaseAt;
    private static WallCube rolling, expiring, onPlayer;
    private static GameObject wall;
    private static WallHitTracker hits;
    private static MocopiLocalGameLoopController loop;
    private static Quaternion initialRotation;
    private static Vector3 initialPosition;
    private static TouchableUIPanel panel;

    public static void Run()
    {
        EditorSceneManager.OpenScene(GameModeValidation.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        began = phaseAt = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 60) throw new Exception("Timed out: " + phase);
            double elapsed = EditorApplication.timeSinceStartup - phaseAt;
            if (phase == 0)
            {
                if (elapsed < 1) return;
                loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
                Check(loop != null, "GameMode loaded");
                var floor = Box("Floor", new Vector3(200, -0.05f, 0), new Vector3(20, 0.1f, 20), 8);
                Box("Forbidden wall and UI stand", new Vector3(200, 0.6f, 0), new Vector3(20, 0.08f, 20), 8);
                var otherFloor = Box("Floor", new Vector3(200, 0.4f, 0), new Vector3(20, 0.08f, 20), 9);
                var player = Box("Player", new Vector3(200.2f, 1, 0), Vector3.one * 0.1f, 8);
                player.gameObject.tag = "Player";
                var playerPlatform = Box("Player body contact test", new Vector3(204, 0.4f, 0), new Vector3(3, 0.1f, 3), 8);
                playerPlatform.gameObject.tag = "Player";
                var debrisPlatform = Box("Other detached cube", new Vector3(200, 0.8f, 0), new Vector3(2, 0.05f, 2), 10);
                debrisPlatform.excludeLayers = ~0;
                debrisPlatform.layerOverridePriority = 10;
                WallDebrisCollision.ConfigureScene(floor.gameObject.scene, null);
                wall = new GameObject("Test wall"); wall.layer = 8;
                wall.AddComponent<WallMaskData>();
                hits = wall.AddComponent<WallHitTracker>();
                hits.Owner = loop.Players[0].WallManager;
                hits.Velocity = new Vector3(0, 0, -1);
                rolling = Cube(new Vector3(200, 1, 0), 15);
                expiring = Cube(new Vector3(201, 1, 0), 0.3f);
                onPlayer = Cube(new Vector3(204, 1, 0), 15);
                initialRotation = rolling.transform.rotation;
                initialPosition = rolling.transform.position;
                rolling.SendMessage("OnTriggerEnter", player);
                expiring.SendMessage("OnTriggerEnter", player);
                onPlayer.SendMessage("OnTriggerEnter", playerPlatform);
                rolling.SendMessage("OnTriggerEnter", player);
                Check(hits.HitCount == 3, "one hit per cube");
                Check(rolling.transform.parent == null, "detached from wall");
                var body = rolling.GetComponent<Rigidbody>();
                Check(!body.isKinematic && body.useGravity && body.constraints == RigidbodyConstraints.None, "gravity and rotation released");
                Check(rolling.gameObject.layer == 8 && rolling.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger && c.gameObject.layer == 10), "renderer layer preserved / physics isolated");
                Check(Resources.Load<AudioClip>("Audio/UI/UIConfirm") != null && Resources.Load<AudioClip>("Audio/UI/UICancel") != null, "audio resources present");
                var menus = Object.FindObjectsByType<TouchableUIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Check(menus.Any(p => p.Label.Contains("このアバター") && p.ResolvedClickSound == UIActionSound.Confirm), "avatar confirm audio");
                Check(menus.Any(p => p.Label.Contains("選び直") && p.ResolvedClickSound == UIActionSound.Cancel), "cancel audio");
                Check(menus.Any(p => p.Label.Contains("協力") && p.ResolvedClickSound == UIActionSound.Confirm), "mode select audio");
                panel = new GameObject("Audio test panel").AddComponent<TouchableUIPanel>();
                panel.SetClickSound(UIActionSound.Confirm);
                Set(panel, "enabledAt", Time.unscaledTime - 1);
                panel.OnClick.AddListener(() => { clicks++; panel.gameObject.SetActive(false); });
                panel.Activate(); panel.Activate();
                Check(clicks == 1, "one activation while hiding panel");
                Check(Object.FindFirstObjectByType<UIActionAudio>() != null, "audio survives panel hide");
                phase = 1; phaseAt = EditorApplication.timeSinceStartup;
            }
            else if (phase == 1)
            {
                if (elapsed < 2.5) return;
                Check(expiring == null, "independent lifetime expires");
                Check(hits.HitCount == 3, "expiry preserves hit total");
                int counted = (int)typeof(WallManager).GetMethod("CountHitCubes", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(loop.Players[0].WallManager, new object[] { wall });
                Check(counted == 3, "wall scoring counts expired debris");
                Check(onPlayer.transform.position.y > 0.45f && onPlayer.transform.position.y < 0.54f,
                    "solid player contact supports debris: " + onPlayer.transform.position);
                Check(rolling != null && rolling.transform.position.y > 0 && rolling.transform.position.y < 0.09f,
                    "lands only on own floor; passes walls and other player's floor: " + rolling.transform.position);
                Check(Quaternion.Angle(initialRotation, rolling.transform.rotation) > 10, "tumbles physically");
                Check(Vector3.Distance(initialPosition, rolling.transform.position) > 0.5f, "inherits movement");
                UIActionAudio.Play(UIActionSound.Cancel);
                var voice = Object.FindFirstObjectByType<UIActionAudio>().GetComponent<AudioSource>();
                Check(voice.clip.name == "UICancel" && voice.spatialBlend == 0 && Mathf.Approximately(voice.volume, 0.65f), "shared cancel voice and volume");
                Object.Destroy(wall);
                phase = 2; phaseAt = EditorApplication.timeSinceStartup;
            }
            else if (phase == 2)
            {
                if (elapsed < 0.25) return;
                Check(rolling != null, "wall destruction does not destroy debris");
                loop.Players[0].WallManager.ClearActiveWalls();
                phase = 3; phaseAt = EditorApplication.timeSinceStartup;
            }
            else if (phase == 3)
            {
                if (elapsed < 0.2) return;
                Check(rolling == null, "retry cleanup handles already destroyed wall");
                Debug.Log("DEBRIS_UI_VALIDATION_PASSED");
                Finish(0);
            }
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    private static BoxCollider Box(string name, Vector3 position, Vector3 size, int layer)
    {
        var go = new GameObject(name); go.layer = layer; go.transform.position = position;
        var collider = go.AddComponent<BoxCollider>(); collider.size = size; return collider;
    }

    private static WallCube Cube(Vector3 position, float lifetime)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(wall.transform); go.layer = 8;
        go.transform.position = position; go.transform.localScale = Vector3.one * 0.05f;
        go.GetComponent<BoxCollider>().isTrigger = true;
        var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        var cube = go.AddComponent<WallCube>();
        Set(cube, "lifeTime", lifetime); Set(cube, "lifeTimeVariation", 0f);
        return cube;
    }
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Debug.Log("DEBRIS_UI_CHECK: " + message);
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
