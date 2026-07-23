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
    public const float VisualWidth = 0.66f;

    private SpriteRenderer rootRenderer;
    private SpriteRenderer body;
    private SpriteRenderer shoulders;
    private SpriteRenderer lowerBody;
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
            new Vector2(0.035f, -0.185f),
            new Vector2(0.57f, 0.22f),
            new Color(0f, 0f, 0f, 0.34f),
            0
        );
        shadow.sprite = WorldVisualPrimitives.CircleSprite;

        lowerBody = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "LowerBody",
            new Vector2(0f, -0.105f),
            new Vector2(0.31f, 0.31f),
            new Color(0.12f, 0.145f, 0.19f, 1f),
            1
        );
        lowerBody.sprite = WorldVisualPrimitives.CircleSprite;

        body = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Body",
            new Vector2(0f, 0.015f),
            new Vector2(0.42f, 0.39f),
            new Color(0.15f, 0.175f, 0.22f, 1f),
            2
        );
        body.sprite = WorldVisualPrimitives.CircleSprite;

        shoulders = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shoulders",
            new Vector2(0f, 0.075f),
            new Vector2(0.58f, 0.19f),
            new Color(0.16f, 0.185f, 0.23f, 1f),
            3
        );
        shoulders.sprite = WorldVisualPrimitives.CircleSprite;

        head = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Head",
            new Vector2(0f, 0.245f),
            new Vector2(0.30f, 0.30f),
            new Color(0.25f, 0.275f, 0.32f, 1f),
            4
        );
        head.sprite = WorldVisualPrimitives.CircleSprite;

        upperLight = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "UpperLight",
            new Vector2(-0.045f, 0.292f),
            new Vector2(0.16f, 0.07f),
            new Color(0.68f, 0.76f, 0.82f, 0.24f),
            5
        );
        upperLight.sprite = WorldVisualPrimitives.CircleSprite;

        accent = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "RoleAccent",
            new Vector2(0f, 0.105f),
            new Vector2(0.22f, 0.035f),
            new Color(0.32f, 0.48f, 0.62f, 0.78f),
            5
        );

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        rootRenderer.enabled = false;
    }

    private void RefreshPalette()
    {
        if (body == null || shoulders == null || lowerBody == null ||
            head == null || upperLight == null || accent == null)
        {
            return;
        }

        Color bodyColor = role == CharacterVisualRole.Cleaner
            ? new Color(0.13f, 0.22f, 0.22f, 1f)
            : new Color(0.14f, 0.165f, 0.21f, 1f);
        body.color = bodyColor;
        shoulders.color = Color.Lerp(
            bodyColor,
            new Color(0.28f, 0.31f, 0.35f, 1f),
            0.18f
        );
        lowerBody.color = Color.Lerp(
            bodyColor,
            new Color(0.055f, 0.065f, 0.085f, 1f),
            0.34f
        );
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
