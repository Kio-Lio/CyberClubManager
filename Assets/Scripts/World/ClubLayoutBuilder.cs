using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClubLayoutBuilder : MonoBehaviour
{
    private const string LayoutRootName = "GeneratedClubLayout";
    private const string ObstacleLayerName = "ClubObstacle";

    private static readonly Vector2 DefaultRoomCenter = new(3.2f, 0f);
    private static readonly Vector2 DefaultRoomSize = new(24f, 10f);

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
    [SerializeField] private Vector2 roomCenter = new(3.2f, 0f);
    [SerializeField] private Vector2 roomSize = new(24f, 10f);
    [SerializeField] private float wallThickness = 0.35f;

    [Header("Camera Bounds")]
    [SerializeField, Min(0f)] private float cameraBoundsPadding = 0.25f;

    [Header("Unlockable Rooms")]
    [SerializeField] private UnlockableRoomDefinition[] unlockableRooms;

    [Header("Colors")]
    [SerializeField] private Color floorColor = new(0.09f, 0.105f, 0.13f);
    [SerializeField] private Color wallColor = new(0.18f, 0.205f, 0.25f);
    [SerializeField] private Color deskColor = new(0.12f, 0.135f, 0.17f);
    [SerializeField] private Color tableColor = new(0.13f, 0.145f, 0.18f);

    private Sprite squareSprite;

    public IReadOnlyList<UnlockableRoomDefinition> UnlockableRooms =>
        unlockableRooms;
    public Vector2 RoomCenter => roomCenter;
    public Vector2 RoomSize => roomSize;

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
        CreateVisualObject(
            "ExteriorVoid",
            parent,
            roomCenter,
            roomSize + new Vector2(8f, 6f),
            new Color(0.012f, 0.017f, 0.027f, 1f),
            -10020,
            false
        );
        CreateVisualObject(
            "FloorPerimeter",
            parent,
            roomCenter,
            roomSize + new Vector2(0.55f, 0.55f),
            new Color(0.035f, 0.045f, 0.065f, 1f),
            -10010,
            false
        );

        GameObject floor = CreateVisualObject(
            "Floor",
            parent,
            roomCenter,
            roomSize,
            floorColor,
            -10000,
            false
        );

        floor.layer = 0;
        CreateFloorDetails(parent);
    }

    private void CreateFloorDetails(Transform parent)
    {
        CreateVisualObject(
            "FloorZone_Reception",
            parent,
            new Vector3(-4.7f, 0f, 0f),
            new Vector2(7.55f, roomSize.y - 0.7f),
            new Color(0.098f, 0.112f, 0.137f, 1f),
            -9998,
            false
        );
        CreateVisualObject(
            "FloorZone_MainHall",
            parent,
            new Vector3(4.1f, 0f, 0f),
            new Vector2(9.55f, roomSize.y - 0.7f),
            new Color(0.108f, 0.122f, 0.15f, 1f),
            -9998,
            false
        );
        CreateVisualObject(
            "FloorZone_PrivateRooms",
            parent,
            new Vector3(12.05f, 0f, 0f),
            new Vector2(5.55f, roomSize.y - 0.7f),
            new Color(0.092f, 0.105f, 0.135f, 1f),
            -9998,
            false
        );
        CreateVisualObject(
            "FloorZone_ServiceLine",
            parent,
            new Vector3(-7.55f, 0.25f, 0f),
            new Vector2(1.25f, 8.3f),
            new Color(0.115f, 0.13f, 0.155f, 0.72f),
            -9995,
            false
        );

        float minimumX = roomCenter.x - roomSize.x * 0.5f + 0.35f;
        float maximumX = roomCenter.x + roomSize.x * 0.5f - 0.35f;
        float minimumY = roomCenter.y - roomSize.y * 0.5f + 0.35f;
        float maximumY = roomCenter.y + roomSize.y * 0.5f - 0.35f;
        Color jointColor = new(0.065f, 0.075f, 0.095f, 0.44f);

        int lineIndex = 0;
        int panelRow = 0;
        const float panelHeight = 2.35f;
        for (float centerY = minimumY + panelHeight * 0.5f;
             centerY < maximumY;
             centerY += panelHeight)
        {
            float stagger = panelRow % 2 == 0 ? 0f : 1.55f;
            for (float x = minimumX + 3.1f + stagger;
                 x < maximumX;
                 x += 3.1f)
            {
                CreateVisualObject(
                    $"FloorJoint_V_{lineIndex++:00}",
                    parent,
                    new Vector3(x, centerY, 0f),
                    new Vector2(0.022f, panelHeight - 0.28f),
                    jointColor,
                    -9996,
                    false
                );
            }
            panelRow++;
        }

        lineIndex = 0;
        for (float y = minimumY + panelHeight;
             y < maximumY;
             y += panelHeight)
        {
            (float center, float width)[] zoneSegments =
            {
                (-4.7f, 7.2f),
                (4.1f, 9.2f),
                (12.05f, 5.2f)
            };
            foreach ((float center, float width) segment in zoneSegments)
            {
                CreateVisualObject(
                    $"FloorJoint_H_{lineIndex++:00}",
                    parent,
                    new Vector3(segment.center, y, 0f),
                    new Vector2(segment.width, 0.022f),
                    jointColor,
                    -9996,
                    false
                );
            }
        }

        Color guideColor = new(0.12f, 0.30f, 0.38f, 0.34f);
        CreateVisualObject(
            "FloorGuide_Reception",
            parent,
            new Vector3(-0.92f, 0f, 0f),
            new Vector2(0.035f, roomSize.y - 1.15f),
            guideColor,
            -9993,
            false
        );
        CreateVisualObject(
            "FloorGuide_PrivateRooms",
            parent,
            new Vector3(9.25f, 0f, 0f),
            new Vector2(0.035f, roomSize.y - 1.15f),
            guideColor,
            -9993,
            false
        );
    }

    private void CreateOuterWalls(Transform parent)
    {
        float halfWidth = roomSize.x * 0.5f;
        float halfHeight = roomSize.y * 0.5f;

        CreateArchitecturalWall(
            "Wall_Top",
            parent,
            new Vector3(roomCenter.x, roomCenter.y + halfHeight, 0f),
            new Vector2(roomSize.x + wallThickness, wallThickness),
            false
        );
        const float entranceCenterX = -0.5f;
        const float entranceWidth = 1.2f;
        float entranceLeft = entranceCenterX - entranceWidth * 0.5f;
        float entranceRight = entranceCenterX + entranceWidth * 0.5f;
        float roomLeft = roomCenter.x - halfWidth;
        float roomRight = roomCenter.x + halfWidth;
        float leftSegmentWidth = entranceLeft - roomLeft;
        float rightSegmentWidth = roomRight - entranceRight;

        CreateArchitecturalWall(
            "Wall_Bottom_Left",
            parent,
            new Vector3(
                roomLeft + leftSegmentWidth * 0.5f,
                roomCenter.y - halfHeight,
                0f
            ),
            new Vector2(leftSegmentWidth, wallThickness),
            false
        );
        CreateArchitecturalWall(
            "Wall_Bottom_Right",
            parent,
            new Vector3(
                entranceRight + rightSegmentWidth * 0.5f,
                roomCenter.y - halfHeight,
                0f
            ),
            new Vector2(rightSegmentWidth, wallThickness),
            false
        );
        CreateArchitecturalWall(
            "Wall_Left",
            parent,
            new Vector3(roomLeft, roomCenter.y, 0f),
            new Vector2(wallThickness, roomSize.y),
            false
        );
        CreateArchitecturalWall(
            "Wall_Right",
            parent,
            new Vector3(roomRight, roomCenter.y, 0f),
            new Vector2(wallThickness, roomSize.y),
            false
        );
    }

    private void CreateAdminDesk(Transform parent)
    {
        Vector2 deskSize = new(3f, 1f);
        GameObject desk = CreateObstacle(
            "AdministratorDesk",
            parent,
            new Vector3(-4.55f, 2.95f, 0f),
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

            CreateVisualObject(
                $"{room.roomId}_Floor",
                parent,
                new Vector3(room.center.x, room.center.y, 0f),
                new Vector2(
                    room.size.x - wallThickness,
                    room.size.y - wallThickness
                ),
                new Color(0.096f, 0.108f, 0.138f, 1f),
                -9994,
                false
            );
            CreateRoomFloorDetails(room, parent);
            CreateRoomWalls(room, parent);
        }
    }

    private void CreateRoomFloorDetails(
        UnlockableRoomDefinition room,
        Transform parent)
    {
        CreateVisualObject(
            $"{room.roomId}_FloorInset",
            parent,
            room.center,
            new Vector2(room.size.x - 0.75f, room.size.y - 0.75f),
            new Color(0.108f, 0.12f, 0.15f, 0.82f),
            -9992,
            false
        );
        CreateVisualObject(
            $"{room.roomId}_FloorSpine",
            parent,
            new Vector3(room.center.x, room.center.y, 0f),
            new Vector2(room.size.x - 1.2f, 0.025f),
            new Color(0.12f, 0.28f, 0.34f, 0.28f),
            -9991,
            false
        );
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
        CreateArchitecturalWall(
            objectName,
            parent,
            position,
            size,
            false
        );
    }

    private GameObject CreateArchitecturalWall(
        string objectName,
        Transform parent,
        Vector3 position,
        Vector2 size,
        bool useYSorting = false)
    {
        GameObject wall = CreateObstacle(
            objectName,
            parent,
            position,
            size,
            wallColor,
            useYSorting
        );
        ApplyWallSurface(wall);
        return wall;
    }

    private void ApplyWallSurface(GameObject wall)
    {
        Transform existingInset = wall.transform.Find("WallInset");
        GameObject insetObject = existingInset != null
            ? existingInset.gameObject
            : new GameObject("WallInset");
        insetObject.transform.SetParent(wall.transform, false);
        insetObject.transform.localPosition = Vector3.zero;
        insetObject.transform.localScale = new Vector3(0.82f, 0.56f, 1f);

        SpriteRenderer inset = insetObject.GetComponent<SpriteRenderer>();
        if (inset == null)
        {
            inset = insetObject.AddComponent<SpriteRenderer>();
        }
        inset.sprite = GetSquareSprite();
        inset.color = new Color(0.255f, 0.285f, 0.335f, 0.72f);
        YSortRenderer.SetSortingLayer(inset, "Background");
        inset.sortingOrder = 5001;
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
            sortingOffset <= -9000 ? "Background" : "World"
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
        ConfigureTerminalLayout();

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

    private static void ConfigureTerminalLayout()
    {
        (string name, Vector3 position)[] terminalLayout =
        {
            ("ClubResearchTerminal", new Vector3(-7.55f, 3.65f, 0f)),
            ("InternetProviderTerminal", new Vector3(-7.55f, 2.55f, 0f)),
            ("MarketingTerminal", new Vector3(-7.55f, 1.45f, 0f)),
            ("ConsumableStockTerminal", new Vector3(-7.55f, 0.35f, 0f)),
            ("PricingTerminal", new Vector3(-7.55f, -0.75f, 0f)),
            ("MaintenanceTerminal", new Vector3(-7.55f, -1.85f, 0f)),
            ("PCExpansionTerminal", new Vector3(-7.55f, -2.95f, 0f))
        };

        foreach ((string name, Vector3 position) terminal in terminalLayout)
        {
            GameObject terminalObject = GameObject.Find(terminal.name);
            if (terminalObject == null)
            {
                continue;
            }

            terminalObject.transform.position = terminal.position;
            terminalObject.transform.localScale =
                new Vector3(0.7f, 0.9f, 1f);
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

        boundsObject.transform.localPosition = roomCenter;

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
        roomCenter = DefaultRoomCenter;
        roomSize = DefaultRoomSize;

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
