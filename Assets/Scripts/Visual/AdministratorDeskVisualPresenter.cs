using UnityEngine;

[DefaultExecutionOrder(205)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class AdministratorDeskVisualPresenter : MonoBehaviour,
    IWorldInteractionVisual
{
    public const string ResourcePath =
        "Environment/Reception/AdministratorDesk_Final";
    public const float VisualLocalOffsetY = -0.89f;

    private static readonly Color HoverColor =
        new(0.22f, 0.70f, 1f, 0.13f);
    private static readonly Color SelectionColor =
        new(0.05f, 0.78f, 1f, 0.25f);

    private SpriteRenderer rootRenderer;
    private Transform visualRoot;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer deskRenderer;
    private SpriteRenderer interactionGlowRenderer;
    private bool isHovered;
    private bool isSelected;

    public SpriteRenderer DeskRenderer => deskRenderer;
    public SpriteRenderer ShadowRenderer => shadowRenderer;
    public SpriteRenderer InteractionGlowRenderer =>
        interactionGlowRenderer;
    public bool IsHovered => isHovered;
    public bool IsSelected => isSelected;
    public Vector2 WorldSize => deskRenderer != null
        ? deskRenderer.bounds.size
        : Vector2.zero;

    private void Awake()
    {
        ApplyVisual();
    }

    private void LateUpdate()
    {
        SyncSortingOrder();
    }

    public void ApplyVisual()
    {
        if (rootRenderer == null)
        {
            rootRenderer = GetComponent<SpriteRenderer>();
        }

        Sprite finalSprite = Resources.Load<Sprite>(ResourcePath);
        if (finalSprite == null)
        {
            Debug.LogWarning(
                $"Administrator desk sprite was not found: {ResourcePath}."
            );
            return;
        }

        visualRoot = EnsureChild(transform, "AdministratorDeskVisual");
        visualRoot.localPosition =
            new Vector3(0f, VisualLocalOffsetY, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        shadowRenderer = EnsureRenderer(
            visualRoot,
            "DeskShadow",
            WorldVisualPrimitives.CircleSprite
        );
        shadowRenderer.transform.localPosition =
            new Vector3(0f, 0.31f, 0f);
        shadowRenderer.transform.localScale =
            new Vector3(3.58f, 0.50f, 1f);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.34f);

        interactionGlowRenderer = EnsureRenderer(
            visualRoot,
            "InteractionGlow",
            finalSprite
        );
        interactionGlowRenderer.transform.localPosition = Vector3.zero;
        interactionGlowRenderer.transform.localRotation =
            Quaternion.identity;

        deskRenderer = EnsureRenderer(
            visualRoot,
            "DeskSprite",
            finalSprite
        );
        deskRenderer.transform.localPosition = Vector3.zero;
        deskRenderer.transform.localRotation = Quaternion.identity;
        deskRenderer.transform.localScale = Vector3.one;
        deskRenderer.color = Color.white;

        rootRenderer.enabled = false;
        RefreshInteractionVisual();
        SyncSortingOrder();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        RefreshInteractionVisual();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshInteractionVisual();
    }

    private void RefreshInteractionVisual()
    {
        if (interactionGlowRenderer == null)
        {
            return;
        }

        bool visible = isHovered || isSelected;
        interactionGlowRenderer.enabled = visible;
        interactionGlowRenderer.color = isSelected
            ? SelectionColor
            : HoverColor;
        interactionGlowRenderer.transform.localScale = isSelected
            ? Vector3.one * 1.018f
            : Vector3.one * 1.010f;
    }

    private void SyncSortingOrder()
    {
        if (rootRenderer == null)
        {
            return;
        }

        int baseOrder = rootRenderer.sortingOrder;
        if (shadowRenderer != null)
        {
            shadowRenderer.sortingOrder = baseOrder - 2;
        }
        if (deskRenderer != null)
        {
            deskRenderer.sortingOrder = baseOrder;
        }
        if (interactionGlowRenderer != null)
        {
            interactionGlowRenderer.sortingOrder = baseOrder + 2;
        }
    }

    private static Transform EnsureChild(
        Transform parent,
        string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new(childName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static SpriteRenderer EnsureRenderer(
        Transform parent,
        string objectName,
        Sprite sprite)
    {
        Transform existing = parent.Find(objectName);
        GameObject rendererObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName);
        rendererObject.transform.SetParent(parent, false);

        SpriteRenderer renderer =
            rendererObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = rendererObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = sprite;
        YSortRenderer.SetSortingLayer(renderer, "World");
        return renderer;
    }
}
