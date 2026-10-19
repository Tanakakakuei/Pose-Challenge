using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class TouchableUIPanel : MonoBehaviour
{
    private const string PlayerTag = "Player";
    private const string CanvasName = "PanelCanvas";
    private const string BackgroundName = "Background";
    private const string PanelOutlineName = "PanelOutline";
    private const string LabelName = "Label";

    [Header("Content")]
    [SerializeField] private string label = "Touch";

    [Header("Font Style")]
    [SerializeField] private Font font;
    [SerializeField, Min(1)] private int fontSize = 48;
    [SerializeField] private Color fontColor = Color.white;
    [SerializeField] private Color fontOutlineColor = new Color(0.1f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 2f)] private float fontOutlineBrightness = 1f;
    [SerializeField, Range(0f, 1f)] private float fontOutlineAlpha = 1f;
    [SerializeField, Range(0f, 32f)] private float fontOutlineThicknessPixels = 6f;

    [Header("Panel Size")]
    [SerializeField] private Vector2 sizeMeters = new Vector2(0.45f, 0.18f);
    [SerializeField, Min(0.01f)] private float triggerDepthMeters = 0.08f;
    [SerializeField, Min(1f)] private float pixelsPerMeter = 500f;

    [Header("Panel Style")]
    [SerializeField] private Color normalColor = new Color(0.08f, 0.10f, 0.13f, 0.85f);
    [SerializeField] private Color touchedColor = new Color(0.18f, 0.45f, 0.95f, 0.95f);
    [SerializeField] private bool showPanelBackground = true;
    [SerializeField, Range(0f, 1f)] private float panelOpacity = 0.85f;
    [SerializeField, Range(0f, 1f)] private float touchedPanelOpacity = 0.95f;
    [SerializeField] private bool showPanelOutline = true;
    [SerializeField] private Color panelOutlineColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField, Range(0f, 2f)] private float panelOutlineBrightness = 1f;
    [SerializeField, Range(0f, 1f)] private float panelOutlineAlpha = 1f;
    [SerializeField, Range(0f, 32f)] private float panelOutlineThicknessPixels = 4f;
    [SerializeField, Range(0.8f, 1.2f)] private float touchedScale = 1.04f;

    [Header("Touch Filtering")]
    [SerializeField] private bool handsOnly = true;
    [SerializeField, Min(0f)] private float holdSeconds = 0.22f;
    [SerializeField, Min(0f)] private float activationDelay = 0.45f;
    [SerializeField] private bool acceptPlayerLayer = true;
    [SerializeField] private bool acceptPlayerTag = true;
    [SerializeField] private bool acceptMocopiRig = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onTouchEnter = new UnityEvent();
    [SerializeField] private UnityEvent onTouchExit = new UnityEvent();
    [SerializeField] private UnityEvent onTouchClick = new UnityEvent();

    private readonly HashSet<Collider> touchingColliders = new HashSet<Collider>();
    private BoxCollider boxCollider;
    private Rigidbody panelRigidbody;
    private RectTransform canvasRect;
    private Image backgroundImage;
    private Image[] panelOutlineImages = new Image[0];
    private Text labelText;
    private Outline labelOutline;
    private float heldTime, enabledAt, visualAmount;
    private bool activated, pointerHover, refreshPending, waitForRelease;
    private Image holdProgress;
    private int playerLayer = -1;

    public bool IsTouched => touchingColliders.Count > 0;
    public string Label => label;
    public UnityEvent OnClick => onTouchClick;
    public float HoldProgress => holdSeconds <= 0 ? 1 : Mathf.Clamp01(heldTime / holdSeconds);

    private void Awake()
    {
        CacheComponents();
        ApplyAll();
    }

    private void OnEnable()
    {
        CacheComponents();
        playerLayer = LayerMask.NameToLayer(PlayerTag);
        ClearTouch();
        enabledAt = Time.unscaledTime;
        ApplyAll();
    }

    private void OnValidate()
    {
        // OnValidate can run during deserialization: create UI only on the main update.
        refreshPending = true;
    }

    private void Reset()
    {
        CacheComponents();
        ApplyAll();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying || !IsAcceptedTouch(other))
        {
            return;
        }

        bool wasTouched = IsTouched;
        touchingColliders.Add(other);
        // A hand already inside when a new menu opens must leave before it can confirm it.
        if (Time.unscaledTime - enabledAt < activationDelay) waitForRelease = true;

        if (!wasTouched)
        {
            heldTime = 0;
            activated = false;
            onTouchEnter.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!Application.isPlaying || !touchingColliders.Remove(other))
        {
            return;
        }

        if (!IsTouched)
        {
            heldTime = 0;
            activated = false;
            waitForRelease = false;
            onTouchExit.Invoke();
        }
    }

    private void OnDisable() => ClearTouch();

    private void ClearTouch()
    {
        touchingColliders.Clear();
        heldTime = visualAmount = 0;
        activated = pointerHover = waitForRelease = false;
        if (canvasRect != null) canvasRect.localScale = Vector3.one / pixelsPerMeter;
        if (holdProgress != null) holdProgress.rectTransform.anchorMax = new Vector2(0, 0);
        if (backgroundImage != null) backgroundImage.color = GetPanelColor(false);
    }

    private void Update()
    {
        if (refreshPending) { refreshPending = false; CacheComponents(); ApplyAll(); }
        if (!Application.isPlaying) return;
        touchingColliders.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        UpdatePointer();
        if (Time.unscaledTime - enabledAt >= activationDelay && IsTouched && !activated && !waitForRelease)
        {
            heldTime += Time.unscaledDeltaTime;
            if (heldTime >= holdSeconds) Activate();
        }
        if (!IsTouched) { heldTime = 0; activated = waitForRelease = false; }
        if (!isActiveAndEnabled) return; // Click callbacks may hide this state panel.
        float target = IsTouched || pointerHover ? 1 : 0;
        visualAmount = Mathf.Lerp(visualAmount, target, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
        if (canvasRect != null) canvasRect.localScale = Vector3.one * Mathf.Lerp(1, touchedScale, visualAmount) / pixelsPerMeter;
        if (backgroundImage != null) backgroundImage.color = Color.Lerp(GetPanelColor(false), GetPanelColor(true), visualAmount);
        if (holdProgress != null) holdProgress.rectTransform.anchorMax = new Vector2(IsTouched ? HoldProgress : 0, 0);
    }

    public void Activate()
    {
        if (!isActiveAndEnabled || activated || Time.unscaledTime - enabledAt < activationDelay) return;
        activated = true;
        onTouchClick.Invoke();
    }

    private void UpdatePointer()
    {
        pointerHover = false;
        Vector2 position;
        bool clicked;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        position = mouse.position.ReadValue();
        clicked = mouse.leftButton.wasPressedThisFrame;
#else
        position = Input.mousePosition;
        clicked = Input.GetMouseButtonDown(0);
#endif
        foreach (var camera in Camera.allCameras)
        {
            if (!camera.pixelRect.Contains(position) || (camera.cullingMask & (1 << gameObject.layer)) == 0) continue;
            Ray ray = camera.ScreenPointToRay(position);
            if (!boxCollider.Raycast(ray, out var hit, 20)) continue;
            bool occluded = false;
            foreach (var nearer in Physics.RaycastAll(ray, hit.distance, camera.cullingMask, QueryTriggerInteraction.Collide))
                if (nearer.collider != boxCollider && nearer.collider.GetComponent<TouchableUIPanel>() != null) occluded = true;
            if (occluded) continue;
            pointerHover = true;
            if (clicked) Activate();
            break;
        }
    }

    public void SetLabel(string value)
    {
        label = value;
        ApplyAll();
    }

    public void SetFont(Font value) { font = value; ApplyAll(); }

    public void ConfigureVisuals(
        Vector2 panelSizeMeters,
        float triggerDepth,
        float panelPixelsPerMeter,
        Font labelFont,
        int labelFontSize,
        Color labelFontColor,
        Color panelColor,
        Color panelTouchedColor,
        bool displayPanelBackground,
        float normalPanelOpacity,
        float touchedPanelOpacityValue,
        bool displayPanelOutline,
        Color rectangleOutlineColor,
        float rectangleOutlineBrightness,
        float rectangleOutlineAlpha,
        float rectangleOutlineThicknessPixels,
        Color textOutlineColor,
        float textOutlineBrightness,
        float textOutlineAlpha,
        float textOutlineThicknessPixels)
    {
        sizeMeters = panelSizeMeters;
        triggerDepthMeters = triggerDepth;
        pixelsPerMeter = panelPixelsPerMeter;
        font = labelFont;
        fontSize = labelFontSize;
        fontColor = labelFontColor;
        normalColor = panelColor;
        touchedColor = panelTouchedColor;
        showPanelBackground = displayPanelBackground;
        panelOpacity = normalPanelOpacity;
        touchedPanelOpacity = touchedPanelOpacityValue;
        showPanelOutline = displayPanelOutline;
        this.panelOutlineColor = rectangleOutlineColor;
        panelOutlineBrightness = rectangleOutlineBrightness;
        panelOutlineAlpha = rectangleOutlineAlpha;
        panelOutlineThicknessPixels = rectangleOutlineThicknessPixels;
        fontOutlineColor = textOutlineColor;
        fontOutlineBrightness = textOutlineBrightness;
        fontOutlineAlpha = textOutlineAlpha;
        fontOutlineThicknessPixels = textOutlineThicknessPixels;
        ApplyAll();
    }

    private void CacheComponents()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (panelRigidbody == null)
        {
            panelRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void ApplyAll()
    {
        if (boxCollider == null || panelRigidbody == null)
        {
            return;
        }

        sizeMeters.x = Mathf.Max(0.01f, sizeMeters.x);
        sizeMeters.y = Mathf.Max(0.01f, sizeMeters.y);
        triggerDepthMeters = Mathf.Max(0.01f, triggerDepthMeters);
        pixelsPerMeter = Mathf.Max(1f, pixelsPerMeter);
        fontSize = Mathf.Max(1, fontSize);

        panelRigidbody.isKinematic = true;
        panelRigidbody.useGravity = false;
        boxCollider.isTrigger = true;
        boxCollider.size = new Vector3(sizeMeters.x, sizeMeters.y, triggerDepthMeters);
        boxCollider.center = Vector3.zero;

        EnsureVisualHierarchy();
        ApplyVisualStyle();
        PlayerArenaLayers.Inherit(canvasRect.gameObject, gameObject);
    }

    private void EnsureVisualHierarchy()
    {
        canvasRect = FindDirectChild(CanvasName) as RectTransform;
        if (canvasRect == null)
        {
            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        }

        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one / pixelsPerMeter;
        canvasRect.sizeDelta = sizeMeters * pixelsPerMeter;

        RectTransform backgroundRect = FindDirectChild(BackgroundName, canvasRect) as RectTransform;
        if (backgroundRect == null)
        {
            GameObject backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.transform.SetParent(canvasRect, false);
            backgroundRect = backgroundObject.GetComponent<RectTransform>();
        }

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundImage = backgroundRect.GetComponent<Image>();

        EnsurePanelOutline(backgroundRect);

        RectTransform labelRect = FindDirectChild(LabelName, backgroundRect) as RectTransform;
        if (labelRect == null)
        {
            GameObject labelObject = new GameObject(LabelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            labelObject.transform.SetParent(backgroundRect, false);
            labelRect = labelObject.GetComponent<RectTransform>();
        }

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.SetAsLastSibling();
        labelText = labelRect.GetComponent<Text>();
        labelOutline = labelRect.GetComponent<Outline>();
        var progressRect = FindDirectChild("HoldProgress", backgroundRect) as RectTransform;
        if (progressRect == null)
        {
            var progress = new GameObject("HoldProgress", typeof(RectTransform), typeof(Image));
            progress.transform.SetParent(backgroundRect, false);
            progressRect = (RectTransform)progress.transform;
        }
        progressRect.anchorMin = progressRect.anchorMax = Vector2.zero;
        progressRect.offsetMin = Vector2.zero;
        progressRect.offsetMax = new Vector2(0, 5);
        holdProgress = progressRect.GetComponent<Image>();
        holdProgress.color = new Color(0.2f, 0.95f, 0.8f);
        holdProgress.raycastTarget = false;
    }

    private void ApplyVisualStyle()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = GetPanelColor(IsTouched);
            backgroundImage.raycastTarget = false;
        }

        if (labelText != null)
        {
            labelText.text = label;
            labelText.font = font != null ? font : GetFallbackFont();
            labelText.fontSize = fontSize;
            labelText.color = fontColor;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * 0.45f));
            labelText.resizeTextMaxSize = fontSize;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.raycastTarget = false;
        }

        if (labelOutline != null)
        {
            labelOutline.effectColor = GetAdjustedFontOutlineColor();
            labelOutline.effectDistance = Vector2.one * fontOutlineThicknessPixels;
            labelOutline.useGraphicAlpha = true;
        }

        ApplyPanelOutlineStyle();

    }

    private Color GetAdjustedFontOutlineColor()
    {
        Color.RGBToHSV(fontOutlineColor, out float h, out float s, out float v);
        Color adjusted = Color.HSVToRGB(h, s, Mathf.Clamp01(v * fontOutlineBrightness));
        adjusted.a = Mathf.Clamp01(fontOutlineColor.a * fontOutlineAlpha);
        return adjusted;
    }

    private Color GetAdjustedPanelOutlineColor()
    {
        Color.RGBToHSV(panelOutlineColor, out float h, out float s, out float v);
        Color adjusted = Color.HSVToRGB(h, s, Mathf.Clamp01(v * panelOutlineBrightness));
        adjusted.a = Mathf.Clamp01(panelOutlineColor.a * panelOutlineAlpha);
        return adjusted;
    }

    private Color GetPanelColor(bool touched)
    {
        Color color = touched ? touchedColor : normalColor;
        float opacity = touched ? touchedPanelOpacity : panelOpacity;
        color.a = showPanelBackground ? Mathf.Clamp01(color.a * opacity) : 0f;
        return color;
    }

    private void EnsurePanelOutline(RectTransform backgroundRect)
    {
        RectTransform outlineRoot = FindDirectChild(PanelOutlineName, backgroundRect) as RectTransform;
        if (outlineRoot == null)
        {
            GameObject outlineObject = new GameObject(PanelOutlineName, typeof(RectTransform));
            outlineObject.transform.SetParent(backgroundRect, false);
            outlineRoot = outlineObject.GetComponent<RectTransform>();
        }

        outlineRoot.anchorMin = Vector2.zero;
        outlineRoot.anchorMax = Vector2.one;
        outlineRoot.offsetMin = Vector2.zero;
        outlineRoot.offsetMax = Vector2.zero;
        outlineRoot.SetAsLastSibling();

        panelOutlineImages = new[]
        {
            EnsureOutlineEdge(outlineRoot, "Top"),
            EnsureOutlineEdge(outlineRoot, "Bottom"),
            EnsureOutlineEdge(outlineRoot, "Left"),
            EnsureOutlineEdge(outlineRoot, "Right")
        };
    }

    private Image EnsureOutlineEdge(RectTransform outlineRoot, string edgeName)
    {
        RectTransform edgeRect = FindDirectChild(edgeName, outlineRoot) as RectTransform;
        if (edgeRect == null)
        {
            GameObject edgeObject = new GameObject(edgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edgeObject.transform.SetParent(outlineRoot, false);
            edgeRect = edgeObject.GetComponent<RectTransform>();
        }

        ConfigureOutlineEdge(edgeRect, edgeName);
        Image image = edgeRect.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void ConfigureOutlineEdge(RectTransform edgeRect, string edgeName)
    {
        float thickness = Mathf.Max(0f, panelOutlineThicknessPixels);

        if (edgeName == "Top")
        {
            edgeRect.anchorMin = new Vector2(0f, 1f);
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = new Vector2(0f, -thickness);
            edgeRect.offsetMax = Vector2.zero;
        }
        else if (edgeName == "Bottom")
        {
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = new Vector2(1f, 0f);
            edgeRect.offsetMin = Vector2.zero;
            edgeRect.offsetMax = new Vector2(0f, thickness);
        }
        else if (edgeName == "Left")
        {
            edgeRect.anchorMin = Vector2.zero;
            edgeRect.anchorMax = new Vector2(0f, 1f);
            edgeRect.offsetMin = Vector2.zero;
            edgeRect.offsetMax = new Vector2(thickness, 0f);
        }
        else
        {
            edgeRect.anchorMin = new Vector2(1f, 0f);
            edgeRect.anchorMax = Vector2.one;
            edgeRect.offsetMin = new Vector2(-thickness, 0f);
            edgeRect.offsetMax = Vector2.zero;
        }
    }

    private void ApplyPanelOutlineStyle()
    {
        bool visible = showPanelOutline && panelOutlineThicknessPixels > 0f;
        foreach (Image outlineImage in panelOutlineImages)
        {
            if (outlineImage == null)
            {
                continue;
            }

            outlineImage.enabled = visible;
            outlineImage.color = GetAdjustedPanelOutlineColor();
            ConfigureOutlineEdge(outlineImage.rectTransform, outlineImage.name);
        }
    }

    private Font GetFallbackFont()
    {
        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private bool IsAcceptedTouch(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (!PlayerArenaLayers.CanInteract(gameObject, other.gameObject)) return false;
        var rig = other.GetComponentInParent<MocopiPlayerColliderRig>();
        if (handsOnly && rig != null)
        {
            var animator = rig.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return false;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            return acceptMocopiRig && ((left != null && other.transform.IsChildOf(left)) ||
                                     (right != null && other.transform.IsChildOf(right)));
        }

        if (acceptPlayerLayer && playerLayer >= 0 && IsLayerInHierarchy(other.transform, playerLayer))
        {
            return true;
        }

        if (acceptPlayerTag && other.CompareTag(PlayerTag))
        {
            return true;
        }

        return acceptMocopiRig && other.GetComponentInParent<MocopiPlayerColliderRig>() != null;
    }

    private bool IsLayerInHierarchy(Transform current, int layer)
    {
        while (current != null)
        {
            if (current.gameObject.layer == layer)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private Transform FindDirectChild(string childName)
    {
        return FindDirectChild(childName, transform);
    }

    private Transform FindDirectChild(string childName, Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
