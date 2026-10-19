using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mocopi.Receiver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run with Unity -batchmode -executeMethod CaptureMaskDataValidation.RunAll.</summary>
public static class CaptureMaskDataValidation
{
    [MenuItem("Tools/mocopi/Validate Capture Masks (opens capture scene)")]
    public static void RunAll()
    {
        const string scenePath = "Assets/MocopiReceiver/Samples/ReceiverSample/Scenes/CaptureMaskDataMode.unity";
        EditorSceneManager.OpenScene(scenePath);
        var mode = UnityEngine.Object.FindAnyObjectByType<CaptureMaskDataMode>();
        Check(mode != null, "Capture mode is present in the scene.");
        var settings = new SerializedObject(mode);
        var avatar = settings.FindProperty("mocopiAvatar").objectReferenceValue as MocopiAvatar;
        var camera = settings.FindProperty("captureCamera").objectReferenceValue as Camera;
        Check(avatar != null && camera != null, "Capture references are wired.");
        Check(UnityEngine.Object.FindAnyObjectByType<WallManager>() == null, "Capture scene has no game wall spawner.");
        var receiver = UnityEngine.Object.FindAnyObjectByType<MocopiSimpleReceiver>();
        Check(receiver != null && receiver.AvatarSettings[0].MocopiAvatar == avatar &&
              receiver.AvatarSettings[0].Port == 12351, "Receiver routes port 12351 to the capture avatar.");
        Animator animator = avatar.GetComponent<Animator>();
        Check(animator.isHuman, "Capture avatar is Humanoid.");

        var projection = new GameObject("ValidationProjection");
        var wall = new GameObject("ValidationWall");
        Mesh mesh = null;
        try
        {
            projection.transform.SetPositionAndRotation(new Vector3(0, 1.3f, -3),
                Quaternion.LookRotation(Vector3.back));
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 originalHead = head.position;
            // An asymmetric pose isolates the head in the upper right of the wall.
            head.position = projection.transform.TransformPoint(new Vector3(0.7f, 0.9f, 3f));
            bool[,] pose = MaskPatternGenerator.GenerateHumanoidPoseHole(52, 42, animator,
                projection.transform, 2.1f, 2.6f, 0.04f, 0.04f, 0.08f, 0);
            Check(!pose[8, 34], "Raised head creates a hole in the upper right, not the bottom.");
            var data = wall.AddComponent<WallMaskData>();
            data.SetSavedMask((bool[,])pose.Clone());
            string before = data.ExportAsJSON();
            head.position = projection.transform.TransformPoint(new Vector3(-0.7f, -0.6f, 3f));
            bool[,] moved = MaskPatternGenerator.GenerateHumanoidPoseHole(52, 42, animator,
                projection.transform, 2.1f, 2.6f, 0.04f, 0.04f, 0.08f, 0);
            Check(moved[8, 34], "A subsequent live pose differs from the captured pose.");
            Check(before == data.ExportAsJSON(), "Moving the avatar does not change saved preview data.");
            head.position = originalHead;

            // Rows in JSON are top-down, while WallBuilder cube names count upward from the floor.
            bool[,] expected = { { false, true }, { true, false }, { false, false } };
            data.SetSavedMask(expected);
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 2; col++)
            {
                var cell = new GameObject($"Cube_{row}_{col}");
                cell.transform.SetParent(wall.transform, false);
                cell.AddComponent<WallCube>();
            }
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "CaptureMaskValidation");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "roundtrip.json");
            File.WriteAllText(path, data.ExportAsJSON());
            data.SetSavedMask(new bool[3, 2]);
            data.LoadSavedMaskData(path);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 2; x++)
            {
                Check(data.GetMask()[y, x] == expected[y, x], "JSON round-trip preserves every cell.");
                Check(wall.transform.Find($"Cube_{2 - y}_{x}").gameObject.activeSelf == expected[y, x],
                    "Loaded game wall matches top-down JSON orientation.");
            }
            Check(data.TryGetSavedMaskPreview(path, out var preview) && preview.HoleCount == 4,
                "Existing mask browser accepts the saved JSON.");
            mesh = CaptureMaskDataMode.BuildPreviewMesh(expected, 1f);
            Check(Mathf.Abs(mesh.bounds.min.y + 0.5f) < 0.001f &&
                  Mathf.Abs(mesh.bounds.max.y - 1.5f) < 0.001f,
                "Frozen preview mesh uses the same row orientation as the game wall.");

            MocopiConnectionMonitor.Clear(avatar);
            Check(!mode.IsConnected, "Offline status is reported without claiming a connection.");
            SetField(mode, "blankWall", projection.transform);
            Check(mode.CanCapture, "An offline avatar with a projection wall is ready to capture.");
            mode.RequestCapture();
            Check((bool)GetField(mode, "capturePending"), "Offline capture starts its countdown.");
            SetField(mode, "captureAt", float.PositiveInfinity);
            MocopiConnectionMonitor.RecordFrame(avatar, 12351, 1);
            Check(mode.IsConnected, "A recent mocopi frame is detected.");
            MocopiConnectionMonitor.Clear(avatar);
            Check(!mode.IsConnected, "Connection state updates after disconnect.");
            typeof(CaptureMaskDataMode).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(mode, null);
            Check((bool)GetField(mode, "capturePending"), "Disconnecting does not cancel the countdown.");
            SetField(mode, "captureAt", float.NegativeInfinity);
            typeof(CaptureMaskDataMode).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(mode, null);
            Check(mode.HasPreview, "Offline countdown completion creates a frozen mesh preview.");
            var offlineSnapshot = (WallMaskData)GetField(mode, "snapshot");
            SetField(offlineSnapshot, "maskSaveFileName", Path.Combine(directory, "offline.json"));
            mode.SaveCapture();
            Check(WallMaskData.TryReadSavedMask(mode.LastSavedPath, out var offlineMask), "Offline capture can be saved.");
            Equal(offlineSnapshot.GetMask(), offlineMask, "Offline saved shape matches the preview.");
            ValidateCellEditing(mode, offlineSnapshot);
            SetField(mode, "mocopiAvatar", null);
            Check(!mode.CanCapture, "A missing avatar is still rejected.");
            mode.RequestCapture();
            Check(!(bool)GetField(mode, "capturePending"), "Missing source cannot start capture.");
            SetField(mode, "mocopiAvatar", avatar);
            ValidateSilhouette(avatar.transform, projection.transform);
            ValidateSkinnedScale(projection.transform);
            ValidateLibrary(directory);
            Debug.Log("CAPTURE_MASK_VALIDATION_PASSED: scene, pose orientation, frozen snapshot, JSON round-trip, game wall mapping, preview mesh, offline capture/save, disconnect during countdown, missing source guard.");
        }
        finally
        {
            // Runtime previews use deferred destruction; dispose explicitly in this edit-mode validation.
            UnityEngine.Object.DestroyImmediate((Texture2D)GetField(mode, "editorTexture"));
            SetField(mode, "editorTexture", null);
            UnityEngine.Object.DestroyImmediate((Mesh)GetField(mode, "previewMesh"));
            UnityEngine.Object.DestroyImmediate((GameObject)GetField(mode, "previewWall"));
            SetField(mode, "previewMesh", null);
            SetField(mode, "previewWall", null);
            SetField(mode, "snapshot", null);
            SetField(mode, "blankWall", null);
            SetField(mode, "mocopiAvatar", avatar);
            SetField(mode, "capturePending", false);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(wall);
            UnityEngine.Object.DestroyImmediate(projection);
        }
    }

    private static void ValidateCellEditing(CaptureMaskDataMode mode, WallMaskData snapshot)
    {
        var history = new CaptureMaskEdits(3, 4);
        var outline = new bool[3, 4]; outline[0, 3] = true;
        history.BeginStroke();
        history.Paint(0, 1, true); history.Paint(1, 2, true); history.Paint(0, 3, false);
        history.EndStroke();
        Check(history.Count == 3 && history.Apply(outline)[0, 1] && !history.Apply(outline)[0, 3], "Stroke adds cubes and removes cubes.");
        Check(!outline[0, 1] && outline[0, 3], "Editing never modifies the source outline.");
        history.Undo(); Equal(outline, history.Apply(outline), "Undo restores the entire drag stroke.");
        history.Redo(); Check(history.Count == 3, "Redo restores the drag stroke.");
        history.Clear(); Equal(outline, history.Apply(outline), "Clear restores the slider outline.");
        history.Undo(); Check(history.Count == 3, "Clear itself can be undone.");
        history.Undo(); history.Paint(2, 0, true); history.EndStroke();
        Check(!history.CanRedo && history.Count == 1, "New brush stroke branches the undo history.");
        Check(!history.Paint(-1, 0, true) && !history.Paint(3, 0, true), "Outside cells are rejected.");

        Rect grid = new Rect(100, 80, 420, 520);
        Check(CaptureMaskDataMode.TryGridCell(grid, new Vector2(105, 85), 52, 42, out var first) && first == Vector2Int.zero, "Top-left screen cell maps to JSON row zero.");
        Check(CaptureMaskDataMode.TryGridCell(grid, new Vector2(515, 595), 52, 42, out var last) && last == new Vector2Int(41, 51), "Bottom-right screen cell maps correctly.");
        Check(!CaptureMaskDataMode.TryGridCell(grid, new Vector2(520, 100), 52, 42, out _), "Grid right boundary does not spill into a cell.");

        var frozen = (bool[,])snapshot.GetMask().Clone();
        int addRow = -1, addColumn = -1, removeRow = -1, removeColumn = -1;
        for (int y = 0; y < frozen.GetLength(0); y++)
        for (int x = 0; x < frozen.GetLength(1); x++)
            if (!frozen[y, x] && addRow < 0) { addRow = y; addColumn = x; }
            else if (frozen[y, x] && removeRow < 0) { removeRow = y; removeColumn = x; }
        Check(mode.SetPreviewCell(addRow, addColumn, true), "Cube can be placed in an empty hole.");
        Check(mode.SetPreviewCell(removeRow, removeColumn, false), "Cube can be removed from the wall.");
        Check(!mode.SetPreviewCell(-1, 0, true), "Invalid edits cannot modify the preview.");
        mode.SetOutlineMargin(0.1f);
        Check(snapshot.GetMask()[addRow, addColumn] && !snapshot.GetMask()[removeRow, removeColumn], "Slider changes retain explicit cube and hole edits.");
        mode.UndoEdit();
        Check(snapshot.GetMask()[addRow, addColumn] && (float)GetField(mode, "outlineMarginMeters") == 0.1f, "Undo affects manual edits without resetting the slider.");
        mode.RedoEdit();
        Check(!snapshot.GetMask()[removeRow, removeColumn], "Redo restores a removed cube after changing margin.");
        mode.SetOutlineMargin(0);
        string firstPath = mode.LastSavedPath;
        mode.SaveCapture();
        Check(mode.LastSavedPath != firstPath, "Editing a saved preview permits a new file.");
        Check(WallMaskData.TryReadSavedMask(mode.LastSavedPath, out var editedMask), "Edited mask saves in the existing JSON format.");
        Equal(snapshot.GetMask(), editedMask, "Saved edited mask matches the final preview exactly.");
        mode.SaveCapture(); Check(WallMaskData.TryReadSavedMask(firstPath, out var previousMask), "Earlier saved data still exists.");
        Equal(frozen, previousMask, "Editing never overwrites the previously saved capture.");

        var wall = ((GameObject)GetField(mode, "previewWall")).transform;
        float size = (float)GetField(mode, "cellSize");
        Vector3 hole = wall.TransformPoint(new Vector3((removeColumn + 0.5f - 21) * size, (26 - removeRow - 0.5f) * size, 0));
        Check(mode.TryPreviewCell(new Ray(hole - wall.forward * 2, wall.forward), out var selected) && selected == new Vector2Int(removeColumn, removeRow), "World click locates empty cells without a collider.");
        Check(!mode.TryPreviewCell(new Ray(hole - wall.forward * 2, -wall.forward), out _), "Rays pointing away from the preview are ignored.");
        var texture = (Texture2D)GetField(mode, "editorTexture");
        Check(texture.GetPixel(addColumn, 51 - addRow).r > 0.8f && texture.GetPixel(removeColumn, 51 - removeRow).r < 0.2f, "Editor texture matches top-down mask orientation.");
        mode.ClearManualEdits(); Equal(frozen, snapshot.GetMask(), "Clearing manual edits at zero margin restores the original capture.");
        mode.UndoEdit(); Equal(editedMask, snapshot.GetMask(), "Clearing edits is reversible in the real capture component.");
        Debug.Log("CAPTURE_CELL_EDITOR_VALIDATION_PASSED: add/remove, stroke undo/redo, reset undo, slider persistence, picking through holes, bounds, orientation, edited JSON, prior save preserved.");
    }

    private static void ValidateSilhouette(Transform avatar, Transform projection)
    {
        var frozen = PoseSilhouette.Capture(avatar, projection, 52, 42, 0.05f, 4);
        bool[,] precise = frozen.CreateMask(0f);
        int holes = CountHoles(precise);
        Check(holes > 0 && holes < precise.Length, "Real avatar mesh produces a nonempty bounded silhouette.");
        Vector3 original = avatar.position;
        avatar.position += Vector3.right * 5;
        bool[,] wider = frozen.CreateMask(0.1f);
        avatar.position = original;
        Check(CountHoles(wider) > holes, "Margin expands the captured mesh silhouette.");
        Equal(precise, frozen.CreateMask(0), "Returning the slider to zero restores the original capture.");
        for (int y = 0; y < 52; y++)
        for (int x = 0; x < 42; x++)
            Check(precise[y, x] || !wider[y, x], "Expansion never fills an original hole.");

        // Distinguish circular dilation from the square/box expansion that merges nearby contours.
        var point = new bool[9, 9]; point[4, 4] = true;
        var distance = new PoseSilhouette(point, 9, 9, 1, 0.05f);
        bool[,] expanded = distance.CreateMask(0.1f);
        Check(!expanded[4, 6] && expanded[6, 6], "Margin uses Euclidean distance, not square dilation.");

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            cube.transform.SetPositionAndRotation(projection.TransformPoint(new Vector3(0.7f, 0.9f, 0)), projection.rotation);
            cube.transform.localScale = Vector3.one * 0.12f;
            var sample = PoseSilhouette.Capture(cube.transform, projection, 52, 42, 0.05f, 4).CreateMask(0);
            Check(!sample[8, 34] && sample[43, 34], "Mesh capture preserves the upper-right position and scale.");
        }
        finally { UnityEngine.Object.DestroyImmediate(cube); }
    }

    private static void ValidateSkinnedScale(Transform projection)
    {
        // A known 0.5m x 1m skinned rectangle isolates scale from avatar geometry and sampling.
        var parent = new GameObject("ScaleValidationParent");
        var root = new GameObject("ScaleValidationAvatar");
        root.transform.SetParent(parent.transform, false);
        var bone = new GameObject("ScaleValidationBone");
        bone.transform.SetParent(root.transform, false);
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-0.25f, 0, 0), new Vector3(0.25f, 0, 0),
                new Vector3(0.25f, 1, 0), new Vector3(-0.25f, 1, 0) },
            triangles = new[] { 0, 1, 2, 0, 2, 3 },
            bindposes = new[] { bone.transform.worldToLocalMatrix * root.transform.localToWorldMatrix },
            boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 } }
        };
        var renderer = root.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.rootBone = bone.transform;
        renderer.bones = new[] { bone.transform };
        try
        {
            foreach (float parentScale in new[] { 1f, 1.25f })
            foreach (float avatarScale in new[] { 0.5f, 1f, 1.6f, 2f })
            {
                parent.transform.localScale = Vector3.one * parentScale;
                root.transform.localScale = Vector3.one * avatarScale;
                bool[,] mask = PoseSilhouette.Capture(root.transform, projection, 52, 42, 0.05f, 4).CreateMask(0);
                int minX = 42, maxX = -1, minY = 52, maxY = -1;
                for (int y = 0; y < 52; y++)
                for (int x = 0; x < 42; x++)
                {
                    if (mask[y, x]) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                float expectedHeight = parentScale * avatarScale;
                float width = (maxX - minX + 1) * 0.05f;
                float height = (maxY - minY + 1) * 0.05f;
                // Sampling can move either edge by one subcell; retaining intersected wall cells
                // can add up to one 5cm cell at each edge (e.g. 31.25cm spans eight centred cells).
                float widthError = width - expectedHeight * 0.5f;
                float heightError = height - expectedHeight;
                Check(widthError >= -0.0251f && widthError <= 0.1001f &&
                    heightError >= -0.0251f && heightError <= 0.1001f,
                    $"Skinned capture applies scale once: parent={parentScale}, avatar={avatarScale}, got={width}x{height}, expected={expectedHeight * 0.5f}x{expectedHeight}.");
            }
            Debug.Log("SKINNED_SCALE_VALIDATION_PASSED: root and parent scales preserve actual dimensions.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    private static void ValidateLibrary(string directory)
    {
        string folder = Path.Combine(directory, "library_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = new GameObject("LibraryValidation");
        var managerObject = new GameObject("ManagerValidation");
        var spawned = new List<GameObject>();
        try
        {
            var data = source.AddComponent<WallMaskData>();
            for (int i = 0; i < 20; i++)
            {
                var mask = new bool[5, 5];
                for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++) mask[y, x] = y * 5 + x != i;
                data.SetSavedMask(mask);
                File.WriteAllText(Path.Combine(folder, $"pose_{i:00}.json"), data.ExportAsJSON());
            }
            File.WriteAllText(Path.Combine(folder, "invalid.json"), "not json");
            File.WriteAllText(Path.Combine(folder, "bad-cell.json"), "{\"wallHeight\":1,\"wallWidth\":1,\"rows\":[\"x\"]}");
            data.SetSavedMask(new bool[5, 5]);
            File.WriteAllText(Path.Combine(folder, "empty.json"), data.ExportAsJSON());
            var library = SavedMaskLibrary.Load(folder, 5, 5);
            Check(library.Count == 20, "Load skips invalid and empty masks.");
            Check(SavedMaskLibrary.Load(folder, 52, 42).Count == 0, "Wrong grid dimensions are excluded.");
            for (int seed = 0; seed < 30; seed++)
            {
                var chosen = SavedMaskLibrary.SelectRound(library, 10, new System.Random(seed));
                Check(chosen.Count == 10 && new HashSet<SavedMaskLibrary.Entry>(chosen).Count == 10,
                    "Twenty saved masks yield ten different masks.");
            }
            var small = SavedMaskLibrary.SelectRound(library.GetRange(0, 3), 10, new System.Random(1));
            Check(small.Count == 10 && new HashSet<SavedMaskLibrary.Entry>(small.GetRange(0, 3)).Count == 3,
                "Small libraries exhaust every entry before repeating.");
            Check(SavedMaskLibrary.SelectRound(new List<SavedMaskLibrary.Entry>(), 10, new System.Random()).Count == 0,
                "An empty library terminates safely.");

            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                var cell = new GameObject($"Cube_{y}_{x}");
                cell.transform.SetParent(source.transform);
                cell.AddComponent<WallCube>();
            }
            var manager = managerObject.AddComponent<WallManager>();
            SetField(manager, "wallPrefab", source);
            SetField(manager, "useSavedMasks", true);
            SetField(manager, "savedMaskDirectory", folder);
            manager.BeginGame();
            Check(manager.UsesSavedMasks, "Game start loads the saved library.");
            var shapes = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                typeof(WallManager).GetMethod("SpawnWall", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                var active = (List<GameObject>)typeof(WallManager).GetField("activeWalls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                GameObject instance = active[active.Count - 1];
                spawned.Add(instance);
                var maskData = instance.GetComponent<WallMaskData>();
                Check(CountHoles(maskData.GetMask()) == 1, "Game applies the saved shape without further expansion.");
                shapes.Add(maskData.ExportAsJSON());
                for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    Check(instance.transform.Find($"Cube_{4 - y}_{x}").gameObject.activeSelf == maskData.GetMask()[y, x],
                        "Spawned wall cells match the selected mask.");
            }
            Check(shapes.Count == 10, "Ten actual spawned game walls have distinct saved shapes.");
            manager.ResetGame(false);
            manager.BeginGame();
            Check(manager.UsesSavedMasks && manager.GetSpawnedWallCount() == 0, "Reset prepares a fresh round.");

            SetField(data, "maskSaveFileName", Path.Combine(folder, "manual.json"));
            data.SaveCurrentMaskData(); string firstPath = data.GetMaskSavePath();
            data.SaveCurrentMaskData(); string secondPath = data.GetMaskSavePath();
            Check(firstPath != secondPath && File.Exists(firstPath) && File.Exists(secondPath),
                "Inspector saves add separate files instead of overwriting.");
            Debug.Log("PRECISE_CAPTURE_LIBRARY_VALIDATION_PASSED: mesh, margin reset, Euclidean dilation, filtering, 20-to-10 selection, spawned walls, reset, multiple saves.");
        }
        finally
        {
            foreach (var instance in spawned) UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static int CountHoles(bool[,] mask)
    {
        int count = 0;
        foreach (bool solid in mask) if (!solid) count++;
        return count;
    }

    private static void Equal(bool[,] expected, bool[,] actual, string message)
    {
        for (int y = 0; y < expected.GetLength(0); y++)
        for (int x = 0; x < expected.GetLength(1); x++) Check(expected[y, x] == actual[y, x], message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Capture mask validation failed: " + message);
    }
}
