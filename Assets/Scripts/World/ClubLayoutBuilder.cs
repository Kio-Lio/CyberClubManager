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
    [SerializeField] private Vector2 roomSize = new Vector2(23.5f, 10f);
    [SerializeField] private float wallThickness = 0.35f;

    [Header("Camera Bounds")]
    [SerializeField, Min(0f)] private float cameraBoundsPadding = 0.25f;

    [Header("Private Room")]
    [SerializeField] private Vector2 privateRoomCenter =
        new Vector2(9.5f, 3.4f);
    [SerializeField] private Vector2 privateRoomSize =
        new Vector2(4.5f, 2.6f);

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
            CreatePrivateRoom(existingRoot);
            CreatePrivateRoomContent();
            CreateCameraBounds();
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
        CreatePrivateRoom(root.transform);
        CreatePrivateRoomContent();
        CreateCameraBounds();
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
            -10000,
            false
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
            wallColor,
            false
        );
        const float entranceCenterX = -0.5f;
        const float entranceWidth = 1.2f;
        float entranceLeft = entranceCenterX - entranceWidth * 0.5f;
        float entranceRight = entranceCenterX + entranceWidth * 0.5f;
        float leftSegmentWidth = entranceLeft + halfWidth;
        float rightSegmentWidth = halfWidth - entranceRight;

        CreateObstacle(
            "Wall_Bottom_Left",
            parent,
            new Vector3(
                -halfWidth + leftSegmentWidth * 0.5f,
                -halfHeight,
                0f
            ),
            new Vector2(leftSegmentWidth, wallThickness),
            wallColor,
            false
        );
        CreateObstacle(
            "Wall_Bottom_Right",
            parent,
            new Vector3(
                entranceRight + rightSegmentWidth * 0.5f,
                -halfHeight,
                0f
            ),
            new Vector2(rightSegmentWidth, wallThickness),
            wallColor,
            false
        );
        CreateObstacle(
            "Wall_Left",
            parent,
            new Vector3(-halfWidth, 0f, 0f),
            new Vector2(wallThickness, roomSize.y),
            wallColor,
            false
        );
        CreateObstacle(
            "Wall_Right",
            parent,
            new Vector3(halfWidth, 0f, 0f),
            new Vector2(wallThickness, roomSize.y),
            wallColor,
            false
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

    private void CreatePrivateRoom(Transform parent)
    {
        float halfWidth = privateRoomSize.x * 0.5f;
        float halfHeight = privateRoomSize.y * 0.5f;
        Vector3 center = new Vector3(
            privateRoomCenter.x,
            privateRoomCenter.y,
            0f
        );

        EnsurePrivateRoomObstacle(
            "PrivateRoom_Wall_Top",
            parent,
            center + new Vector3(0f, halfHeight, 0f),
            new Vector2(privateRoomSize.x, wallThickness)
        );
        EnsurePrivateRoomObstacle(
            "PrivateRoom_Wall_Left",
            parent,
            center + new Vector3(-halfWidth, 0f, 0f),
            new Vector2(wallThickness, privateRoomSize.y)
        );
        EnsurePrivateRoomObstacle(
            "PrivateRoom_Wall_Right",
            parent,
            center + new Vector3(halfWidth, 0f, 0f),
            new Vector2(wallThickness, privateRoomSize.y)
        );

        const float doorwayWidth = 1.2f;
        float bottomSegmentWidth =
            (privateRoomSize.x - doorwayWidth) * 0.5f;

        EnsurePrivateRoomObstacle(
            "PrivateRoom_Wall_Bottom_Left",
            parent,
            center + new Vector3(
                -doorwayWidth * 0.5f - bottomSegmentWidth * 0.5f,
                -halfHeight,
                0f
            ),
            new Vector2(bottomSegmentWidth, wallThickness)
        );
        EnsurePrivateRoomObstacle(
            "PrivateRoom_Wall_Bottom_Right",
            parent,
            center + new Vector3(
                doorwayWidth * 0.5f + bottomSegmentWidth * 0.5f,
                -halfHeight,
                0f
            ),
            new Vector2(bottomSegmentWidth, wallThickness)
        );
    }

    private void EnsurePrivateRoomObstacle(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size)
    {
        if (parent.Find(objectName) != null)
        {
            return;
        }

        CreateObstacle(
            objectName,
            parent,
            position,
            size,
            wallColor,
            false
        );
    }

    private void CreatePrivateRoomContent()
    {
        ClientNavigationManager navigation =
            ClientNavigationManager.EnsureRuntimeGraph();

        GameObject managerObject = GameObject.Find("RoomUnlockManager");
        if (managerObject == null)
        {
            managerObject = new GameObject("RoomUnlockManager");
        }

        RoomUnlockManager roomManager =
            managerObject.GetComponent<RoomUnlockManager>() ??
            managerObject.AddComponent<RoomUnlockManager>();

        ClientNavigationNode doorNode = GameObject.Find(
            "PrivateRoomDoorNode"
        )?.GetComponent<ClientNavigationNode>();

        GameObject doorObject = GameObject.Find("PrivateRoomDoor");
        if (doorObject == null)
        {
            doorObject = new GameObject("PrivateRoomDoor");
            doorObject.transform.position =
                new Vector3(privateRoomCenter.x, 2.1f, 0f);
            doorObject.transform.localScale =
                new Vector3(1.2f, wallThickness, 1f);

            SpriteRenderer doorRenderer =
                doorObject.AddComponent<SpriteRenderer>();
            doorRenderer.sprite = GetSquareSprite();
            YSortRenderer.SetSortingLayer(doorRenderer, "World");
            doorObject.AddComponent<BoxCollider2D>();
            doorObject.AddComponent<RoomDoor>();
        }

        RoomDoor roomDoor = doorObject.GetComponent<RoomDoor>();
        roomDoor.Configure(
            "PrivateRoom01",
            "Приватная комната",
            3,
            1500,
            doorNode
        );
        roomManager.RegisterDoor(roomDoor);

        EnsurePrivateRoomPC(
            "PC_10",
            new Vector3(8.55f, 3.55f, 0f),
            roomDoor
        );
        EnsurePrivateRoomPC(
            "PC_11",
            new Vector3(10.45f, 3.55f, 0f),
            roomDoor
        );

        navigation.EnsureDefaultGraph();
    }

    private void EnsurePrivateRoomPC(
        string pcName,
        Vector3 position,
        RoomDoor roomDoor)
    {
        GameObject pcObject = GameObject.Find(pcName);
        bool created = pcObject == null;

        if (created)
        {
            pcObject = new GameObject(pcName);
            pcObject.transform.position = position;
            pcObject.transform.localScale = Vector3.one;

            SpriteRenderer renderer = pcObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            pcObject.AddComponent<BoxCollider2D>();
            pcObject.AddComponent<PC>();
        }

        PC pc = pcObject.GetComponent<PC>();
        pc.SetRequiredRoomDoor(roomDoor);
        pc.ConfigureYSorting();

        if (created)
        {
            pc.RestoreTier(PCTier.Gaming);
        }
    }

    private void CreateObstacle(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size,
        Color color,
        bool useYSorting = true)
    {
        GameObject obstacle = CreateVisualObject(
            objectName,
            parent,
            position,
            size,
            color,
            useYSorting ? -1 : 5000,
            useYSorting
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
        int sortingOffset,
        bool useYSorting = true)
    {
        GameObject visualObject = new GameObject(objectName);
        visualObject.transform.SetParent(parent);
        visualObject.transform.position = position;
        visualObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        YSortRenderer.SetSortingLayer(
            renderer,
            sortingOffset == -10000 ? "Background" : "World"
        );

        if (useYSorting)
        {
            YSortRenderer ySort = visualObject.AddComponent<YSortRenderer>();
            ySort.SetSortingPoint(CreateSortingPoint(
                visualObject.transform,
                size
            ));
            ySort.SetSortingOffset(sortingOffset);
        }
        else
        {
            renderer.sortingOrder = sortingOffset;
        }

        return visualObject;
    }

    private static Transform CreateSortingPoint(
        Transform parent,
        Vector2 objectSize)
    {
        GameObject pointObject = new GameObject("SortingPoint");
        pointObject.transform.SetParent(parent, false);

        float parentScaleY = Mathf.Abs(parent.localScale.y);
        float localBottomY = parentScaleY > 0f
            ? -objectSize.y * 0.5f / parentScaleY
            : -0.5f;
        pointObject.transform.localPosition =
            new Vector3(0f, localBottomY, 0f);

        return pointObject.transform;
    }

    private void ConfigureScenePresentation()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
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
                YSortRenderer.Ensure(player, 20, -0.45f);
            }
        }

        CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
        CameraBounds2D cameraBounds = FindAnyObjectByType<CameraBounds2D>();
        if (cameraFollow != null && cameraBounds != null)
        {
            cameraFollow.SetBounds(cameraBounds);
        }

        if (cameraFollow != null && player != null)
        {
            cameraFollow.SetTarget(player.transform);
        }
    }

    private void CreateCameraBounds()
    {
        Transform existingBounds = transform.Find("CameraBounds");
        GameObject boundsObject = existingBounds != null
            ? existingBounds.gameObject
            : new GameObject("CameraBounds");

        if (existingBounds == null)
        {
            boundsObject.transform.SetParent(transform, false);
        }

        boundsObject.transform.localPosition = Vector3.zero;

        CameraBounds2D bounds = boundsObject.GetComponent<CameraBounds2D>() ??
            boundsObject.AddComponent<CameraBounds2D>();
        bounds.Configure(new Vector2(
            Mathf.Max(1f, roomSize.x - cameraBoundsPadding * 2f),
            Mathf.Max(1f, roomSize.y - cameraBoundsPadding * 2f)
        ));

        SpriteRenderer renderer = boundsObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            Destroy(renderer);
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
        cameraBoundsPadding = Mathf.Max(0f, cameraBoundsPadding);
        privateRoomSize.x = Mathf.Max(2f, privateRoomSize.x);
        privateRoomSize.y = Mathf.Max(2f, privateRoomSize.y);
    }
}
