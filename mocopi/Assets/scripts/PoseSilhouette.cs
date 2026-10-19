using System;
using UnityEngine;

/// <summary>Frozen, supersampled mesh silhouette. Distances are measured in wall-local metres.</summary>
public sealed class PoseSilhouette
{
    private readonly int rows, columns, samples;
    private readonly float cellSize;
    private readonly float[,] distances;

    public PoseSilhouette(bool[,] holes, int rows, int columns, int samples, float cellSize)
    {
        this.rows = rows;
        this.columns = columns;
        this.samples = samples;
        this.cellSize = cellSize;
        int height = rows * samples, width = columns * samples;
        if (holes.GetLength(0) != height || holes.GetLength(1) != width)
            throw new ArgumentException("Silhouette dimensions do not match the grid.");

        // Exact separable squared Euclidean distance transform, calculated once per capture.
        var horizontal = new float[height, width];
        distances = new float[height, width];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < width; k++)
                if (holes[y, k]) best = Mathf.Min(best, (x - k) * (x - k));
            horizontal[y, x] = best;
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float best = float.PositiveInfinity;
            for (int k = 0; k < height; k++)
                best = Mathf.Min(best, horizontal[k, x] + (y - k) * (y - k));
            distances[y, x] = best;
        }
    }

    public bool[,] CreateMask(float marginMeters)
    {
        float radius = Mathf.Max(0, marginMeters) * samples / cellSize;
        float threshold = radius * radius + 0.0001f;
        var mask = new bool[rows, columns];
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            bool solid = true;
            for (int sy = 0; sy < samples && solid; sy++)
            for (int sx = 0; sx < samples; sx++)
                if (distances[y * samples + sy, x * samples + sx] <= threshold)
                {
                    solid = false;
                    break;
                }
            mask[y, x] = solid;
        }
        return mask;
    }

    public static PoseSilhouette Capture(Transform avatar, Transform projection,
        int rows, int columns, float cellSize, int samples)
    {
        var holes = new bool[rows * samples, columns * samples];
        foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!renderer.enabled || renderer.sharedMesh == null) continue;
            var baked = new Mesh();
            try
            {
                // Compensate for Transform scale in the baked vertices, then apply it once below.
                // With the default false, a scaled avatar's silhouette was scaled a second time.
                renderer.BakeMesh(baked, true);
                Rasterize(holes, baked, projection.worldToLocalMatrix * renderer.transform.localToWorldMatrix,
                    columns * cellSize, rows * cellSize);
            }
            finally { Release(baked); }
        }
        foreach (var filter in avatar.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
            if (!filter.sharedMesh.isReadable)
                throw new InvalidOperationException($"{filter.name}: メッシュの Read/Write を有効にしてください。");
            Rasterize(holes, filter.sharedMesh, projection.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                columns * cellSize, rows * cellSize);
        }
        return new PoseSilhouette(holes, rows, columns, samples, cellSize);
    }

    private static void Rasterize(bool[,] holes, Mesh mesh, Matrix4x4 matrix, float width, float height)
    {
        int h = holes.GetLength(0), w = holes.GetLength(1);
        Vector3[] vertices = mesh.vertices;
        var points = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 local = matrix.MultiplyPoint3x4(vertices[i]);
            points[i] = new Vector2((local.x / width + 0.5f) * w, (0.5f - local.y / height) * h);
        }
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 0.000001f) continue;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(w - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(h - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float u = Cross(b - a, p - a) / area;
                float v = Cross(p - a, c - a) / area;
                if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f) holes[y, x] = true;
            }
        }
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    private static void Release(UnityEngine.Object value)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
