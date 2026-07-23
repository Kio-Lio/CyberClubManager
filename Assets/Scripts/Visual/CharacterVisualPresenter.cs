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
    public const string RegularGuestResourcePath =
        "Characters/RegularGuest_Walk_4x5_64px";
    public const int RegularGuestFramesPerDirection = 5;

    private const int RegularGuestDirectionCount = 4;
    private const int RegularGuestFrameSize = 64;
    private const float RegularGuestFrameDuration = 0.12f;
    private const float RegularGuestVisualScale = 0.92f;
    private const float MovementThreshold = 0.000001f;

    private enum FacingDirection
    {
        Down,
        Left,
        Right,
        Up
    }

    private static Sprite[,] regularGuestFrames;

    private SpriteRenderer rootRenderer;
    private SpriteRenderer shadow;
    private SpriteRenderer body;
    private SpriteRenderer shoulders;
    private SpriteRenderer lowerBody;
    private SpriteRenderer head;
    private SpriteRenderer upperLight;
    private SpriteRenderer accent;
    private SpriteRenderer regularGuestRenderer;
    private WorldVisualPart[] visualParts;
    private CharacterVisualRole role;
    private Vector3 previousPosition;
    private FacingDirection facingDirection;
    private float animationTimer;
    private int animationFrame;

    public CharacterVisualRole Role => role;
    public SpriteRenderer RegularGuestRenderer => regularGuestRenderer;
    public int RegularGuestAnimationFrame => animationFrame;

    private void Awake()
    {
        rootRenderer = GetComponent<SpriteRenderer>();
        BuildVisual();
        previousPosition = transform.position;
        RefreshPalette();
    }

    private void LateUpdate()
    {
        UpdateRegularGuestAnimation();

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
        ResetAnimation();
        RefreshPalette();
    }

    public void ConfigureCleaner()
    {
        role = CharacterVisualRole.Cleaner;
        ResetAnimation();
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

        shadow = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shadow",
            new Vector2(0.035f, -0.185f),
            new Vector2(0.57f, 0.22f),
            new Color(0f, 0f, 0f, 0.34f),
            0
        );
        shadow.sprite = WorldVisualPrimitives.CircleSprite;

        regularGuestRenderer = CreateRegularGuestRenderer(
            visualRoot.transform
        );

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

    private SpriteRenderer CreateRegularGuestRenderer(Transform parent)
    {
        GameObject spriteObject = new("RegularGuestSprite");
        spriteObject.transform.SetParent(parent, false);
        spriteObject.transform.localPosition =
            new Vector3(0f, -0.02f, 0f);
        spriteObject.transform.localScale = Vector3.one *
            RegularGuestVisualScale;

        SpriteRenderer renderer =
            spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetRegularGuestFrame(
            FacingDirection.Down,
            0
        );
        renderer.color = Color.white;
        YSortRenderer.SetSortingLayer(renderer, "World");

        WorldVisualPart visualPart =
            spriteObject.AddComponent<WorldVisualPart>();
        visualPart.OrderOffset = 4;
        return renderer;
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

        RefreshRolePresentation();
    }

    private void RefreshRolePresentation()
    {
        bool showRegularGuest =
            role == CharacterVisualRole.RegularClient &&
            regularGuestRenderer != null &&
            regularGuestRenderer.sprite != null;

        if (regularGuestRenderer != null)
        {
            regularGuestRenderer.enabled = showRegularGuest;
        }

        body.enabled = !showRegularGuest;
        shoulders.enabled = !showRegularGuest;
        lowerBody.enabled = !showRegularGuest;
        head.enabled = !showRegularGuest;
        upperLight.enabled = !showRegularGuest;
        accent.enabled = !showRegularGuest;
        shadow.enabled = true;
    }

    private void UpdateRegularGuestAnimation()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - previousPosition;
        previousPosition = currentPosition;

        if (role != CharacterVisualRole.RegularClient ||
            regularGuestRenderer == null ||
            !regularGuestRenderer.enabled)
        {
            return;
        }

        bool isMoving = movement.sqrMagnitude > MovementThreshold;
        if (!isMoving)
        {
            if (animationFrame != 0)
            {
                animationFrame = 0;
                RefreshRegularGuestFrame();
            }

            animationTimer = 0f;
            return;
        }

        facingDirection = ResolveFacingDirection(movement);
        animationTimer += Time.deltaTime;
        while (animationTimer >= RegularGuestFrameDuration)
        {
            animationTimer -= RegularGuestFrameDuration;
            animationFrame =
                (animationFrame + 1) %
                RegularGuestFramesPerDirection;
        }

        RefreshRegularGuestFrame();
    }

    private void ResetAnimation()
    {
        previousPosition = transform.position;
        facingDirection = FacingDirection.Down;
        animationTimer = 0f;
        animationFrame = 0;
        RefreshRegularGuestFrame();
    }

    private void RefreshRegularGuestFrame()
    {
        if (regularGuestRenderer == null)
        {
            return;
        }

        Sprite frame = GetRegularGuestFrame(
            facingDirection,
            animationFrame
        );
        if (frame != null)
        {
            regularGuestRenderer.sprite = frame;
        }
    }

    private static FacingDirection ResolveFacingDirection(
        Vector3 movement)
    {
        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
        {
            return movement.x < 0f
                ? FacingDirection.Left
                : FacingDirection.Right;
        }

        return movement.y < 0f
            ? FacingDirection.Down
            : FacingDirection.Up;
    }

    private static Sprite GetRegularGuestFrame(
        FacingDirection direction,
        int frameIndex)
    {
        EnsureRegularGuestFrames();
        if (regularGuestFrames == null)
        {
            return null;
        }

        int directionIndex = (int)direction;
        int safeFrameIndex = Mathf.Clamp(
            frameIndex,
            0,
            RegularGuestFramesPerDirection - 1
        );
        return regularGuestFrames[directionIndex, safeFrameIndex];
    }

    private static void EnsureRegularGuestFrames()
    {
        if (regularGuestFrames != null &&
            regularGuestFrames[0, 0] != null)
        {
            return;
        }

        regularGuestFrames = null;
        Sprite sheet = Resources.Load<Sprite>(
            RegularGuestResourcePath
        );
        if (sheet == null)
        {
            Debug.LogWarning(
                $"Regular guest sprite sheet was not found: " +
                $"{RegularGuestResourcePath}."
            );
            return;
        }

        Texture2D texture = sheet.texture;
        int expectedWidth =
            RegularGuestFrameSize *
            RegularGuestFramesPerDirection;
        int expectedHeight =
            RegularGuestFrameSize *
            RegularGuestDirectionCount;
        if (texture.width != expectedWidth ||
            texture.height != expectedHeight)
        {
            Debug.LogWarning(
                $"Regular guest sprite sheet must be " +
                $"{expectedWidth}x{expectedHeight}, but is " +
                $"{texture.width}x{texture.height}."
            );
            return;
        }

        regularGuestFrames = new Sprite[
            RegularGuestDirectionCount,
            RegularGuestFramesPerDirection
        ];

        for (int direction = 0;
             direction < RegularGuestDirectionCount;
             direction++)
        {
            int sourceRow =
                RegularGuestDirectionCount - 1 - direction;

            for (int frame = 0;
                 frame < RegularGuestFramesPerDirection;
                 frame++)
            {
                Rect frameRect = new(
                    frame * RegularGuestFrameSize,
                    sourceRow * RegularGuestFrameSize,
                    RegularGuestFrameSize,
                    RegularGuestFrameSize
                );
                Sprite sprite = Sprite.Create(
                    texture,
                    frameRect,
                    new Vector2(0.5f, 0.08f),
                    RegularGuestFrameSize,
                    0,
                    SpriteMeshType.FullRect
                );
                sprite.name =
                    $"RegularGuest_{(FacingDirection)direction}_" +
                    $"{frame:00}";
                regularGuestFrames[direction, frame] = sprite;
            }
        }
    }
}
