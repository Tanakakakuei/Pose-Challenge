using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Bakes a reusable neon stage and applies it without changing game rules or avatar bindings.</summary>
public static class NeonArenaSetup
{
    private const string Folder = "Assets/Environment/NeonArena";
    private const string PrefabPath = Folder + "/NeonArena.prefab";
    private const string Scenes = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/";
    private static readonly Dictionary<Material, List<CombineInstance>> Geometry = new Dictionary<Material, List<CombineInstance>>();
    private static Mesh cubeMesh;

    [MenuItem("Tools/mocopi/Build Neon Arena")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building the stage.");
        foreach (string name in new[] { "Mocopi/Neon Arena Surface", "Mocopi/Neon Arena Sky", "Hidden/Mocopi/Neon Arena Bloom" })
        {
            Shader shader = Shader.Find(name);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Shader is not ready: " + name);
        }
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Temp/NeonArenaPreview");
        AssetDatabase.Refresh();
        Material metal = Surface("Obsidian", new Color(0.013f, 0.022f, 0.038f), new Color(0.1f, 0.4f, 0.6f), 0);
        Material floor = Surface("Runway", new Color(0.008f, 0.015f, 0.026f), new Color(0.05f, 0.8f, 1.2f), 1);
        Material cyan = Surface("IonCyan", Color.black, new Color(0.08f, 2.4f, 3.8f), 3);
        Material violet = Surface("Ultraviolet", Color.black, new Color(1.4f, 0.16f, 3.2f), 3);
        Material white = Surface("IceWhite", Color.black, new Color(1.7f, 2.5f, 2.8f), 3);
        Material dim = Surface("DistantBlue", Color.black, new Color(0.015f, 0.22f, 0.48f), 3);
        Material wall = Surface("PoseWall", new Color32(146, 155, 194, 255), new Color(0.18f, 0.28f, 0.4f), 2);
        wall.SetFloat("_Glow", 0.25f);
        wall.SetColor("_HoleGlowColor", new Color(0.65f, 3.2f, 1f));
        wall.SetFloat("_HoleEdgeWidth", 0.06f);
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/NightSky.mat");
        if (sky == null)
        {
            sky = new Material(Shader.Find("Mocopi/Neon Arena Sky"));
            AssetDatabase.CreateAsset(sky, Folder + "/NightSky.mat");
        }
        BakeStage(metal, floor, cyan, violet, white, dim);
        Scene original = SceneManager.GetActiveScene();
        var touched = new List<string>();
        try
        {
            foreach (string name in new[] { "CaptureMaskDataMode", "GameMode" })
            {
                string path = Scenes + name + ".unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    ApplyScene(scene, sky, wall);
                    EditorSceneManager.SaveScene(scene);
                    touched.Add(path);
                }
                finally
                {
                    if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally { if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original); }
        StyleWallPrefabs(wall);
        // Forward-rendered neon edges need sufficient coverage sampling.
        QualitySettings.antiAliasing = Math.Max(QualitySettings.antiAliasing, 4);
        AssetDatabase.SaveAssets();
        RenderPreview();
        Validate();
        File.WriteAllText("Temp/NeonArenaPreview/result.txt", "NEON_ARENA_PASSED\n" + string.Join("\n", touched));
        SceneView.RepaintAll();
        Debug.Log("NEON_ARENA_PASSED: both scenes, wall materials, camera bloom and baked stage validated.");
    }

    private static Material Surface(string name, Color color, Color neon, int mode)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Mocopi/Neon Arena Surface")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_Color", color);
        material.SetColor("_NeonColor", neon);
        material.SetFloat("_Mode", mode);
        material.SetFloat("_Glow", 1);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Box(Material mat, Vector3 center, Vector3 scale, Quaternion rotation)
    {
        if (!Geometry.TryGetValue(mat, out var list)) Geometry.Add(mat, list = new List<CombineInstance>());
        list.Add(new CombineInstance { mesh = cubeMesh, transform = Matrix4x4.TRS(center, rotation, scale) });
    }
    private static void Box(Material mat, Vector3 center, Vector3 scale) => Box(mat, center, scale, Quaternion.identity);
    private static void Line(Material mat, Vector3 a, Vector3 b, float width)
        => Box(mat, (a+b)*0.5f, new Vector3(width, Vector3.Distance(a,b), width), Quaternion.FromToRotation(Vector3.up, b-a));

    private static void BakeStage(Material metal, Material floor, Material cyan, Material violet, Material white, Material dim)
    {
        Geometry.Clear();
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cube);
        Box(floor, new Vector3(0,-0.08f,0), new Vector3(13,0.16f,150));
        for (int side=-1; side<=1; side+=2)
        {
            Box(metal, new Vector3(side*7.5f,-0.35f,0), new Vector3(1.6f,0.5f,150));
            Line(cyan, new Vector3(side*4.8f,0.012f,-72),new Vector3(side*4.8f,0.012f,72),0.026f);
            Line(violet,new Vector3(side*6.4f,-0.02f,-72),new Vector3(side*6.4f,-0.02f,72),0.025f);
            Line(dim,new Vector3(side*8,1.2f,-72),new Vector3(side*8,1.2f,72),0.055f);
            for(int z=-66;z<=66;z+=6)
            {
                Box(metal,new Vector3(side*5.65f,0.015f,z),new Vector3(1.15f,0.07f,0.6f));
                for(int mark=0;mark<3;mark++)
                    Line(z%12==0?cyan:dim,new Vector3(side*5.4f,0.06f,z-0.17f+mark*0.15f),new Vector3(side*5.9f,0.06f,z-0.02f+mark*0.15f),0.023f);
            }
        }
        // The central play volume is deliberately clear; frames sit outside the wall lanes.
        foreach(int z in new[] {-64,-50,-38,-27,-17,17,27,38,50,64})
        {
            var p=new[] {new Vector3(-7,0,z),new Vector3(-7,5,z),new Vector3(-4.5f,8,z),new Vector3(4.5f,8,z),new Vector3(7,5,z),new Vector3(7,0,z)};
            for(int i=0;i<p.Length-1;i++)
            {
                Line(metal,p[i],p[i+1],0.28f);
                Material light=Math.Abs(z)==38?violet:cyan;
                Line(light,p[i]+Vector3.forward*0.15f,p[i+1]+Vector3.forward*0.15f,0.035f);
                Line(light,p[i]-Vector3.forward*0.15f,p[i+1]-Vector3.forward*0.15f,0.035f);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Line(metal,new Vector3(side*7,0,z),new Vector3(side*12,10,z+3),0.5f);
                Line(dim,new Vector3(side*8,1,z-4),new Vector3(side*13,13,z+5),0.035f);
                Line(Math.Abs(z)==27?violet:cyan,new Vector3(side*7.7f,2,z+4),new Vector3(side*9.8f,10,z-3),0.025f);
            }
            Box(white,new Vector3(0,7.82f,z),new Vector3(1.1f,0.028f,0.05f));
        }
        for(int side=-1;side<=1;side+=2)
        {
            // Distant nested diamonds give each viewing direction a clear focal point.
            for(int ring=0;ring<3;ring++)
            {
                float r=4.4f-ring*0.85f, z=side*(73+ring*1.6f);
                Vector3 c=new Vector3(0,4.8f,z);
                var corners=new[] {c+Vector3.up*r,c+Vector3.right*r,c+Vector3.down*r,c+Vector3.left*r};
                for(int i=0;i<4;i++) Line(ring==1?violet:cyan,corners[i],corners[(i+1)%4],0.055f);
            }
        }
        // Player pad: a broken ring, thin enough to stay behind avatar feet.
        for(int i=0;i<64;i++)
        {
            if(i%16<3)continue;
            float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
            Line(i%16==3?white:cyan,new Vector3(Mathf.Cos(a)*1.6f,0.018f,Mathf.Sin(a)*1.6f),new Vector3(Mathf.Cos(b)*1.6f,0.018f,Mathf.Sin(b)*1.6f),0.018f);
        }
        var root=new GameObject("NeonArena");
        try
        {
            foreach(var entry in Geometry)
            {
                var mesh=new Mesh { name=entry.Key.name+" Geometry", indexFormat=IndexFormat.UInt32 };
                mesh.CombineMeshes(entry.Value.ToArray(),true,true);
                string path=Folder+"/"+entry.Key.name+".asset";
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(asset==null) { AssetDatabase.CreateAsset(mesh,path); asset=mesh; }
                else { EditorUtility.CopySerialized(mesh,asset); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(asset); }
                var part=new GameObject(entry.Key.name);
                part.transform.SetParent(root.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=asset;
                var renderer=part.AddComponent<MeshRenderer>(); renderer.sharedMaterial=entry.Key;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            }
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { Object.DestroyImmediate(root); Geometry.Clear(); }
    }

    private static void ApplyScene(Scene scene, Material sky, Material wall)
    {
        var roots=scene.GetRootGameObjects();
        if(!roots.Any(r=>r.name=="NeonArena"))
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);
        RenderSettings.skybox=sky;
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(0.12f,0.18f,0.28f);
        RenderSettings.fog=true;
        RenderSettings.fogColor=new Color(0.005f,0.016f,0.035f);
        RenderSettings.fogMode=FogMode.Linear;
        RenderSettings.fogStartDistance=24;
        RenderSettings.fogEndDistance=115;
        foreach(var root in roots)
        {
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))
            {
                Undo.RecordObject(camera,"Neon arena camera");
                camera.clearFlags=CameraClearFlags.Skybox; camera.allowHDR=true; camera.allowMSAA=true;
                var bloom=camera.GetComponent<NeonArenaBloom>();
                if(bloom==null)bloom=Undo.AddComponent<NeonArenaBloom>(camera.gameObject);
                bloom.Configure(Shader.Find("Hidden/Mocopi/Neon Arena Bloom"));
                EditorUtility.SetDirty(bloom);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            foreach(var light in root.GetComponentsInChildren<Light>(true))
                if(light.type==LightType.Directional)
                {
                    Undo.RecordObject(light,"Neon arena lighting");
                    light.color=new Color(0.72f,0.86f,1); light.intensity=0.8f;
                }
            foreach(var capture in root.GetComponentsInChildren<CaptureMaskDataMode>(true))
            {
                var settings=new SerializedObject(capture);
                settings.FindProperty("wallMaterial").objectReferenceValue=wall;
                settings.ApplyModifiedProperties();
            }
            // Keep the original gameplay ground collider, while showing the new runway surface.
            if(root.name=="Plane" && root.TryGetComponent<Renderer>(out var planeRenderer))
            {
                Undo.RecordObject(planeRenderer,"Neon arena ground"); planeRenderer.enabled=false;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void StyleWallPrefabs(Material wall)
    {
        foreach(string path in new[] {"Assets/prefab/wallbuilder.prefab","Assets/prefab/1P_Prefab/1P_WallBuilder.prefab","Assets/prefab/2P_Prefab/2P_WallBuilder.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var cell in root.GetComponentsInChildren<WallCube>(true))
                    if(cell.TryGetComponent<Renderer>(out var renderer))renderer.sharedMaterial=wall;
                foreach(var builder in root.GetComponentsInChildren<WallBuilder>(true))
                {
                    var settings=new SerializedObject(builder);
                    settings.FindProperty("cubeMaterial").objectReferenceValue=wall;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    [MenuItem("Tools/mocopi/Render Neon Arena Preview")]
    public static void RenderPreview()
    {
        Directory.CreateDirectory("Temp/NeonArenaPreview");
        var cameraObject=new GameObject("Neon Preview Camera") { hideFlags=HideFlags.HideAndDontSave };
        var camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false; camera.allowHDR=true; camera.allowMSAA=true;
        camera.clearFlags=CameraClearFlags.Skybox; camera.fieldOfView=65;
        camera.transform.SetPositionAndRotation(new Vector3(0,2,7),Quaternion.Euler(2,180,0));
        cameraObject.AddComponent<NeonArenaBloom>().Configure(Shader.Find("Hidden/Mocopi/Neon Arena Bloom"));
        RenderTexture previous=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Default,4);
        var display=RenderTexture.GetTemporary(1600,900,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        bool previousSRGBWrite=GL.sRGBWrite;
        Texture2D image=null;
        try
        {
            camera.targetTexture=target; camera.Render();
            // PNG stores display colors, while the HDR camera buffer contains linear light.
            GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Graphics.Blit(target,display);
            RenderTexture.active=display;
            image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            File.WriteAllBytes("Temp/NeonArenaPreview/stage.png",image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            GL.sRGBWrite=previousSRGBWrite;
            Object.DestroyImmediate(cameraObject);
            RenderTexture.ReleaseTemporary(target);
            RenderTexture.ReleaseTemporary(display);
            if(image!=null)Object.DestroyImmediate(image);
        }
        Validate();
        File.WriteAllText("Temp/NeonArenaPreview/render-validation.txt", "NEON_RENDER_PASSED: shaders and baked geometry validated.\n");
        Debug.Log("NEON_RENDER_PASSED: shaders and baked geometry validated.");
    }

    private static void Validate()
    {
        foreach(string name in new[] {"Mocopi/Neon Arena Surface","Mocopi/Neon Arena Sky","Hidden/Mocopi/Neon Arena Bloom"})
        {
            var shader=Shader.Find(name);
            if(ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException(string.Join("\n",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message)));
        }
        var stage=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(stage.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("Stage geometry must not affect game collisions.");
        foreach(var filter in stage.GetComponentsInChildren<MeshFilter>())
            if(filter.sharedMesh==null || filter.sharedMesh.vertexCount==0)throw new InvalidOperationException("Missing baked stage mesh.");
        foreach(var renderer in stage.GetComponentsInChildren<Renderer>())
            if(renderer.sharedMaterial==null)throw new InvalidOperationException("Missing stage material.");
    }
}
