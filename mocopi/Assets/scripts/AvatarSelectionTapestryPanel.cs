using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visual and input bridge for one avatar choice in the tapestry-style selection screen.
/// </summary>
public class AvatarSelectionTapestryPanel : MonoBehaviour
{
    [SerializeField] private MocopiGameStateController stateController;
    [SerializeField] private MocopiLocalGameLoopController localGameLoopController;
    [SerializeField] private int playerIndex;
    [SerializeField] private int avatarIndex;
    [SerializeField] private bool confirmOnChoose;

    [Header("UI Image Visuals")]
    [SerializeField] private Image tapestryImage;
    [SerializeField] private Image avatarPreviewImage;
    [SerializeField] private Sprite avatarSprite;

    [Header("Mesh Renderer Visuals")]
    [SerializeField] private MeshRenderer tapestryRenderer;
    [SerializeField] private MeshRenderer avatarPreviewRenderer;
    [SerializeField] private Texture2D tapestryTexture;
    [SerializeField] private Texture2D avatarTexture;
    [SerializeField] private string texturePropertyName = "_BaseMap";
    [SerializeField] private string fallbackTexturePropertyName = "_MainTex";
    [SerializeField] private string colorPropertyName = "_BaseColor";
    [SerializeField] private string fallbackColorPropertyName = "_Color";

    [Header("Labels")]
    [SerializeField] private Text labelText;
    [SerializeField] private Text countdownText;
    [SerializeField] private string label = "Avatar";

    [Header("Selection Style")]
    [SerializeField] private Color normalColor = new Color(0.16f, 0.14f, 0.12f, 0.92f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.86f, 0.1f, 1f);
    [SerializeField] private float selectedScale = 1.06f;

    private Vector3 baseScale = Vector3.one;
    private MaterialPropertyBlock tapestryPropertyBlock;
    private MaterialPropertyBlock previewPropertyBlock;
    private bool isSelected;

    public int AvatarIndex => avatarIndex;

    private void Awake()
    {
        CacheDefaults();
        ApplyContent();
    }

    private void OnEnable()
    {
        CacheDefaults();
        ApplyContent();
    }

    private void OnValidate()
    {
        CacheDefaults();
        ApplyContent();
    }

    public void Choose()
    {
        if (localGameLoopController == null)
        {
            localGameLoopController = FindAnyObjectByType<MocopiLocalGameLoopController>();
        }

        if (localGameLoopController != null)
        {
            localGameLoopController.ChooseAvatarForPlayer(playerIndex, avatarIndex);
            if (confirmOnChoose)
            {
                localGameLoopController.ConfirmAvatarSelection();
            }

            return;
        }

        if (stateController == null)
        {
            stateController = FindAnyObjectByType<MocopiGameStateController>();
        }

        if (stateController != null)
        {
            stateController.ChooseAvatar(avatarIndex);
        }
    }

    public void Configure(MocopiGameStateController controller, int index)
    {
        stateController = controller;
        avatarIndex = index;
        ApplyContent();
    }

    public void Configure(MocopiLocalGameLoopController controller, int targetPlayerIndex, int index)
    {
        localGameLoopController = controller;
        playerIndex = targetPlayerIndex;
        avatarIndex = index;
        ApplyContent();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (tapestryImage != null)
        {
            tapestryImage.color = selected ? selectedColor : normalColor;
        }

        ApplyRendererSelection();
        transform.localScale = selected ? baseScale * selectedScale : baseScale;
    }

    public void SetCountdown(string value)
    {
        if (countdownText != null)
        {
            countdownText.text = value;
        }
    }

    private void CacheDefaults()
    {
        if (baseScale == Vector3.one)
        {
            baseScale = transform.localScale;
        }

        if (tapestryImage == null)
        {
            tapestryImage = GetComponent<Image>();
        }

        if (tapestryRenderer == null)
        {
            tapestryRenderer = GetComponent<MeshRenderer>();
        }

        if (tapestryPropertyBlock == null)
        {
            tapestryPropertyBlock = new MaterialPropertyBlock();
        }

        if (previewPropertyBlock == null)
        {
            previewPropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ApplyContent()
    {
        if (avatarPreviewImage != null && avatarSprite != null)
        {
            avatarPreviewImage.sprite = avatarSprite;
        }

        ApplyRendererTexture(tapestryRenderer, tapestryPropertyBlock, tapestryTexture);
        ApplyRendererTexture(avatarPreviewRenderer, previewPropertyBlock, avatarTexture);
        ApplyRendererSelection();

        if (labelText != null)
        {
            labelText.text = label;
        }
    }

    private void ApplyRendererTexture(MeshRenderer targetRenderer, MaterialPropertyBlock propertyBlock, Texture texture)
    {
        if (targetRenderer == null || propertyBlock == null || texture == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        SetTextureProperty(targetRenderer, propertyBlock, texturePropertyName, texture);
        SetTextureProperty(targetRenderer, propertyBlock, fallbackTexturePropertyName, texture);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyRendererSelection()
    {
        if (tapestryRenderer == null || tapestryPropertyBlock == null)
        {
            return;
        }

        Color color = isSelected ? selectedColor : normalColor;
        tapestryRenderer.GetPropertyBlock(tapestryPropertyBlock);
        SetColorProperty(tapestryRenderer, tapestryPropertyBlock, colorPropertyName, color);
        SetColorProperty(tapestryRenderer, tapestryPropertyBlock, fallbackColorPropertyName, color);
        tapestryRenderer.SetPropertyBlock(tapestryPropertyBlock);
    }

    private static void SetTextureProperty(Renderer targetRenderer, MaterialPropertyBlock propertyBlock, string propertyName, Texture texture)
    {
        if (targetRenderer == null || propertyBlock == null || texture == null || string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        Material material = targetRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(propertyName))
        {
            return;
        }

        propertyBlock.SetTexture(propertyName, texture);
    }

    private static void SetColorProperty(Renderer targetRenderer, MaterialPropertyBlock propertyBlock, string propertyName, Color color)
    {
        if (targetRenderer == null || propertyBlock == null || string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        Material material = targetRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(propertyName))
        {
            return;
        }

        propertyBlock.SetColor(propertyName, color);
    }
}
