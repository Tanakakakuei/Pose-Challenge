using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class HalloweenArenaSetup
{
    private const string Folder = "Assets/Environment/Halloween";
    private const string Request = "Temp/HalloweenArenaSetup.request";
    public const string ScenePath = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/GameMode-Halloween.unity";
    private static Mesh pumpkinMesh, wingMesh, faceMesh, cavityMesh, earMesh;
    private static Material orange, stem, dark, glow, wing, moon, mist, spark;

    [InitializeOnLoadMethod]
    private static void Watch() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(Request); } catch (IOException) { return; }
        try { Apply(); }
        catch (Exception error) { Directory.CreateDirectory("output/halloween"); File.WriteAllText("output/halloween/error.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/mocopi/Decorate Halloween Scene")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open GameMode-Halloween in edit mode.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory("output/halloween");
        if (!EditorSceneManager.SaveScene(scene, "output/halloween/before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
            throw new IOException("Could not back up the Halloween scene.");
        AssetDatabase.Refresh();
        foreach (string name in new[] { "Mocopi/Halloween Prop", "Mocopi/Halloween Wisp" })
            if (Shader.Find(name) == null || ShaderUtil.ShaderHasError(Shader.Find(name))) throw new Exception("Shader error: " + name);
        orange = Material("Pumpkin orange", new Color(0.95f, 0.255f, 0.025f), Color.black);
        stem = Material("Curled green stem", new Color(0.19f, 0.24f, 0.07f), Color.black);
        dark = Material("Carved recess", new Color(0.026f, 0.009f, 0.018f), Color.black);
        glow = Material("Candle glow", new Color(0.2f, 0.05f, 0), new Color(1.5f, 0.56f, 0.045f));
        wing = Material("Plum bat wings", new Color(0.11f, 0.035f, 0.16f), new Color(0.025f, 0.006f, 0.045f));
        moon = Material("Harvest moon", new Color(0.95f, 0.74f, 0.43f), new Color(0.8f, 0.51f, 0.2f));
        mist = Wisp("Ground mist", new Color(0.42f, 0.21f, 0.59f, 0.12f));
        spark = Wisp("Amber embers", new Color(2.5f, 0.8f, 0.1f, 0.8f));
        pumpkinMesh = SaveMesh("Ribbed pumpkin", Pumpkin());
        faceMesh = SaveMesh("Jack lantern face", Face(0.82f, -0.515f));
        cavityMesh = SaveMesh("Carved face outline", Face(1.04f, -0.508f));
        wingMesh = SaveMesh("Scalloped bat wing", Wing());
        earMesh = SaveMesh("Pointed bat ear", Mesh(new List<Vector3> { new Vector3(-0.055f, 0, 0), new Vector3(0.055f, 0, 0), new Vector3(0.025f, 0.14f, 0) }, new List<int> { 0, 1, 2 }));
        var loop = Object.FindFirstObjectByType<MocopiLocalGameLoopController>();
        var isolation = loop.GetComponent<PlayerArenaIsolation>();
        if (isolation == null || isolation.environments.Length < 2) throw new Exception("Player arenas are missing.");
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Halloween stage decoration");
        for (int player = 0; player < isolation.environments.Length; player++)
        {
            var environment = isolation.environments[player].transform;
            var old = environment.Find("HalloweenDecor");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            TintStage(environment);
            var root = new GameObject("HalloweenDecor");
            Undo.RegisterCreatedObjectUndo(root, "Halloween decoration");
            root.transform.SetParent(environment, false);
            Vector3 origin = loop.Players[player].CurrentMocopiAvatar.transform.position;
            origin.y = 0;
            root.transform.position = origin;
            Vector3 forward = Vector3.ProjectOnPlane(loop.Players[player].PlayerCamera.transform.forward, Vector3.up).normalized;
            root.transform.rotation = Quaternion.LookRotation(forward);
            var atmosphere = root.AddComponent<HalloweenAtmosphere>();
            atmosphere.controller = loop; atmosphere.playerIndex = player;
            var faces = new List<Renderer>(); var lights = new List<Light>(); var bats = new List<HalloweenAtmosphere.Bat>(); var embers = new List<ParticleSystem>();
            var random = new System.Random(1021);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 6; i++)
                {
                    float scale = i == 0 ? 1.55f : 0.95f + (float)random.NextDouble() * 0.55f;
                    var pumpkin = MakePumpkin(root.transform, new Vector3(side * (3.75f + i % 2 * 0.7f), scale * 0.45f, 4.2f + i * 4.5f), scale, faces);
                    float facing = Mathf.Atan2(Mathf.Abs(pumpkin.localPosition.x), pumpkin.localPosition.z + 3) * Mathf.Rad2Deg;
                    pumpkin.localRotation = Quaternion.Euler(0, side * facing, side * 3);
                    if (i == 0)
                    {
                        var lamp = new GameObject("Warm lantern pool", typeof(Light)).GetComponent<Light>();
                        lamp.transform.SetParent(pumpkin, false); lamp.transform.localPosition = new Vector3(0, 0.15f, -0.7f);
                        lamp.type = LightType.Point; lamp.color = new Color(1, 0.38f, 0.055f); lamp.range = 4; lamp.intensity = 1.3f;
                        lamp.shadows = LightShadows.None; lamp.renderMode = LightRenderMode.ForceVertex;
                        lights.Add(lamp);
                    }
                    if (i % 2 == 0) MakeGrave(root.transform, new Vector3(side * 5.6f, 0, 7 + i * 4.5f), side);
                }
                for (int i = 0; i < 6; i++)
                {
                    Vector3 pos = new Vector3(side * (3.8f + i % 2 * 0.6f), 3.7f + i % 3 * 0.6f, 5 + i * 4);
                    bats.Add(MakeBat(root.transform, pos, 0.9f + (float)random.NextDouble() * 0.4f, i + side * 2.4f));
                }
                MakeParticles(root.transform, "Low drifting mist", new Vector3(side * 4.7f, 0.18f, 15), mist, false);
                embers.Add(MakeParticles(root.transform, "Floating candle embers", new Vector3(side * 3.9f, 0.7f, 11), spark, true));
            }
            // A few high silhouettes frame the moon, above the entire three-metre wall.
            for (int i = 0; i < 4; i++) bats.Add(MakeBat(root.transform, new Vector3(-2.5f + i * 1.6f, 5.7f + i % 2 * 0.4f, 20 + i * 2), 1.2f, i * 2.1f));
            MakeMoon(root.transform);
            atmosphere.bats = bats.ToArray(); atmosphere.lanternFaces = faces.ToArray(); atmosphere.lanternLights = lights.ToArray(); atmosphere.embers = embers.ToArray();
            atmosphere.Animate(0);
            PlayerArenaLayers.Assign(root, PlayerArenaLayers.One + player);
            foreach (var light in root.GetComponentsInChildren<Light>()) light.cullingMask = 1 << (PlayerArenaLayers.One + player);
            Validate(root, atmosphere);
        }
        var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Halloween sky.mat");
        if (sky == null) { sky = new Material(RenderSettings.skybox); AssetDatabase.CreateAsset(sky, Folder + "/Halloween sky.mat"); }
        sky.SetColor("_Color", new Color(0.018f, 0.004f, 0.033f)); EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;
        RenderSettings.ambientLight = new Color(0.21f, 0.14f, 0.27f);
        RenderSettings.fogColor = new Color(0.035f, 0.009f, 0.05f);
        RenderSettings.fogStartDistance = 24; RenderSettings.fogEndDistance = 88;
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        SceneView.RepaintAll();
        File.WriteAllText("output/halloween/setup.txt", "HALLOWEEN_SETUP_PASSED\nPer arena: 12 pumpkins, 16 animated bats, harvest moon, 6 grave markers, 2 mist and 2 ember emitters. No decorative colliders. Isolated player layers.\n");
        Debug.Log("HALLOWEEN_SETUP_PASSED");
    }

    private static Material Material(string name, Color color, Color emission)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Mocopi/Halloween Prop")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_Color", color); mat.SetColor("_Emission", emission); EditorUtility.SetDirty(mat); return mat;
    }
    private static Material Wisp(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Mocopi/Halloween Wisp")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_Color", color); EditorUtility.SetDirty(mat); return mat;
    }
    private static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = Folder + "/" + name + ".asset"; mesh.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
    }
    private static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go.transform;
    }
    private static Transform Primitive(Transform parent, string name, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); var mesh = go.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(go);
        return Part(parent, name, mesh, material, position, scale);
    }
    private static Transform MakePumpkin(Transform parent, Vector3 position, float size, List<Renderer> faces)
    {
        var root = new GameObject("Jack-o-lantern").transform; root.SetParent(parent, false); root.localPosition = position; root.localScale = Vector3.one * size;
        Part(root, "Ribbed orange shell", pumpkinMesh, orange, Vector3.zero, Vector3.one);
        Part(root, "Carved recesses", cavityMesh, dark, Vector3.zero, Vector3.one);
        faces.Add(Part(root, "Glowing grin", faceMesh, glow, Vector3.zero, Vector3.one).GetComponent<Renderer>());
        var stalk = Primitive(root, "Crooked stem", PrimitiveType.Cylinder, stem, new Vector3(0.025f, 0.49f, 0), new Vector3(0.11f, 0.14f, 0.1f));
        stalk.localRotation = Quaternion.Euler(0, 0, -22);
        return root;
    }
    private static HalloweenAtmosphere.Bat MakeBat(Transform parent, Vector3 position, float scale, float phase)
    {
        var root = new GameObject("Flying bat").transform; root.SetParent(parent, false); root.localPosition = position; root.localScale = Vector3.one * scale;
        Primitive(root, "Body", PrimitiveType.Sphere, dark, Vector3.zero, new Vector3(0.16f, 0.31f, 0.12f));
        Primitive(root, "Head", PrimitiveType.Sphere, wing, new Vector3(0, 0.17f, 0), new Vector3(0.21f, 0.19f, 0.13f));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(root, "Pointed ear", earMesh, wing, new Vector3(side * 0.065f, 0.23f, -0.01f), new Vector3(side, 1, 1));
            Primitive(root, "Amber eye", PrimitiveType.Sphere, glow, new Vector3(side * 0.045f, 0.18f, -0.07f), Vector3.one * 0.027f);
        }
        var left = Part(root, "Left scalloped wing", wingMesh, wing, new Vector3(-0.065f, 0.05f, 0), new Vector3(-1, 1, 1));
        var right = Part(root, "Right scalloped wing", wingMesh, wing, new Vector3(0.065f, 0.05f, 0), Vector3.one);
        return new HalloweenAtmosphere.Bat { body = root, leftWing = left, rightWing = right, restPosition = position, phase = phase };
    }
    private static void MakeGrave(Transform parent, Vector3 position, int side)
    {
        var root = new GameObject("Distant grave marker").transform; root.SetParent(parent, false); root.localPosition = position; root.localRotation = Quaternion.Euler(0, -side * 18, side * 7);
        Primitive(root, "Stone", PrimitiveType.Cube, wing, new Vector3(0, 0.43f, 0), new Vector3(0.6f, 0.85f, 0.19f));
        Primitive(root, "Rounded crown", PrimitiveType.Sphere, wing, new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.38f, 0.19f));
        Primitive(root, "Engraved cross vertical", PrimitiveType.Cube, dark, new Vector3(0, 0.61f, -0.104f), new Vector3(0.045f, 0.32f, 0.01f));
        Primitive(root, "Engraved cross horizontal", PrimitiveType.Cube, dark, new Vector3(0, 0.65f, -0.105f), new Vector3(0.23f, 0.045f, 0.01f));
    }
    private static void MakeMoon(Transform parent)
    {
        var root = new GameObject("Harvest moon").transform; root.SetParent(parent, false); root.localPosition = new Vector3(-3.5f, 6.8f, 29);
        Primitive(root, "Golden moon", PrimitiveType.Sphere, moon, Vector3.zero, new Vector3(4.2f, 4.2f, 0.35f));
        var crater = Material("Moon craters", new Color(0.56f, 0.35f, 0.2f), new Color(0.22f, 0.12f, 0.05f));
        foreach (var p in new[] { new Vector3(-0.7f, 0.65f, -0.18f), new Vector3(0.65f, -0.5f, -0.18f), new Vector3(0.8f, 0.9f, -0.18f), new Vector3(-0.5f, -1.1f, -0.18f) })
            Primitive(root, "Soft crater", PrimitiveType.Sphere, crater, p, new Vector3(0.45f, 0.39f, 0.015f));
    }
    private static ParticleSystem MakeParticles(Transform parent, string name, Vector3 position, Material material, bool ember)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = true; main.prewarm = true; main.duration = 8; main.maxParticles = ember ? 64 : 45;
        main.startLifetime = ember ? 4 : 8; main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(ember ? 0.025f : 1.2f, ember ? 0.065f : 2.3f);
        main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = ps.emission; emission.rateOverTime = ember ? 7 : 4;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.55f, 0.08f, ember ? 15 : 25);
        var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = ember ? 0.45f : 0.035f; velocity.z = 0.12f; velocity.x = 0.025f;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) }); color.color = gradient;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        ps.useAutoRandomSeed = false; ps.randomSeed = ember ? 61u : 1021u;
        return ps;
    }
    private static void TintStage(Transform environment)
    {
        foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.transform.GetComponentsInParent<Transform>().Any(t => t.name == "NeonArena")) continue;
            var source = renderer.sharedMaterial; if (source == null || source.shader.name != "Mocopi/Neon Arena Surface") continue;
            string name = source.name.Replace("Halloween ", "");
            string path = Folder + "/Halloween " + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(source); AssetDatabase.CreateAsset(mat, path); }
            Color emission = name == "IonCyan" ? new Color(2.6f, 0.54f, 0.025f) : name == "IceWhite" ? new Color(2, 1, 0.25f) : new Color(0.9f, 0.07f, 1.9f);
            mat.SetColor("_NeonColor", emission);
            if (mat.GetFloat("_Mode") < 2.5f) mat.SetColor("_Color", new Color(0.026f, 0.012f, 0.039f));
            EditorUtility.SetDirty(mat); Undo.RecordObject(renderer, "Halloween stage colors"); renderer.sharedMaterial = mat;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
    private static Mesh Pumpkin()
    {
        const int segments = 64, rings = 24;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int y = 0; y <= rings; y++)
        {
            float theta = Mathf.PI * y / rings;
            for (int x = 0; x <= segments; x++)
            {
                float angle = 2 * Mathf.PI * x / segments;
                float r = 0.5f * Mathf.Sin(theta) * (1 + 0.075f * Mathf.Cos(angle * 10));
                vertices.Add(new Vector3(r * Mathf.Sin(angle), Mathf.Cos(theta) * 0.43f, r * Mathf.Cos(angle)));
                if (y < rings && x < segments)
                {
                    int a = y * (segments + 1) + x, b = a + segments + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
        }
        return Mesh(vertices, triangles);
    }
    private static Mesh Face(float scale, float z)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Action<Vector2[], Vector2> polygon = (points, center) =>
        {
            int first = v.Count;
            foreach (var p in points) { Vector2 q = center + (p - center) * scale; v.Add(new Vector3(q.x, q.y, z)); }
            for (int i = 1; i < points.Length - 1; i++) t.AddRange(new[] { first, first + i, first + i + 1 });
        };
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 c = new Vector2(side * 0.2f, 0.12f);
            polygon(new[] { new Vector2(side * 0.08f, 0.055f), new Vector2(side * 0.32f, 0.09f), new Vector2(side * 0.23f, 0.29f) }, c);
        }
        polygon(new[] { new Vector2(-0.055f, -0.035f), new Vector2(0.055f, -0.035f), new Vector2(0, 0.045f) }, new Vector2(0, -0.01f));
        for (int i = 0; i < 8; i++)
        {
            float x = -0.32f + i * 0.08f, next = x + 0.08f;
            float y0 = -0.17f + Mathf.Abs(x) * 0.22f, y1 = -0.17f + Mathf.Abs(next) * 0.22f;
            float tooth = i % 3 == 1 ? 0.065f : 0;
            polygon(new[] { new Vector2(x, y0 - tooth), new Vector2(next, y1 - tooth), new Vector2(next, y1 - 0.095f), new Vector2(x, y0 - 0.095f) }, new Vector2((x + next) * 0.5f, (y0 + y1) * 0.5f - 0.045f));
        }
        // Subdivide and wrap the carving onto the ribbed shell; a flat face floats off its sides.
        var curved = new List<Vector3>(); var triangles = new List<int>();
        Action<Vector3, Vector3, Vector3> triangle = (a, b, c) =>
        {
            int first = curved.Count;
            foreach (var p in new[] { a, b, c }) curved.Add(OnPumpkin(p, -z - 0.5f));
            triangles.AddRange(new[] { first, first + 1, first + 2 });
        };
        const int divisions = 6;
        for (int index = 0; index < t.Count; index += 3)
        {
            Vector3 a = v[t[index]], b = v[t[index + 1]], c = v[t[index + 2]];
            Func<int, int, Vector3> point = (i, j) => a + (b - a) * (i / (float)divisions) + (c - a) * (j / (float)divisions);
            for (int i = 0; i < divisions; i++)
            for (int j = 0; j < divisions - i; j++)
            {
                triangle(point(i, j), point(i + 1, j), point(i, j + 1));
                if (i + j < divisions - 1) triangle(point(i + 1, j), point(i + 1, j + 1), point(i, j + 1));
            }
        }
        return Mesh(curved, triangles);
    }
    private static Vector3 OnPumpkin(Vector3 p, float offset)
    {
        float radius = 0.5f * Mathf.Sqrt(Mathf.Max(0.001f, 1 - p.y * p.y / (0.43f * 0.43f)));
        float z = -Mathf.Sqrt(Mathf.Max(0.001f, radius * radius - p.x * p.x));
        for (int i = 0; i < 12; i++)
        {
            float ribbed = radius * (1 + 0.075f * Mathf.Cos(Mathf.Atan2(p.x, z) * 10));
            z = -Mathf.Sqrt(Mathf.Max(0.001f, ribbed * ribbed - p.x * p.x));
        }
        return new Vector3(p.x, p.y, z - offset);
    }
    private static Mesh Wing()
    {
        var v = new List<Vector3> { Vector3.zero, new Vector3(0.18f,0.28f), new Vector3(0.48f,0.36f), new Vector3(0.83f,0.13f), new Vector3(0.56f,0.12f), new Vector3(0.47f,-0.08f), new Vector3(0.34f,0.025f), new Vector3(0.24f,-0.18f), new Vector3(0.13f,-0.07f), new Vector3(0.02f,-0.23f) };
        var t = new List<int>(); for (int i = 1; i < v.Count - 1; i++) t.AddRange(new[] { 0, i, i + 1 });
        return Mesh(v, t);
    }
    private static Mesh Mesh(List<Vector3> vertices, List<int> triangles)
    {
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    private static void Validate(GameObject root, HalloweenAtmosphere atmosphere)
    {
        if (root.GetComponentsInChildren<Collider>(true).Length != 0) throw new Exception("Decoration must not add gameplay collisions.");
        if (atmosphere.bats.Length != 16 || atmosphere.lanternFaces.Length != 12) throw new Exception("Missing seasonal props.");
        if (root.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterial == null || r.gameObject.layer != root.layer)) throw new Exception("Invalid decoration material or arena layer.");
    }
}
