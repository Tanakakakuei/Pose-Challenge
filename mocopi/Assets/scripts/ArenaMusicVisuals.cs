using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Approaching tunnel lights and a translucent circular spectral floor.</summary>
public sealed class ArenaMusicVisuals : MonoBehaviour
{
    public GameMusicDirector music;
    public Shader trailShader, floorShader, haloShader;
    [Min(0)] public float idleSpeed = 5;
    [Min(0)] public float playSpeed = 28;
    [Range(0.05f, 2f)] public float maximumBarHeight = 0.22f;
    [Min(0.5f)] public float circleDiameter = 3.8f;
    [Range(0, 1)] public float tileOpacity = 0.24f;
    [ColorUsage(false, true)] public Color cyan = new Color(0.85f, 1.55f, 1.65f);
    [ColorUsage(false, true)] public Color lavender = new Color(1.35f, 1.1f, 1.65f);
    private Mesh haloMesh;
    private Material haloMaterial;
    private MeshRenderer haloRenderer;
    private Mesh trailMesh, floorMesh;
    private Material trailMaterial, floorMaterial;
    private MeshRenderer trailRenderer, floorRenderer;
    private MaterialPropertyBlock properties;
    private float travelled;
    private readonly List<(MeshFilter filter, Mesh original, Mesh replacement)> replacedPads = new List<(MeshFilter, Mesh, Mesh)>();
    public float Travelled => travelled;
    private bool sharedFloor;
    private Vector3 sharedFloorCenter;
    private float sharedFloorDiameter;

    public void SetSharedFloor(bool shared, Vector3 center, float diameter)
    {
        sharedFloor = shared;
        sharedFloorCenter = center;
        sharedFloorDiameter = diameter;
        UpdateFloorLayout();
    }

    private void UpdateFloorLayout()
    {
        if (floorRenderer == null || haloRenderer == null) return;
        Vector3 center = sharedFloor ? transform.InverseTransformPoint(sharedFloorCenter) : Vector3.zero;
        center.y = 0;
        float scale = sharedFloor ? sharedFloorDiameter / circleDiameter : 1;
        floorRenderer.transform.localPosition = center;
        haloRenderer.transform.localPosition = center + Vector3.up * 0.045f;
        floorRenderer.transform.localScale = haloRenderer.transform.localScale = new Vector3(scale, 1, scale);
    }

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        ReplaceLegacyPad();
        trailMesh = BuildTunnel();
        trailMaterial = new Material(trailShader) { name = "Passing tunnel lights" };
        trailRenderer = CreateRenderer("Tunnel Light Trails", trailMesh, trailMaterial);
        floorMesh = BuildFloor();
        floorMaterial = new Material(floorShader) { name = "Pastel spectral ring" };
        floorRenderer = CreateRenderer("Audio Floor", floorMesh, floorMaterial);
        float radius = circleDiameter * 0.5f;
        haloMesh = new Mesh { name = "Electric circle plane" };
        haloMesh.vertices = new[] { new Vector3(-radius,0,-radius), new Vector3(-radius,0,radius), new Vector3(radius,0,radius), new Vector3(radius,0,-radius) };
        haloMesh.uv = new[] { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
        haloMesh.triangles = new[] { 0,1,2,0,2,3 }; haloMesh.RecalculateBounds();
        haloMaterial = new Material(haloShader) { name = "Soft electric halo" };
        haloRenderer = CreateRenderer("Electric Halo", haloMesh, haloMaterial);
        haloRenderer.transform.localPosition = Vector3.up * 0.045f;
        UpdateFloorLayout();
        ApplyProperties();
    }

    private MeshRenderer CreateRenderer(string label, Mesh mesh, Material material)
    {
        var child = new GameObject(label); child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return renderer;
    }

    private static Mesh BuildTunnel()
    {
        // Follow the five inner faces of NeonArena's arch, leaving the entire floor open.
        var profile = new[] { new Vector2(-6.82f,0.8f), new Vector2(-6.82f,4.92f),
            new Vector2(-4.42f,7.82f), new Vector2(4.42f,7.82f),
            new Vector2(6.82f,4.92f), new Vector2(6.82f,0.8f) };
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var indices = new List<int>();
        float distance = 0;
        for (int i = 0; i < profile.Length - 1; i++)
        {
            Vector2 a = profile[i], b = profile[i+1];
            float end = distance + Vector2.Distance(a,b); int n = vertices.Count;
            vertices.Add(new Vector3(a.x,a.y,-12)); vertices.Add(new Vector3(a.x,a.y,72));
            vertices.Add(new Vector3(b.x,b.y,72)); vertices.Add(new Vector3(b.x,b.y,-12));
            uvs.Add(new Vector2(distance,-12)); uvs.Add(new Vector2(distance,72));
            uvs.Add(new Vector2(end,72)); uvs.Add(new Vector2(end,-12));
            indices.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 }); distance = end;
        }
        var mesh = new Mesh { name = "Open-bottom tunnel light shell" };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
        return mesh;
    }

    private void ReplaceLegacyPad()
    {
        // The old 3.2 m pad is baked into the neon mesh. Remove only those triangles
        // from a runtime copy; the shared environment asset and CaptureMode stay intact.
        if (transform.parent == null) return;
        foreach (var filter in transform.parent.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "IonCyan" && filter.name != "IceWhite") continue;
            var original = filter.sharedMesh;
            if (original == null || !original.isReadable || original.subMeshCount != 1) continue;
            var vertices = original.vertices; var indices = original.triangles;
            var kept = new List<int>(indices.Length);
            for (int i = 0; i < indices.Length; i += 3)
            {
                if (IsPad(vertices[indices[i]]) && IsPad(vertices[indices[i+1]]) && IsPad(vertices[indices[i+2]])) continue;
                kept.Add(indices[i]); kept.Add(indices[i+1]); kept.Add(indices[i+2]);
            }
            if (kept.Count == indices.Length) continue;
            var replacement = Instantiate(original); replacement.name = original.name + " without legacy pad";
            replacement.SetTriangles(kept, 0); filter.sharedMesh = replacement;
            replacedPads.Add((filter, original, replacement));
        }
        bool IsPad(Vector3 v) => Mathf.Abs(v.y - 0.018f) < 0.025f && Mathf.Abs(new Vector2(v.x,v.z).magnitude - 1.6f) < 0.04f;
    }

    private void Update()
    {
        travelled += Time.unscaledDeltaTime * Mathf.Lerp(idleSpeed, playSpeed, music != null ? music.Visibility : 0);
        ApplyProperties();
    }

    private void ApplyProperties()
    {
        properties.Clear();
        properties.SetFloat("_Travel", travelled);
        properties.SetFloat("_Activity", music != null ? music.Visibility : 0);
        properties.SetFloat("_Energy", music != null ? music.Energy : 0);
        properties.SetFloat("_MaxHeight", maximumBarHeight);
        properties.SetFloat("_TileOpacity", tileOpacity);
        properties.SetFloat("_Radius", circleDiameter * 0.5f);
        properties.SetColor("_Cyan", cyan); properties.SetColor("_Lavender", lavender);
        if (music != null && music.SpectrumHistory != null) properties.SetTexture("_SpectrumHistory", music.SpectrumHistory);
        trailRenderer.SetPropertyBlock(properties); floorRenderer.SetPropertyBlock(properties); haloRenderer.SetPropertyBlock(properties);
    }

    private Mesh BuildFloor()
    {
        var positions = new List<Vector3>(); var normals = new List<Vector3>();
        var uvs = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
        const int columns = 32, rows = 2;
        float radius = circleDiameter * 0.5f;
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            float angleA = (column + 0.1f) * Mathf.PI * 2 / columns;
            float angleB = (column + 0.9f) * Mathf.PI * 2 / columns;
            float inner = radius * (0.76f + row * 0.09f), outer = inner + radius * 0.072f;
            Vector3 a = new Vector3(Mathf.Cos(angleA),0,Mathf.Sin(angleA)) * inner;
            Vector3 b = new Vector3(Mathf.Cos(angleB),0,Mathf.Sin(angleB)) * inner;
            Vector3 c = b.normalized * outer, d = a.normalized * outer;
            // Mirror bass/treble around the circle; outer rows carry a little audio history.
            float band = Mathf.PingPong((column + 0.5f) / columns * 2, 1);
            Color data = new Color(Mathf.Lerp(0.03125f,0.96875f,band), (row * 3 + 0.5f) / GameMusicDirector.HistoryRows, (column + 0.5f) / columns, 1);
            AddFace(a+Vector3.up,b+Vector3.up,c+Vector3.up,d+Vector3.up,Vector3.up,data);
            AddFace(a,a+Vector3.up,d+Vector3.up,d,-b.normalized,data);
            AddFace(c,c+Vector3.up,b+Vector3.up,b,b.normalized,data);
            AddFace(b,b+Vector3.up,a+Vector3.up,a,-a.normalized,data);
            AddFace(d,d+Vector3.up,c+Vector3.up,c,d.normalized,data);
        }
        var mesh = new Mesh { name = "Circular audio floor - 64 wide tiles" };
        mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        // The vertex shader raises the blocks; include the configurable maximum height in culling.
        mesh.bounds = new Bounds(Vector3.up, new Vector3(circleDiameter,4,circleDiameter));
        return mesh;

        void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color data)
        {
            int index = positions.Count;
            positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
            for (int i = 0; i < 4; i++) { normals.Add(normal); colors.Add(data); }
            uvs.Add(Vector2.zero); uvs.Add(Vector2.up); uvs.Add(Vector2.one); uvs.Add(Vector2.right);
            triangles.Add(index); triangles.Add(index+1); triangles.Add(index+2);
            triangles.Add(index); triangles.Add(index+2); triangles.Add(index+3);
        }
    }

    private void OnDestroy()
    {
        foreach (var pad in replacedPads)
        {
            if (pad.filter != null && pad.filter.sharedMesh == pad.replacement) pad.filter.sharedMesh = pad.original;
            if (pad.replacement != null) Destroy(pad.replacement);
        }
        if (trailMesh != null) Destroy(trailMesh);
        if (floorMesh != null) Destroy(floorMesh);
        if (trailMaterial != null) Destroy(trailMaterial);
        if (floorMaterial != null) Destroy(floorMaterial);
        if (haloMesh != null) Destroy(haloMesh);
        if (haloMaterial != null) Destroy(haloMaterial);
    }
}
