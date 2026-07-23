using UnityEngine;

public enum CharacterVisualRole
{
    RegularClient,
    GamerClient,
    VIPClient,
    Cleaner
}

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CharacterVisualPresenter : MonoBehaviour
{
    public const float VisualWidth = 0.64f;

    private SpriteRenderer rootRenderer;
    private SpriteRenderer body;
    private SpriteRenderer head;
    private SpriteRenderer upperLight;
    private SpriteRenderer accent;
    private WorldVisualPart[] visualParts;
    private CharacterVisualRole role;

    public CharacterVisualRole Role => role;

    private void Awake()
    {
        rootRenderer = GetComponent<SpriteRenderer>();
        BuildVisual();
        RefreshPalette();
    }

    private void LateUpdate()
    {
        if (rootRenderer == null || visualParts == null)
        {
            return;
        }

        int baseOrder = rootRenderer.sortingOrder;
        foreach (WorldVisualPart part in visualParts)
        {
            if (part != null &&
                part.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.sortingOrder = baseOrder + part.OrderOffset;
            }
        }
    }

    public void ConfigureClient(ClientType clientType)
    {
        role = clientType switch
        {
            ClientType.Gamer => CharacterVisualRole.GamerClient,
            ClientType.VIP => CharacterVisualRole.VIPClient,
            _ => CharacterVisualRole.RegularClient
        };
        RefreshPalette();
    }

    public void ConfigureCleaner()
    {
        role = CharacterVisualRole.Cleaner;
        RefreshPalette();
    }

    private void BuildVisual()
    {
        Transform existing = transform.Find("CharacterVisual");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject visualRoot = new("CharacterVisual");
        visualRoot.transform.SetParent(transform, false);

        SpriteRenderer shadow = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shadow",
            new Vector2(0.035f, -0.17f),
            new Vector2(VisualWidth, 0.21f),
            new Color(0f, 0f, 0f, 0.38f),
            0
        );
        shadow.sprite = WorldVisualPrimitives.CircleSprite;

        body = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Body",
            new Vector2(0f, -0.035f),
            new Vector2(0.50f, 0.49f),
            new Color(0.15f, 0.175f, 0.22f, 1f),
            2
        );
        body.sprite = WorldVisualPrimitives.CircleSprite;

        head = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Head",
            new Vector2(0f, 0.205f),
            new Vector2(0.29f, 0.29f),
            new Color(0.25f, 0.275f, 0.32f, 1f),
            4
        );
        head.sprite = WorldVisualPrimitives.CircleSprite;

        upperLight = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "UpperLight",
            new Vector2(-0.045f, 0.255f),
            new Vector2(0.15f, 0.075f),
            new Color(0.68f, 0.76f, 0.82f, 0.26f),
            5
        );
        upperLight.sprite = WorldVisualPrimitives.CircleSprite;

        accent = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "RoleAccent",
            new Vector2(0f, 0.085f),
            new Vector2(0.25f, 0.045f),
            new Color(0.32f, 0.48f, 0.62f, 0.78f),
            5
        );

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        rootRenderer.enabled = false;
    }

    private void RefreshPalette()
    {
        if (body == null || head == null || upperLight == null ||
            accent == null)
        {
            return;
        }

        body.color = role == CharacterVisualRole.Cleaner
            ? new Color(0.13f, 0.22f, 0.22f, 1f)
            : new Color(0.14f, 0.165f, 0.21f, 1f);
        head.color = new Color(0.24f, 0.27f, 0.315f, 1f);
        upperLight.color = role == CharacterVisualRole.VIPClient
            ? new Color(0.84f, 0.73f, 0.45f, 0.30f)
            : new Color(0.67f, 0.76f, 0.84f, 0.28f);
        accent.color = role switch
        {
            CharacterVisualRole.GamerClient =>
                new Color(0.22f, 0.48f, 0.70f, 0.82f),
            CharacterVisualRole.VIPClient =>
                new Color(0.62f, 0.48f, 0.22f, 0.82f),
            CharacterVisualRole.Cleaner =>
                new Color(0.24f, 0.58f, 0.52f, 0.82f),
            _ => new Color(0.38f, 0.50f, 0.61f, 0.78f)
        };
    }
}
