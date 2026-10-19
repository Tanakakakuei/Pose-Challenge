using UnityEngine;

/// <summary>Small HDR bloom for the built-in pipeline, limited to emissive stage lights.</summary>
[ExecuteAlways, RequireComponent(typeof(Camera)), ImageEffectAllowedInSceneView]
public sealed class NeonArenaBloom : MonoBehaviour
{
    [SerializeField] private Shader bloomShader;
    [Range(0.5f, 3f)] public float threshold = 1.1f;
    [Range(0f, 1f)] public float intensity = 0.38f;
    private Material material;

    public void Configure(Shader shader) { bloomShader = shader; }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (bloomShader == null || !bloomShader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (material == null)
            material = new Material(bloomShader) { hideFlags = HideFlags.HideAndDontSave };
        int width = Mathf.Max(1, source.width / 4), height = Mathf.Max(1, source.height / 4);
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
        var a = RenderTexture.GetTemporary(width, height, 0, format);
        var b = RenderTexture.GetTemporary(width, height, 0, format);
        try
        {
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            material.SetFloat("_Threshold", threshold);
            material.SetFloat("_Intensity", intensity);
            Graphics.Blit(source, a, material, 0);
            for (int i = 0; i < 3; i++)
            {
                material.SetVector("_BlurDirection", new Vector4(1 + i, 0, 0, 0));
                Graphics.Blit(a, b, material, 1);
                material.SetVector("_BlurDirection", new Vector4(0, 1 + i, 0, 0));
                Graphics.Blit(b, a, material, 1);
            }
            material.SetTexture("_BloomTex", a);
            Graphics.Blit(source, destination, material, 2);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }
    }

    private void OnDisable()
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        material = null;
    }
}
