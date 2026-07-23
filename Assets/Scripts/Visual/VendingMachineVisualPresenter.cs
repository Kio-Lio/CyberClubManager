using UnityEngine;

[DefaultExecutionOrder(205)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class VendingMachineVisualPresenter : MonoBehaviour
{
    public const string ResourcePath =
        "Environment/Props/CyberClub_Vending";
    public const float TargetWorldWidth = 0.65f;

    private SpriteRenderer rootRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer vendingRenderer;

    public SpriteRenderer VendingRenderer => vendingRenderer;
    public SpriteRenderer ShadowRenderer => shadowRenderer;

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

        Sprite vendingSprite = Resources.Load<Sprite>(ResourcePath);
        if (vendingSprite == null)
        {
            Debug.LogWarning(
                $"Vending machine sprite was not found: {ResourcePath}."
            );
            return;
        }

        Transform visualRoot = EnsureChild(
            transform,
            "VendingMachineVisual"
        );
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        shadowRenderer = EnsureRenderer(
            visualRoot,
            "VendingShadow",
            WorldVisualPrimitives.CircleSprite
        );
        shadowRenderer.transform.localPosition =
            new Vector3(0f, 0.13f, 0f);
        shadowRenderer.transform.localScale =
            new Vector3(0.62f, 0.22f, 1f);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.32f);

        vendingRenderer = EnsureRenderer(
            visualRoot,
            "VendingSprite",
            vendingSprite
        );
        float scale = TargetWorldWidth /
            Mathf.Max(0.01f, vendingSprite.bounds.size.x);
        vendingRenderer.transform.localPosition = Vector3.zero;
        vendingRenderer.transform.localRotation = Quaternion.identity;
        vendingRenderer.transform.localScale =
            new Vector3(scale, scale, 1f);
        vendingRenderer.color = Color.white;

        rootRenderer.enabled = false;
        SyncSortingOrder();
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
        if (vendingRenderer != null)
        {
            vendingRenderer.sortingOrder = baseOrder;
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
