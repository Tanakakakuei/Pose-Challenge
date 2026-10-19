using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;

/// <summary>
/// Generates wall masks. true means a solid cube, false means a cut-out hole.
/// </summary>
public static class MaskPatternGenerator
{
    private static readonly Dictionary<char, string[]> AlphabetPatterns = new Dictionary<char, string[]>
    {
        { 'A', new[] { "0011100", "0100010", "1000001", "1000001", "1111111", "1000001", "1000001", "1000001", "1000001" } },
        { 'B', new[] { "1111100", "1000010", "1000010", "1000010", "1111100", "1000010", "1000010", "1000010", "1111100" } },
        { 'C', new[] { "0011110", "0100001", "1000000", "1000000", "1000000", "1000000", "1000000", "0100001", "0011110" } },
        { 'D', new[] { "1111100", "1000010", "1000001", "1000001", "1000001", "1000001", "1000001", "1000010", "1111100" } },
        { 'E', new[] { "1111111", "1000000", "1000000", "1000000", "1111100", "1000000", "1000000", "1000000", "1111111" } },
        { 'F', new[] { "1111111", "1000000", "1000000", "1000000", "1111100", "1000000", "1000000", "1000000", "1000000" } },
        { 'G', new[] { "0011110", "0100001", "1000000", "1000000", "1001111", "1000001", "1000001", "0100001", "0011110" } },
        { 'H', new[] { "1000001", "1000001", "1000001", "1000001", "1111111", "1000001", "1000001", "1000001", "1000001" } },
        { 'I', new[] { "1111111", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000", "1111111" } },
        { 'J', new[] { "0011111", "0000010", "0000010", "0000010", "0000010", "0000010", "1000010", "1000010", "0111100" } },
        { 'K', new[] { "1000001", "1000010", "1000100", "1001000", "1110000", "1001000", "1000100", "1000010", "1000001" } },
        { 'L', new[] { "1000000", "1000000", "1000000", "1000000", "1000000", "1000000", "1000000", "1000000", "1111111" } },
        { 'M', new[] { "1000001", "1100011", "1010101", "1001001", "1000001", "1000001", "1000001", "1000001", "1000001" } },
        { 'N', new[] { "1000001", "1100001", "1010001", "1001001", "1000101", "1000011", "1000001", "1000001", "1000001" } },
        { 'O', new[] { "0011100", "0100010", "1000001", "1000001", "1000001", "1000001", "1000001", "0100010", "0011100" } },
        { 'P', new[] { "1111100", "1000010", "1000010", "1000010", "1111100", "1000000", "1000000", "1000000", "1000000" } },
        { 'Q', new[] { "0011100", "0100010", "1000001", "1000001", "1000001", "1001001", "1000101", "0100010", "0011101" } },
        { 'R', new[] { "1111100", "1000010", "1000010", "1000010", "1111100", "1001000", "1000100", "1000010", "1000001" } },
        { 'S', new[] { "0111110", "1000001", "1000000", "1000000", "0111100", "0000010", "0000001", "1000001", "0111110" } },
        { 'T', new[] { "1111111", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000" } },
        { 'U', new[] { "1000001", "1000001", "1000001", "1000001", "1000001", "1000001", "1000001", "0100010", "0011100" } },
        { 'V', new[] { "1000001", "1000001", "1000001", "1000001", "0100010", "0100010", "0010100", "0010100", "0001000" } },
        { 'W', new[] { "1000001", "1000001", "1000001", "1000001", "1001001", "1001001", "1010101", "1100011", "1000001" } },
        { 'X', new[] { "1000001", "0100010", "0010100", "0001000", "0001000", "0010100", "0100010", "1000001", "1000001" } },
        { 'Y', new[] { "1000001", "0100010", "0010100", "0001000", "0001000", "0001000", "0001000", "0001000", "0001000" } },
        { 'Z', new[] { "1111111", "0000001", "0000010", "0000100", "0001000", "0010000", "0100000", "1000000", "1111111" } },
    };

    public static bool[,] GenerateAlphabetHole(int height, int width, char letter, int strokePadding = 1, int holeExpansionCells = 0)
    {
        bool[,] mask = CreateFilledMask(height, width);
        char key = char.ToUpperInvariant(letter);
        holeExpansionCells = Mathf.Max(0, holeExpansionCells);

        if (!AlphabetPatterns.TryGetValue(key, out string[] pattern))
        {
            Debug.LogWarning($"Unsupported letter '{letter}'. Falling back to A.");
            pattern = AlphabetPatterns['A'];
        }

        int patternHeight = pattern.Length;
        int patternWidth = pattern[0].Length;
        int edgePadding = strokePadding + holeExpansionCells;
        int cellScale = Mathf.Max(1, Mathf.Min(
            (height - edgePadding * 2) / patternHeight,
            (width - edgePadding * 2) / patternWidth));

        int scaledHeight = patternHeight * cellScale;
        int scaledWidth = patternWidth * cellScale;
        int startY = Mathf.Max(0, (height - scaledHeight) / 2);
        int startX = Mathf.Max(0, (width - scaledWidth) / 2);

        for (int py = 0; py < patternHeight; py++)
        {
            for (int px = 0; px < patternWidth; px++)
            {
                if (pattern[py][px] != '1')
                {
                    continue;
                }

                for (int sy = 0; sy < cellScale; sy++)
                {
                    for (int sx = 0; sx < cellScale; sx++)
                    {
                        int y = startY + py * cellScale + sy;
                        int x = startX + px * cellScale + sx;

                        CutExpandedHole(mask, y, x, holeExpansionCells);
                    }
                }
            }
        }

        return mask;
    }

    public static bool[,] GenerateTextHole(
        int height,
        int width,
        char letter,
        Text sourceText,
        int textureSize = 512,
        float paddingRatio = 0.08f,
        float alphaThreshold = 0.1f,
        int holeExpansionCells = 0)
    {
        if (sourceText == null || sourceText.font == null)
        {
            return GenerateAlphabetHole(height, width, letter, 1, holeExpansionCells);
        }

        textureSize = Mathf.Max(64, textureSize);
        paddingRatio = Mathf.Clamp01(paddingRatio);
        alphaThreshold = Mathf.Clamp01(alphaThreshold);
        holeExpansionCells = Mathf.Max(0, holeExpansionCells);

        bool[,] mask = CreateFilledMask(height, width);
        Texture2D texture = RenderTextToTexture(letter, sourceText, textureSize, paddingRatio);

        if (texture == null)
        {
            return GenerateAlphabetHole(height, width, letter, 1, holeExpansionCells);
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (SampleCellAlpha(texture, height, width, y, x) >= alphaThreshold)
                {
                    CutExpandedHole(mask, y, x, holeExpansionCells);
                }
            }
        }

        Object.DestroyImmediate(texture);
        return mask;
    }

    public static bool[,] GenerateHumanoidPoseHole(
        int height,
        int width,
        Animator animator,
        Transform projectionTransform,
        float projectionWidth,
        float projectionHeight,
        float bodyRadius = 0.12f,
        float handFootRadius = 0.09f,
        float headRadius = 0.16f,
        int holeExpansionCells = 0)
    {
        bool[,] mask = CreateFilledMask(height, width);
        if (animator == null || !animator.isHuman || projectionTransform == null)
        {
            return mask;
        }

        projectionWidth = Mathf.Max(0.001f, projectionWidth);
        projectionHeight = Mathf.Max(0.001f, projectionHeight);
        bodyRadius = Mathf.Max(0.001f, bodyRadius);
        handFootRadius = Mathf.Max(0.001f, handFootRadius);
        headRadius = Mathf.Max(0.001f, headRadius);
        holeExpansionCells = Mathf.Max(0, holeExpansionCells);

        float cellSize = Mathf.Max(0.001f, Mathf.Min(projectionWidth / width, projectionHeight / height));
        int bodyRadiusCells = Mathf.Max(1, Mathf.CeilToInt(bodyRadius / cellSize)) + holeExpansionCells;
        int handFootRadiusCells = Mathf.Max(1, Mathf.CeilToInt(handFootRadius / cellSize)) + holeExpansionCells;
        int headRadiusCells = Mathf.Max(1, Mathf.CeilToInt(headRadius / cellSize)) + holeExpansionCells;

        // Chest, UpperChest and Neck are optional Humanoid bones. Bridge absent bones
        // so the torso does not acquire solid gaps in the captured silhouette.
        HumanBodyBones previous = HumanBodyBones.Hips;
        foreach (HumanBodyBones next in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head })
        {
            if (animator.GetBoneTransform(next) == null) continue;
            CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, previous, next, bodyRadiusCells);
            previous = next;
        }
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, bodyRadiusCells);

        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, bodyRadiusCells);

        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, bodyRadiusCells);
        CutBoneCapsule(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, bodyRadiusCells);

        CutBoneCircle(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.Head, headRadiusCells);
        CutBoneCircle(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftHand, handFootRadiusCells);
        CutBoneCircle(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightHand, handFootRadiusCells);
        CutBoneCircle(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.LeftFoot, handFootRadiusCells);
        CutBoneCircle(mask, animator, projectionTransform, projectionWidth, projectionHeight, HumanBodyBones.RightFoot, handFootRadiusCells);

        return mask;
    }

    public static bool[,] GenerateCenterHole(int height, int width, int holeHeight, int holeWidth)
    {
        bool[,] mask = CreateFilledMask(height, width);
        int startY = (height - holeHeight) / 2;
        int startX = (width - holeWidth) / 2;

        CutRectangle(mask, startY, startX, holeHeight, holeWidth);
        return mask;
    }

    public static bool[,] GenerateRandomPattern(int height, int width, float fillRatio = 0.7f)
    {
        bool[,] mask = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[y, x] = Random.value < fillRatio;
            }
        }

        return mask;
    }

    public static bool[,] GenerateMultipleHoles(int height, int width, List<Vector4> holes)
    {
        bool[,] mask = CreateFilledMask(height, width);

        foreach (Vector4 hole in holes)
        {
            int startY = (int)hole.x;
            int startX = (int)hole.y;
            int holeH = (int)hole.z;
            int holeW = (int)hole.w;

            CutRectangle(mask, startY, startX, holeH, holeW);
        }

        return mask;
    }

    public static bool[,] GenerateProgressivePattern(int height, int width, int difficulty)
    {
        bool[,] mask = CreateFilledMask(height, width);
        float fillRatio = Mathf.Clamp01(1f - (difficulty * 0.1f));
        int holeWidth = Mathf.Max(2, width - difficulty * 2);
        int startX = (width - holeWidth) / 2;

        for (int y = 0; y < height; y++)
        {
            if (Random.value > fillRatio)
            {
                for (int x = startX; x < startX + holeWidth && x < width; x++)
                {
                    if (x >= 0)
                    {
                        mask[y, x] = false;
                    }
                }
            }
        }

        return mask;
    }

    public static char GetRandomUppercaseLetter()
    {
        return (char)('A' + Random.Range(0, 26));
    }

    public static void DebugPrintMask(bool[,] mask)
    {
        StringBuilder output = new StringBuilder();
        for (int y = 0; y < mask.GetLength(0); y++)
        {
            for (int x = 0; x < mask.GetLength(1); x++)
            {
                output.Append(mask[y, x] ? '1' : '0');
            }
            output.AppendLine();
        }

        Debug.Log("Mask Pattern:\n" + output);
    }

    private static bool[,] CreateFilledMask(int height, int width)
    {
        bool[,] mask = new bool[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[y, x] = true;
            }
        }

        return mask;
    }

    private static Texture2D RenderTextToTexture(char letter, Text sourceText, int textureSize, float paddingRatio)
    {
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;
        Camera textCamera = null;
        GameObject root = null;

        try
        {
            renderTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            root = new GameObject("GeneratedTextMask");
            root.hideFlags = HideFlags.HideAndDontSave;

            GameObject cameraObject = new GameObject("GeneratedTextMaskCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(root.transform, false);
            textCamera = cameraObject.AddComponent<Camera>();
            textCamera.clearFlags = CameraClearFlags.SolidColor;
            textCamera.backgroundColor = Color.clear;
            textCamera.orthographic = true;
            textCamera.orthographicSize = textureSize * 0.5f;
            textCamera.nearClipPlane = -10f;
            textCamera.farClipPlane = 10f;
            textCamera.targetTexture = renderTexture;

            GameObject canvasObject = new GameObject("GeneratedTextMaskCanvas");
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = textCamera;
            canvas.pixelPerfect = true;
            canvas.planeDistance = 1f;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(textureSize, textureSize);

            GameObject textObject = new GameObject("GeneratedTextMaskText");
            textObject.hideFlags = HideFlags.HideAndDontSave;
            textObject.transform.SetParent(canvasObject.transform, false);

            Text text = textObject.AddComponent<Text>();
            CopyTextSettings(sourceText, text);
            text.text = char.ToUpperInvariant(letter).ToString();
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            RectTransform textRect = text.GetComponent<RectTransform>();
            float padding = textureSize * paddingRatio;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(padding, padding);
            textRect.offsetMax = new Vector2(-padding, -padding);

            Canvas.ForceUpdateCanvases();
            textCamera.Render();

            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            texture.Apply();
            return texture;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (textCamera != null)
            {
                textCamera.targetTexture = null;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
            }

            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void CopyTextSettings(Text source, Text destination)
    {
        destination.font = source.font;
        destination.fontStyle = source.fontStyle;
        destination.fontSize = source.fontSize;
        destination.resizeTextForBestFit = true;
        destination.resizeTextMinSize = Mathf.Max(1, source.resizeTextMinSize);
        destination.resizeTextMaxSize = Mathf.Max(destination.resizeTextMinSize, source.resizeTextMaxSize);
        destination.horizontalOverflow = HorizontalWrapMode.Overflow;
        destination.verticalOverflow = VerticalWrapMode.Overflow;
        destination.supportRichText = false;
        destination.material = source.material;
    }

    private static float SampleCellAlpha(Texture2D texture, int maskHeight, int maskWidth, int y, int x)
    {
        int textureHeight = texture.height;
        int textureWidth = texture.width;
        int minY = Mathf.FloorToInt(y * textureHeight / (float)maskHeight);
        int maxY = Mathf.CeilToInt((y + 1) * textureHeight / (float)maskHeight);
        int minX = Mathf.FloorToInt(x * textureWidth / (float)maskWidth);
        int maxX = Mathf.CeilToInt((x + 1) * textureWidth / (float)maskWidth);
        float maxAlpha = 0f;

        for (int pixelY = minY; pixelY < maxY; pixelY++)
        {
            for (int pixelX = minX; pixelX < maxX; pixelX++)
            {
                Color pixel = texture.GetPixel(pixelX, pixelY);
                maxAlpha = Mathf.Max(maxAlpha, pixel.a);
            }
        }

        return maxAlpha;
    }

    private static void CutExpandedHole(bool[,] mask, int centerY, int centerX, int expansionCells)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);

        for (int y = centerY - expansionCells; y <= centerY + expansionCells; y++)
        {
            for (int x = centerX - expansionCells; x <= centerX + expansionCells; x++)
            {
                if (y >= 0 && y < height && x >= 0 && x < width)
                {
                    mask[y, x] = false;
                }
            }
        }
    }

    private static void CutBoneCapsule(
        bool[,] mask,
        Animator animator,
        Transform projectionTransform,
        float projectionWidth,
        float projectionHeight,
        HumanBodyBones startBone,
        HumanBodyBones endBone,
        int radiusCells)
    {
        Transform start = animator.GetBoneTransform(startBone);
        Transform end = animator.GetBoneTransform(endBone);
        if (start == null || end == null)
        {
            return;
        }

        if (!TryProjectToMask(start.position, projectionTransform, projectionWidth, projectionHeight, mask, out Vector2 startCell) ||
            !TryProjectToMask(end.position, projectionTransform, projectionWidth, projectionHeight, mask, out Vector2 endCell))
        {
            return;
        }

        CutCapsule(mask, startCell, endCell, radiusCells);
    }

    private static void CutBoneCircle(
        bool[,] mask,
        Animator animator,
        Transform projectionTransform,
        float projectionWidth,
        float projectionHeight,
        HumanBodyBones bone,
        int radiusCells)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            return;
        }

        if (TryProjectToMask(boneTransform.position, projectionTransform, projectionWidth, projectionHeight, mask, out Vector2 cell))
        {
            CutCircle(mask, Mathf.RoundToInt(cell.y), Mathf.RoundToInt(cell.x), radiusCells);
        }
    }

    private static bool TryProjectToMask(
        Vector3 worldPosition,
        Transform projectionTransform,
        float projectionWidth,
        float projectionHeight,
        bool[,] mask,
        out Vector2 cell)
    {
        Vector3 local = projectionTransform.InverseTransformPoint(worldPosition);
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        float normalizedX = (local.x / projectionWidth) + 0.5f;
        float normalizedY = (local.y / projectionHeight) + 0.5f;

        // Stored rows are top-down; WallBuilder names cube rows from the bottom.
        cell = new Vector2(
            normalizedX * (width - 1),
            (1f - normalizedY) * (height - 1));

        return normalizedX >= -0.25f && normalizedX <= 1.25f && normalizedY >= -0.25f && normalizedY <= 1.25f;
    }

    private static void CutCapsule(bool[,] mask, Vector2 start, Vector2 end, int radiusCells)
    {
        Vector2 delta = end - start;
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)steps);
            CutCircle(mask, Mathf.RoundToInt(point.y), Mathf.RoundToInt(point.x), radiusCells);
        }
    }

    private static void CutCircle(bool[,] mask, int centerY, int centerX, int radiusCells)
    {
        int height = mask.GetLength(0);
        int width = mask.GetLength(1);
        int radiusSquared = radiusCells * radiusCells;

        for (int y = centerY - radiusCells; y <= centerY + radiusCells; y++)
        {
            for (int x = centerX - radiusCells; x <= centerX + radiusCells; x++)
            {
                int dy = y - centerY;
                int dx = x - centerX;
                if (dy * dy + dx * dx <= radiusSquared && y >= 0 && y < height && x >= 0 && x < width)
                {
                    mask[y, x] = false;
                }
            }
        }
    }

    private static void CutRectangle(bool[,] mask, int startY, int startX, int height, int width)
    {
        int maskHeight = mask.GetLength(0);
        int maskWidth = mask.GetLength(1);

        for (int y = startY; y < startY + height && y < maskHeight; y++)
        {
            for (int x = startX; x < startX + width && x < maskWidth; x++)
            {
                if (y >= 0 && x >= 0)
                {
                    mask[y, x] = false;
                }
            }
        }
    }
}
