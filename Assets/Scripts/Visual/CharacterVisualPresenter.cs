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
    public const float VisualWidth = 0.56f;

    private SpriteRenderer rootRenderer;
    private SpriteRenderer body;
    private SpriteRenderer head;
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
            new Vector2(0.03f, -0.15f),
            new Vector2(VisualWidth, 0.19f),
            new Color(0f, 0f, 0f, 0.32f),
            0
        );
        shadow.sprite = WorldVisualPrimitives.CircleSprite;

        body = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Body",
            new Vector2(0f, -0.03f),
            new Vector2(0.44f, 0.43f),
            new Color(0.10f, 0.12f, 0.16f, 1f),
            2
        );
        body.sprite = WorldVisualPrimitives.CircleSprite;

        head = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Head",
            new Vector2(0f, 0.18f),
            new Vector2(0.26f, 0.26f),
            new Color(0.17f, 0.19f, 0.23f, 1f),
            4
        );
        head.sprite = WorldVisualPrimitives.CircleSprite;

        accent = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "RoleAccent",
            new Vector2(0f, 0.08f),
            new Vector2(0.22f, 0.045f),
            new Color(0.32f, 0.48f, 0.62f, 0.78f),
            5
        );

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        rootRenderer.enabled = false;
    }

    private void RefreshPalette()
    {
        if (body == null || head == null || accent == null)
        {
            return;
        }

        body.color = role == CharacterVisualRole.Cleaner
            ? new Color(0.10f, 0.16f, 0.17f, 1f)
            : new Color(0.09f, 0.11f, 0.15f, 1f);
        head.color = new Color(0.17f, 0.19f, 0.23f, 1f);
        accent.color = role switch
        {
            CharacterVisualRole.GamerClient =>
                new Color(0.20f, 0.43f, 0.66f, 0.82f),
            CharacterVisualRole.VIPClient =>
                new Color(0.62f, 0.48f, 0.22f, 0.82f),
            CharacterVisualRole.Cleaner =>
                new Color(0.22f, 0.55f, 0.50f, 0.82f),
            _ => new Color(0.34f, 0.45f, 0.56f, 0.78f)
        };
    }
}
