using UnityEngine;

public sealed class ClubLayoutBuilder : MonoBehaviour
{
    private const string LayoutRootName = "GeneratedClubLayout";
    private const string ObstacleLayerName = "ClubObstacle";

    private static readonly Vector3[] StartingPCPositions =
    {
        new Vector3(1.4f, 2.6f, 0f),
        new Vector3(3.8f, 2.6f, 0f),
        new Vector3(6.2f, 2.6f, 0f),
        new Vector3(1.4f, -1.4f, 0f),
        new Vector3(3.8f, -1.4f, 0f)
    };

    private static readonly Vector3[] ExpansionPCPositions =
    {
        new Vector3(6.2f, -1.4f, 0f),
        new Vector3(1.4f, -3.4f, 0f),
        new Vector3(3.8f, -3.4f, 0f),
        new Vector3(6.2f, -3.4f, 0f)
    };

    public static ClubLayoutBuilder Instance { get; private set; }

    [Header("Room")]
    [SerializeField] private Vector2 roomSize = new Vector2(16f, 10f);
    [SerializeField] private float wallThickness = 0.35f;

    [Header("Colors")]
    [SerializeField] private Color floorColor = new Color(0.12f, 0.13f, 0.16f);
    [SerializeField] private Color wallColor = new Color(0.30f, 0.32f, 0.38f);
    [SerializeField] private Color deskColor = new Color(0.22f, 0.14f, 0.08f);
    [SerializeField] private Color tableColor = new Color(0.16f, 0.17f, 0.20f);

    private Sprite squareSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeLayout()
    {
        EnsureRuntimeLayout();
    }

    private void Start()
    {
        BuildLayout();
        Invoke(nameof(RefreshRestoredPCLayout), 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static ClubLayoutBuilder EnsureRuntimeLayout()
    {
        GameObject builderObject = GameObject.Find("ClubLayoutBuilder");

        if (builderObject == null)
        {
            builderObject = new GameObject("ClubLayoutBuilder");
        }

        ClubLayoutBuilder builder =
            builderObject.GetComponent<ClubLayoutBuilder>() ??
            builderObject.AddComponent<ClubLayoutBuilder>();

        builder.BuildLayout();
        return builder;
    }

    public void BuildLayout()
    {
        ConfigureStartingPCPositions();

        Transform existingRoot = transform.Find(LayoutRootName);
        if (existingRoot != null)
        {
            ConfigureScenePresentation();
            ClientNavigationManager.EnsureRuntimeGraph();
            return;
        }

        GameObject root = new GameObject(LayoutRootName);
        root.transform.SetParent(transform, false);

        CreateFloor(root.transform);
        CreateOuterWalls(root.transform);
        CreateAdminDesk(root.transform);
        CreatePCTables(root.transform);
        ConfigureScenePresentation();
        ClientNavigationManager.EnsureRuntimeGraph();
    }

    private void CreateFloor(Transform parent)
    {
        GameObject floor = CreateVisualObject(
            "Floor",
            parent,
            Vector3.zero,
            roomSize,
            floorColor,
            -10
        );

        floor.layer = 0;
    }

    private void CreateOuterWalls(Transform parent)
    {
        float halfWidth = roomSize.x * 0.5f;
        float halfHeight = roomSize.y * 0.5f;

        CreateObstacle(
            "Wall_Top",
            parent,
            new Vector3(0f, halfHeight, 0f),
            new Vector2(roomSize.x + wallThickness, wallThickness),
            wallColor
        );
        CreateObstacle(
            "Wall_Bottom_Left",
            parent,
            new Vector3(-5f, -halfHeight, 0f),
            new Vector2(6f, wallThickness),
            wallColor
        );
        CreateObstacle(
            "Wall_Bottom_Right",
            parent,
            new Vector3(4.5f, -halfHeight, 0f),
            new Vector2(7f, wallThickness),
            wallColor
        );
        CreateObstacle(
            "Wall_Left",
            parent,
            new Vector3(-halfWidth, 0f, 0f),
            new Vector2(wallThickness, roomSize.y),
            wallColor
        );
        CreateObstacle(
            "Wall_Right",
            parent,
            new Vector3(halfWidth, 0f, 0f),
            new Vector2(wallThickness, roomSize.y),
            wallColor
        );
    }

    private void CreateAdminDesk(Transform parent)
    {
        CreateObstacle(
            "AdministratorDesk",
            parent,
            new Vector3(-4.8f, 3.1f, 0f),
            new Vector2(3.2f, 1.1f),
            deskColor
        );
    }

    private void CreatePCTables(Transform parent)
    {
        CreateObstacle(
            "PCTable_Top",
            parent,
            new Vector3(3.8f, 2.6f, 0f),
            new Vector2(7.4f, 1.1f),
            tableColor
        );
        CreateObstacle(
            "PCTable_Bottom",
            parent,
            new Vector3(3.8f, -1.4f, 0f),
            new Vector2(7.4f, 1.1f),
            tableColor
        );
        CreateObstacle(
            "PCTable_Expansion",
            parent,
            new Vector3(3.8f, -3.4f, 0f),
            new Vector2(7.4f, 1.1f),
            tableColor
        );
    }

    private void CreateObstacle(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Color color)
    {
        GameObject obstacle = CreateVisualObject(
            objectName,
            parent,
            position,
            size,
            color,
            -1
        );

        int obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
        if (obstacleLayer >= 0)
        {
            obstacle.layer = obstacleLayer;
        }

        obstacle.AddComponent<BoxCollider2D>();
    }

    private GameObject CreateVisualObject(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Color color,
        int sortingOrder)
    {
        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(parent);
        visualObject.transform.position = position;
        visualObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return visualObject;
    }

    private void ConfigureScenePresentation()
    {
        Camera mainCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        if (mainCamera != null && mainCamera.orthographic)
        {
            mainCamera.orthographicSize = 6.5f;
        }

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            return;
        }

        Rigidbody2D body = player.GetComponent<Rigidbody2D>() ??
            player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        EnsureSolidPlayerCollider(player);

        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            playerRenderer.sortingOrder = 3;
        }
    }

    private static void ConfigureStartingPCPositions()
    {
        for (int i = 0; i < StartingPCPositions.Length; i++)
        {
            GameObject pc = GameObject.Find($"PC_{i + 1:00}");
            if (pc != null)
            {
                pc.transform.position = StartingPCPositions[i];
            }
        }
    }

    private static void EnsureSolidPlayerCollider(GameObject player)
    {
        foreach (Collider2D collider in player.GetComponents<Collider2D>())
        {
            if (!collider.isTrigger)
            {
                return;
            }
        }

        CircleCollider2D solidCollider = player.AddComponent<CircleCollider2D>();
        solidCollider.radius = 0.45f;
        solidCollider.isTrigger = false;
    }

    private void RefreshRestoredPCLayout()
    {
        for (int i = 0; i < ExpansionPCPositions.Length; i++)
        {
            GameObject pc = GameObject.Find($"PC_{i + 6:00}");
            if (pc != null)
            {
                pc.transform.position = ExpansionPCPositions[i];
            }
        }

        ClientNavigationManager.EnsureRuntimeGraph();
    }

    private Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        squareSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f
        );

        return squareSprite;
    }

    private void OnValidate()
    {
        roomSize.x = Mathf.Max(8f, roomSize.x);
        roomSize.y = Mathf.Max(6f, roomSize.y);
        wallThickness = Mathf.Clamp(wallThickness, 0.1f, 1f);
    }
}
