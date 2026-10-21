using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Captures posed copies of the CaptureMode avatar, without changing the authored scene.</summary>
public static class RandomPoseCaptureBatch
{
    private const string Request = "Temp/RandomPoseCapture.request";
    private const string PreviewRequest = "Temp/RandomPoseCapture.previews";
    private const string ScenePath = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity";
    private const float Margin = 0.05f;
    private static Job job;

    [InitializeOnLoadMethod]
    private static void Watch()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (job == null)
            {
                if (File.Exists(PreviewRequest))
                {
                    File.Delete(PreviewRequest);
                    job = new Job(true);
                    return;
                }
                if (!File.Exists(Request)) return;
                File.Delete(Request);
                Run();
                return;
            }
            if (job.Step()) { job.Dispose(); job = null; }
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("output");
            File.WriteAllText("output/random-pose-capture-error.txt", error.ToString());
            Debug.LogException(error);
            job?.Dispose(); job = null;
        }
    }

    [MenuItem("Tools/mocopi/Capture 30 Random Poses")]
    public static void Run()
    {
        if (job != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        job = new Job();
    }

    [Serializable]
    private sealed class Report
    {
        public int requested = 30;
        public float marginMeters = Margin;
        public int rows, columns;
        public float cellSize;
        public string savedDirectory;
        public string status = "capturing";
        public List<Capture> captures = new List<Capture>();
    }

    [Serializable]
    private sealed class Capture
    {
        public string file, savedPath, image;
        public float horizontalOffset;
        public int originalHoleCells, expandedHoleCells;
        public string[] originalRows, expandedRows;
    }

    private sealed class Job : IDisposable
    {
        private readonly System.Random random = new System.Random(20261021);
        private readonly Scene originalScene;
        private Scene sourceScene, previewScene;
        private bool openedSource;
        private CaptureMaskDataMode mode;
        private Animator animator;
        private Transform projection;
        private Transform[] bones;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Camera camera;
        private readonly HashSet<string> shapes = new HashSet<string>();
        private readonly Report report = new Report();
        private readonly string output;
        private int attempts;
        private readonly bool previewOnly;
        private int previewIndex;

        public Job(bool previewOnly = false)
        {
            this.previewOnly = previewOnly;
            originalScene = SceneManager.GetActiveScene();
            output = previewOnly ? File.ReadAllText("output/random-pose-capture-latest.txt").Trim() :
                Path.GetFullPath("output/captured-random-poses-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            if (previewOnly) report = JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(output, "manifest.json")));
            try
            {
                sourceScene = SceneManager.GetSceneByPath(ScenePath);
                openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
                if (openedSource) sourceScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var source = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CaptureMaskDataMode>(true)).Single();
                var sourceAvatar = Get<MocopiAvatar>(source, "mocopiAvatar");
                previewScene = EditorSceneManager.NewPreviewScene();
                var avatar = Object.Instantiate(sourceAvatar.gameObject);
                SceneManager.MoveGameObjectToScene(avatar, previewScene);
                foreach (var behaviour in avatar.GetComponentsInChildren<Behaviour>()) behaviour.enabled = false;
                foreach (var t in avatar.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                animator = avatar.GetComponent<Animator>();
                if (animator == null || !animator.isHuman) throw new InvalidOperationException("Capture source must be Humanoid.");
                animator.enabled = true;
                animator.Rebind(); animator.Update(0);
                animator.enabled = false;
                bones = avatar.GetComponentsInChildren<Transform>(true);
                positions = bones.Select(t => t.localPosition).ToArray();
                rotations = bones.Select(t => t.localRotation).ToArray();

                var owner = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(owner, previewScene);
                mode = owner.GetComponent<CaptureMaskDataMode>();
                Set(mode, "mocopiAvatar", avatar.GetComponent<MocopiAvatar>());
                report.rows = Get<int>(mode, "rows"); report.columns = Get<int>(mode, "columns");
                report.cellSize = Get<float>(mode, "cellSize");
                report.savedDirectory = Application.persistentDataPath;
                projection = new GameObject("Batch capture projection").transform;
                SceneManager.MoveGameObjectToScene(projection.gameObject, previewScene);
                var sourceCamera = Get<Camera>(mode, "captureCamera");
                Vector3 forward = Vector3.ProjectOnPlane(sourceCamera.transform.forward, Vector3.up).normalized;
                projection.rotation = Quaternion.LookRotation(forward);
                Vector3 center = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                center.y = Get<float>(mode, "floorHeight") + report.rows * report.cellSize * 0.5f;
                projection.position = center + forward * Get<float>(mode, "forwardDistance");
                Set(mode, "blankWall", projection);

                camera = new GameObject("Batch thumbnail camera", typeof(Camera)).GetComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, previewScene);
                camera.enabled = false; camera.orthographic = true; camera.cullingMask = 1 << 31;
                camera.scene = previewScene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.07f, 0.12f);
                camera.nearClipPlane = 0.01f; camera.farClipPlane = 30;
                var light = new GameObject("Batch light", typeof(Light)).GetComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, previewScene);
                light.type = LightType.Directional; light.intensity = 1.2f; light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.LookRotation(forward + Vector3.down * 0.6f);
                File.WriteAllText("output/random-pose-capture-latest.txt", output);
                WriteReport();
            }
            catch { Dispose(); throw; }
        }

        public bool Step()
        {
            if (++attempts > 120) throw new InvalidOperationException("Could not generate 30 valid distinct captures.");
            for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; }
            Pose();
            Bounds bounds = ProjectedBounds();
            float width = report.columns * report.cellSize, height = report.rows * report.cellSize;
            if (bounds.size.x > width - 0.5f || bounds.size.y > height - 0.15f) return false;
            // Random horizontal placement inside the usable wall. Leave room for the later dilation.
            float x = Range(-width * 0.5f + bounds.extents.x + 0.25f, width * 0.5f - bounds.extents.x - 0.25f);
            animator.transform.position += projection.right * (x - bounds.center.x) +
                projection.up * (-height * 0.5f + 0.025f - bounds.min.y);
            Invoke(mode, "CapturePose");
            if (!mode.HasPreview) throw new InvalidOperationException("CaptureMode did not produce a preview.");
            var snapshot = Get<WallMaskData>(mode, "snapshot");
            var original = (bool[,])snapshot.GetMask().Clone();
            mode.SetOutlineMargin(Margin);
            var expanded = snapshot.GetMask();
            if (HoleCount(expanded) <= HoleCount(original)) throw new InvalidOperationException("Margin did not expand the hole.");
            for (int y = 0; y < report.rows; y++)
            for (int c = 0; c < report.columns; c++)
            {
                if (!original[y, c] && expanded[y, c]) throw new InvalidOperationException("Expansion removed part of the pose.");
                if ((y == 0 || c == 0 || c == report.columns - 1) && !expanded[y, c])
                    throw new InvalidOperationException("Pose was clipped at the top or sides.");
            }
            string key = string.Join("", Encode(expanded));
            if (!shapes.Add(key)) return false;
            if (previewOnly)
            {
                var capture = report.captures[previewIndex];
                if (key != string.Join("", capture.expandedRows))
                    throw new InvalidOperationException("Preview replay did not match the saved pose.");
                RenderThumbnail(Path.Combine(output, capture.image));
                return ++previewIndex == report.captures.Count;
            }
            int index = report.captures.Count + 1;
            string stem = index.ToString("00") + "_random_" + (x < -0.6f ? "left" : x > 0.6f ? "right" : "center");
            RenderThumbnail(Path.Combine(output, stem + ".png"));
            string previous = mode.LastSavedPath;
            mode.SaveCapture();
            if (string.IsNullOrEmpty(mode.LastSavedPath) || previous == mode.LastSavedPath ||
                !WallMaskData.TryReadSavedMask(mode.LastSavedPath, out var loaded) ||
                string.Join("", Encode(loaded)) != key) throw new IOException("Saved capture verification failed.");
            string file = stem + ".json";
            File.Copy(mode.LastSavedPath, Path.Combine(output, file), false);
            report.captures.Add(new Capture { file = file, savedPath = mode.LastSavedPath, image = stem + ".png",
                horizontalOffset = x, originalHoleCells = HoleCount(original), expandedHoleCells = HoleCount(expanded),
                originalRows = Encode(original), expandedRows = Encode(expanded) });
            if (report.captures.Count == report.requested) report.status = "complete";
            WriteReport();
            Debug.Log($"RANDOM_POSE_CAPTURE: {index}/30; x={x:F2}m; margin=5cm; {mode.LastSavedPath}");
            return report.status == "complete";
        }

        private void Pose()
        {
            var torso = animator.GetBoneTransform(HumanBodyBones.Spine);
            torso.rotation = Quaternion.AngleAxis(Range(-9, 9), projection.forward) * torso.rotation;
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            head.rotation = Quaternion.AngleAxis(Range(-10, 10), projection.forward) * head.rotation;
            // Upper arms vary from down-and-out to raised; elbows bend toward the upper body plane.
            Arm(true, Range(-55, 85), Range(0, 95));
            Arm(false, Range(-55, 85), Range(0, 95));
            Leg(true, Range(1, 18), random.NextDouble() < 0.25 ? Range(0.15f, 0.45f) : 0);
            Leg(false, Range(1, 18), random.NextDouble() < 0.25 ? Range(0.15f, 0.45f) : 0);
        }

        private void Arm(bool left, float angle, float bend)
        {
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            float sign = Mathf.Sign(Vector3.Dot(upper.position - animator.GetBoneTransform(HumanBodyBones.Hips).position, projection.right));
            Aim(upper, lower, Direction(sign, angle));
            Aim(lower, hand, Direction(sign, Mathf.Min(155, angle + bend)));
        }

        private Vector3 Direction(float sign, float degrees) => projection.right * (sign * Mathf.Cos(degrees * Mathf.Deg2Rad)) +
            Vector3.up * Mathf.Sin(degrees * Mathf.Deg2Rad) - projection.forward * 0.12f;

        private void Leg(bool left, float spread, float lift)
        {
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            float sign = Mathf.Sign(Vector3.Dot(upper.position - animator.GetBoneTransform(HumanBodyBones.Hips).position, projection.right));
            Vector3 side = projection.right * (sign * Mathf.Sin(spread * Mathf.Deg2Rad));
            Aim(upper, lower, side - Vector3.up * Mathf.Cos(spread * Mathf.Deg2Rad) - projection.forward * lift);
            Aim(lower, foot, side * 0.4f - Vector3.up + projection.forward * lift * 0.5f);
        }

        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
        }

        private Bounds ProjectedBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled || renderer.sharedMesh == null) continue;
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh, true);
                    Matrix4x4 matrix = projection.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 p = matrix.MultiplyPoint3x4(vertex);
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (first) throw new InvalidOperationException("No avatar mesh was found.");
            return bounds;
        }

        private void RenderThumbnail(string path)
        {
            Bounds b = ProjectedBounds();
            camera.transform.SetPositionAndRotation(projection.TransformPoint(b.center) - projection.forward * 5, projection.rotation);
            camera.orthographicSize = Mathf.Max(b.size.y * 0.57f, b.size.x * 0.72f);
            var target = RenderTexture.GetTemporary(256, 320, 24);
            var image = new Texture2D(256, 320, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            var bakedObjects = new List<GameObject>();
            var bakedMeshes = new List<Mesh>();
            var hiddenRenderers = new List<SkinnedMeshRenderer>();
            try
            {
                // Render the same CPU-baked geometry used by CaptureMode. GPU skinning in
                // edit-mode preview scenes can otherwise display the preceding pose.
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.enabled || renderer.sharedMesh == null) continue;
                    var mesh = new Mesh();
                    bakedMeshes.Add(mesh);
                    renderer.BakeMesh(mesh, true);
                    var baked = new GameObject("Captured mesh", typeof(MeshFilter), typeof(MeshRenderer));
                    bakedObjects.Add(baked);
                    baked.transform.SetParent(renderer.transform, false);
                    baked.layer = 31;
                    baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                    baked.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                    renderer.enabled = false;
                    hiddenRenderers.Add(renderer);
                }
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 320), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                foreach (var renderer in hiddenRenderers) renderer.enabled = true;
                foreach (var baked in bakedObjects) Object.DestroyImmediate(baked);
                foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh);
                camera.targetTexture = null; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private void WriteReport() => File.WriteAllText(Path.Combine(output, "manifest.json"), JsonUtility.ToJson(report, true));

        public void Dispose()
        {
            if (mode != null)
            {
                mode.RetryCapture();
                Set(mode, "blankWall", null); // The preview scene owns the temporary projection.
            }
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (openedSource && sourceScene.IsValid() && sourceScene.isLoaded) EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static int HoleCount(bool[,] mask) { int count = 0; foreach (bool solid in mask) if (!solid) count++; return count; }
    private static string[] Encode(bool[,] mask)
    {
        var rows = new string[mask.GetLength(0)];
        for (int y = 0; y < rows.Length; y++) { var row = new char[mask.GetLength(1)]; for (int x = 0; x < row.Length; x++) row[x] = mask[y, x] ? '1' : '0'; rows[y] = new string(row); }
        return rows;
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
