using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClubLayoutBuilder : MonoBehaviour
{
    private const string LayoutRootName = "GeneratedClubLayout";
    private const string ObstacleLayerName = "ClubObstacle";

    private static readonly Vector3[] StartingPCPositions =
    {
        new Vector3(1.2f, 2.8f, 0f),
        new Vector3(3.8f, 2.8f, 0f),
        new Vector3(6.4f, 2.8f, 0f),
        new Vector3(1.2f, -0.7f, 0f),
        new Vector3(3.8f, -0.7f, 0f)
    };

    private static readonly Vector3[] ExpansionPCPositions =
    {
        new Vector3(6.4f, -0.7f, 0f),
        new Vector3(1.2f, -3.3f, 0f),
        new Vector3(3.8f, -3.3f, 0f),
        new Vector3(6.4f, -3.3f, 0f)
    };

    public static ClubLayoutBuilder Instance { get; private set; }

    [Header("Room")]
    [SerializeField] private Vector2 roomSize = new Vector2(30f, 10f);
    [SerializeField] private float wallThickness = 0.35f;

    [Header("Camera Bounds")]
    [SerializeField, Min(0f)] private float cameraBoundsPadding = 0.25f;

    [Header("Unlockable Rooms")]
    [SerializeField] private UnlockableRoomDefinition[] unlockableRooms;

    [Header("Colors")]
    [SerializeField] private Color floorColor = new Color(0.12f, 0.13f, 0.16f);
    [SerializeField] private Color wallColor = new Color(0.30f, 0.32f, 0.38f);
    [SerializeField] private Color deskColor = new Color(0.22f, 0.14f, 0.08f);
    [SerializeField] private Color tableColor = new Color(0.16f, 0.17f, 0.20f);

    private Sprite squareSprite;

    public IReadOnlyList<UnlockableRoomDefinition> UnlockableRooms =>
        unlockableRooms;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUnlockableRoomDefinitions();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeLayout()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene")
        {
            return;
        }

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
        EnsureUnlockableRoomDefinitions();
        ConfigureStartingPCPositions();

        Transform existingRoot = transform.Find(LayoutRootName);
        if (existingRoot != null)
        {
            CreateFloor(existingRoot);
            CreateOuterWalls(existingRoot);
            CreateAdminDesk(existingRoot);
            CreatePCTables(existingRoot);
            CreateUnlockableRooms(existingRoot);
            ClientNavigationManager.EnsureRuntimeGraph();
            BuildUnlockableRoomContent();
            CreateCameraBounds();
            ConfigureScenePresentation();
            return;
        }

        GameObject root = new GameObject(LayoutRootName);
        root.transform.SetParent(transform, false);

        CreateFloor(root.transform);
        CreateOuterWalls(root.transform);
        CreateAdminDesk(root.transform);
        CreatePCTables(root.transform);
        CreateUnlockableRooms(root.transform);
        ClientNavigationManager.EnsureRuntimeGraph();
        BuildUnlockableRoomContent();
        CreateCameraBounds();
        ConfigureScenePresentation();
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
        Vector2 deskSize = new(3.2f, 1.1f);
        GameObject desk = CreateObstacle(
            "AdministratorDesk",
            parent,
            new Vector3(-4.8f, 3.1f, 0f),
            deskSize,
            deskColor
        );

        desk.transform.localScale = Vector3.one;
        BoxCollider2D collider = desk.GetComponent<BoxCollider2D>();
        collider.size = deskSize;

        ReceptionVisualPresenter presenter =
            desk.GetComponent<ReceptionVisualPresenter>() ??
            desk.AddComponent<ReceptionVisualPresenter>();
        presenter.ApplyVisual();
    }

    private void CreatePCTables(Transform parent)
    {
        CreateObstacle(
            "PCTable_Top",
            parent,
            new Vector3(3.8f, 2.8f, 0f),
            new Vector2(8.2f, 0.85f),
            tableColor
        );
        CreateObstacle(
            "PCTable_Bottom",
            parent,
            new Vector3(3.8f, -0.7f, 0f),
            new Vector2(8.2f, 0.85f),
            tableColor
        );
        CreateObstacle(
            "PCTable_Expansion",
            parent,
            new Vector3(3.8f, -3.3f, 0f),
            new Vector2(8.2f, 0.85f),
            tableColor
        );
    }

    private void CreateUnlockableRooms(Transform parent)
    {
        if (unlockableRooms == null)
        {
            return;
        }

        foreach (UnlockableRoomDefinition room in unlockableRooms)
        {
            if (room == null)
            {
                continue;
            }

            if (!room.IsValid(out string error))
            {
                Debug.LogWarning(error);
                continue;
            }

            CreateRoomWalls(room, parent);
        }
    }

    private void CreateRoomWalls(
        UnlockableRoomDefinition room,
        Transform parent)
    {
        float halfWidth = room.size.x * 0.5f;
        float halfHeight = room.size.y * 0.5f;
        const float doorwayWidth = 1.2f;
        Vector3 center = new Vector3(room.center.x, room.center.y, 0f);
        Vector2 doorOffset = room.doorPosition - room.center;

        if (Mathf.Abs(doorOffset.x) > Mathf.Abs(doorOffset.y))
        {
            CreateRoomWithLeftDoor(
                room,
                parent,
                center,
                halfWidth,
                halfHeight,
                doorwayWidth
            );
            return;
        }

        CreateRoomWithBottomDoor(
            room,
            parent,
            center,
            halfWidth,
            halfHeight,
            doorwayWidth
        );
    }

    private void CreateRoomWithBottomDoor(
        UnlockableRoomDefinition room,
        Transform parent,
        Vector3 center,
        float halfWidth,
        float halfHeight,
        float doorwayWidth)
    {
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Top",
            parent,
            center + Vector3.up * halfHeight,
            new Vector2(room.size.x, wallThickness)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Left",
            parent,
            center + Vector3.left * halfWidth,
            new Vector2(wallThickness, room.size.y)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Right",
            parent,
            center + Vector3.right * halfWidth,
            new Vector2(wallThickness, room.size.y)
        );

        float segmentWidth = Mathf.Max(
            0.1f,
            (room.size.x - doorwayWidth) * 0.5f
        );
        Vector3 bottomCenter = center + Vector3.down * halfHeight;

        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Bottom_Left",
            parent,
            bottomCenter + Vector3.left *
            (doorwayWidth * 0.5f + segmentWidth * 0.5f),
            new Vector2(segmentWidth, wallThickness)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Bottom_Right",
            parent,
            bottomCenter + Vector3.right *
            (doorwayWidth * 0.5f + segmentWidth * 0.5f),
            new Vector2(segmentWidth, wallThickness)
        );
    }

    private void CreateRoomWithLeftDoor(
        UnlockableRoomDefinition room,
        Transform parent,
        Vector3 center,
        float halfWidth,
        float halfHeight,
        float doorwayWidth)
    {
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Top",
            parent,
            center + Vector3.up * halfHeight,
            new Vector2(room.size.x, wallThickness)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Bottom",
            parent,
            center + Vector3.down * halfHeight,
            new Vector2(room.size.x, wallThickness)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Right",
            parent,
            center + Vector3.right * halfWidth,
            new Vector2(wallThickness, room.size.y)
        );

        float segmentHeight = Mathf.Max(
            0.1f,
            (room.size.y - doorwayWidth) * 0.5f
        );
        Vector3 leftCenter = center + Vector3.left * halfWidth;

        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Left_Bottom",
            parent,
            leftCenter + Vector3.down *
            (doorwayWidth * 0.5f + segmentHeight * 0.5f),
            new Vector2(wallThickness, segmentHeight)
        );
        EnsureRoomObstacle(
            $"{room.roomId}_Wall_Left_Top",
            parent,
            leftCenter + Vector3.up *
            (doorwayWidth * 0.5f + segmentHeight * 0.5f),
            new Vector2(wallThickness, segmentHeight)
        );
    }

    private void EnsureRoomObstacle(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size)
    {
        CreateObstacle(
            objectName,
            parent,
            position,
            size,
            wallColor,
            false
        );
    }

    private void BuildUnlockableRoomContent()
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
        UnlockableRoomRuntimeBuilder runtimeBuilder =
            GetComponent<UnlockableRoomRuntimeBuilder>() ??
            gameObject.AddComponent<UnlockableRoomRuntimeBuilder>();

        runtimeBuilder.BuildRooms(
            unlockableRooms,
            navigation,
            roomManager,
            GetSquareSprite()
        );
    }

    private GameObject CreateObstacle(
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

        if (obstacle.GetComponent<BoxCollider2D>() == null)
        {
            obstacle.AddComponent<BoxCollider2D>();
        }

        return obstacle;
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
        Transform existingObject = parent.Find(objectName);
        GameObject visualObject = existingObject != null
            ? existingObject.gameObject
            : new GameObject(objectName);

        visualObject.transform.SetParent(parent);
        visualObject.transform.position = position;
        visualObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = visualObject.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = visualObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        YSortRenderer.SetSortingLayer(
            renderer,
            sortingOffset == -10000 ? "Background" : "World"
        );

        if (useYSorting)
        {
            YSortRenderer ySort = visualObject.GetComponent<YSortRenderer>();

            if (ySort == null)
            {
                ySort = visualObject.AddComponent<YSortRenderer>();
            }
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
        Transform existingPoint = parent.Find("SortingPoint");
        GameObject pointObject = existingPoint != null
            ? existingPoint.gameObject
            : new GameObject("SortingPoint");

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

        if (cameraFollow != null &&
            FindAnyObjectByType<ManagerModeController>() != null)
        {
            cameraFollow.SetTarget(null);
        }
        else if (cameraFollow != null && player != null)
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
                if (collider is CircleCollider2D circleCollider)
                {
                    circleCollider.radius = Mathf.Min(
                        circleCollider.radius,
                        0.38f
                    );
                }

                return;
            }
        }

        CircleCollider2D solidCollider = player.AddComponent<CircleCollider2D>();
        solidCollider.radius = 0.38f;
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
        BuildUnlockableRoomContent();
    }

    private void EnsureUnlockableRoomDefinitions()
    {
        roomSize.x = Mathf.Max(roomSize.x, 30f);

        unlockableRooms = new[]
        {
            new UnlockableRoomDefinition
            {
                roomId = "PrivateRoom01",
                displayName = "Приватная комната",
                requiredClubLevel = 3,
                unlockCost = 1500,
                center = new Vector2(12f, 3.2f),
                size = new Vector2(4.6f, 3f),
                doorPosition = new Vector2(9.7f, 3.2f),
                pcNames = new[] { "PC_10", "PC_11" },
                pcPositions = new[]
                {
                    new Vector2(11.3f, 3.55f),
                    new Vector2(12.7f, 3.55f)
                },
                approachPositions = new[]
                {
                    new Vector2(11.3f, 2.75f),
                    new Vector2(12.7f, 2.75f)
                },
                startingTier = PCTier.Gaming
            },
            new UnlockableRoomDefinition
            {
                roomId = "VIPRoom01",
                displayName = "VIP-комната",
                requiredClubLevel = 5,
                unlockCost = 4000,
                center = new Vector2(12f, -2.2f),
                size = new Vector2(4.6f, 3f),
                doorPosition = new Vector2(9.7f, -2.2f),
                pcNames = new[] { "PC_12", "PC_13" },
                pcPositions = new[]
                {
                    new Vector2(11.3f, -1.85f),
                    new Vector2(12.7f, -1.85f)
                },
                approachPositions = new[]
                {
                    new Vector2(11.3f, -2.75f),
                    new Vector2(12.7f, -2.75f)
                },
                startingTier = PCTier.Premium
            }
        };
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
        EnsureUnlockableRoomDefinitions();

        foreach (UnlockableRoomDefinition room in unlockableRooms)
        {
            if (room == null)
            {
                continue;
            }

            room.size.x = Mathf.Max(2f, room.size.x);
            room.size.y = Mathf.Max(2f, room.size.y);
            room.requiredClubLevel = Mathf.Max(1, room.requiredClubLevel);
            room.unlockCost = Mathf.Max(0, room.unlockCost);
        }
    }
}
