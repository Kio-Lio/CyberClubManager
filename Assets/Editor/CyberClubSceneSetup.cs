using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CyberClubSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Cyber Club/Apply Prototype Setup")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureObjectWithComponent<EconomyManager>("EconomyManager", Vector3.zero);
        EnsureObjectWithComponent<ClubReputationManager>("ClubReputationManager", Vector3.zero);
        EnsureObjectWithComponent<GameDayManager>("GameDayManager", Vector3.zero);
        EnsureObjectWithComponent<BankruptcyManager>("BankruptcyManager", Vector3.zero);
        EnsureObjectWithComponent<PCExpansionManager>("PCExpansionManager", Vector3.zero);
        EnsureObjectWithComponent<DailyGoalManager>("DailyGoalManager", Vector3.zero);
        EnsureObjectWithComponent<ClubProgressionManager>("ClubProgressionManager", Vector3.zero);
        EnsureObjectWithComponent<TechnicianManager>("TechnicianManager", Vector3.zero);
        EnsureObjectWithComponent<ClubCleanlinessManager>("ClubCleanlinessManager", Vector3.zero);
        EnsureObjectWithComponent<CleanerManager>("CleanerManager", Vector3.zero);
        EnsureObjectWithComponent<PricingManager>("PricingManager", Vector3.zero);
        EnsureObjectWithComponent<ConsumableInventoryManager>("ConsumableInventoryManager", Vector3.zero);
        EnsureObjectWithComponent<MarketingManager>("MarketingManager", Vector3.zero);
        EnsureObjectWithComponent<DailyFinancialReportManager>("DailyFinancialReportManager", Vector3.zero);
        EnsureObjectWithComponent<DemandAnalyticsManager>("DemandAnalyticsManager", Vector3.zero);
        EnsureObjectWithComponent<RoomUnlockManager>("RoomUnlockManager", Vector3.zero);
        EnsureObjectWithComponent<SaveManager>("SaveManager", Vector3.zero);
        EnsureObjectWithComponent<ClubLayoutBuilder>("ClubLayoutBuilder", Vector3.zero);
        EnsurePlayerPhysicsAndVisuals();
        CreatePCs();
        NormalizeExpansionPCs();
        EnsureNavigationNetwork();
        EnsureExpansionTerminal();
        EnsureMaintenanceTerminal();
        EnsurePricingTerminal();
        EnsureConsumableStockTerminal();
        EnsureMarketingTerminal();
        EnsureClientSpawner();
        EnsureClubHUDCanvas();
        EnsurePauseMenuController();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureExpansionTerminal()
    {
        GameObject terminalObject = GameObject.Find("PCExpansionTerminal");
        if (terminalObject == null)
        {
            terminalObject = new GameObject("PCExpansionTerminal");
        }

        terminalObject.transform.position = new Vector3(-2f, 2f, 0f);
        terminalObject.transform.localScale = Vector3.one;

        SpriteRenderer renderer = terminalObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = terminalObject.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = CreateRuntimeSquareSprite();
        }

        renderer.color = new Color(0.2f, 0.8f, 0.3f);
        YSortRenderer.Ensure(terminalObject, 12, -0.45f);

        BoxCollider2D collider = terminalObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = terminalObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = false;

        if (terminalObject.GetComponent<PCExpansionTerminal>() == null)
        {
            terminalObject.AddComponent<PCExpansionTerminal>();
        }
    }

    private static void EnsureMaintenanceTerminal()
    {
        GameObject terminalObject = GameObject.Find("MaintenanceTerminal");
        if (terminalObject == null)
        {
            terminalObject = new GameObject("MaintenanceTerminal");
        }

        terminalObject.transform.position = new Vector3(-2.6f, 3.7f, 0f);
        terminalObject.transform.localScale = new Vector3(0.7f, 0.9f, 1f);

        SpriteRenderer renderer = terminalObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = terminalObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = CreateRuntimeSquareSprite();
        renderer.color = new Color(0.15f, 0.45f, 0.85f);
        YSortRenderer.Ensure(terminalObject, 12, -0.45f);

        BoxCollider2D collider = terminalObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = terminalObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        if (terminalObject.GetComponent<PCMaintenanceTerminal>() == null)
        {
            terminalObject.AddComponent<PCMaintenanceTerminal>();
        }
    }

    private static void EnsurePauseMenuController()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
        {
            Debug.LogWarning("Player не найден. Меню паузы не добавлено.");
            return;
        }

        if (playerObject.GetComponent<PauseMenuController>() == null)
        {
            playerObject.AddComponent<PauseMenuController>();
        }
    }

    private static void EnsurePricingTerminal()
    {
        GameObject terminalObject = GameObject.Find("PricingTerminal");
        if (terminalObject == null)
        {
            terminalObject = new GameObject("PricingTerminal");
        }

        terminalObject.transform.position = new Vector3(-3.7f, 3.7f, 0f);
        terminalObject.transform.localScale = new Vector3(0.7f, 0.9f, 1f);
        SpriteRenderer renderer = terminalObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = terminalObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = CreateRuntimeSquareSprite();
        renderer.color = new Color(0.7f, 0.3f, 0.9f);
        YSortRenderer.Ensure(terminalObject, 12, -0.45f);
        BoxCollider2D collider = terminalObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = terminalObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        if (terminalObject.GetComponent<PricingTerminal>() == null)
        {
            terminalObject.AddComponent<PricingTerminal>();
        }
    }

    private static void EnsureConsumableStockTerminal()
    {
        GameObject terminalObject = GameObject.Find("ConsumableStockTerminal");
        if (terminalObject == null)
        {
            terminalObject = new GameObject("ConsumableStockTerminal");
        }

        terminalObject.transform.position = new Vector3(-4.8f, 3.7f, 0f);
        terminalObject.transform.localScale = new Vector3(0.7f, 0.9f, 1f);

        SpriteRenderer renderer = terminalObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = terminalObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = CreateRuntimeSquareSprite();
        renderer.color = new Color(0.95f, 0.45f, 0.08f);
        YSortRenderer.Ensure(terminalObject, 12, -0.45f);

        BoxCollider2D collider = terminalObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = terminalObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        if (terminalObject.GetComponent<ConsumableStockTerminal>() == null)
        {
            terminalObject.AddComponent<ConsumableStockTerminal>();
        }
    }

    private static void EnsureMarketingTerminal()
    {
        GameObject terminalObject = GameObject.Find("MarketingTerminal");
        if (terminalObject == null)
        {
            terminalObject = new GameObject("MarketingTerminal");
        }

        terminalObject.transform.position = new Vector3(-5.9f, 3.7f, 0f);
        terminalObject.transform.localScale = new Vector3(0.7f, 0.9f, 1f);

        SpriteRenderer renderer = terminalObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = terminalObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = CreateRuntimeSquareSprite();
        renderer.color = new Color(0.95f, 0.85f, 0.1f);
        YSortRenderer.Ensure(terminalObject, 12, -0.45f);

        BoxCollider2D collider = terminalObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = terminalObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        if (terminalObject.GetComponent<MarketingTerminal>() == null)
        {
            terminalObject.AddComponent<MarketingTerminal>();
        }
    }

    private static void EnsureClubHUDCanvas()
    {
        GameObject hudCanvasObject = GameObject.Find("ClubHUDCanvas");
        if (hudCanvasObject == null)
        {
            hudCanvasObject = new GameObject("ClubHUDCanvas");
        }

        if (hudCanvasObject.GetComponent<ClubHUDCanvas>() == null)
        {
            hudCanvasObject.AddComponent<ClubHUDCanvas>();
        }

        if (hudCanvasObject.GetComponent<ClientFeedbackUI>() == null)
        {
            hudCanvasObject.AddComponent<ClientFeedbackUI>();
        }

        if (hudCanvasObject.GetComponent<PCMaintenancePanel>() == null)
        {
            hudCanvasObject.AddComponent<PCMaintenancePanel>();
        }

        if (hudCanvasObject.GetComponent<PricingPanel>() == null)
        {
            hudCanvasObject.AddComponent<PricingPanel>();
        }

        if (hudCanvasObject.GetComponent<ConsumableStockPanel>() == null)
        {
            hudCanvasObject.AddComponent<ConsumableStockPanel>();
        }

        if (hudCanvasObject.GetComponent<DailyFinancialReportPanel>() == null)
        {
            hudCanvasObject.AddComponent<DailyFinancialReportPanel>();
        }

        if (hudCanvasObject.GetComponent<MarketingPanel>() == null)
        {
            hudCanvasObject.AddComponent<MarketingPanel>();
        }

        if (hudCanvasObject.GetComponent<DemandAnalyticsPanel>() == null)
        {
            hudCanvasObject.AddComponent<DemandAnalyticsPanel>();
        }
    }

    private static void EnsureObjectWithComponent<T>(string name, Vector3 position) where T : Component
    {
        GameObject target = GameObject.Find(name);
        if (target == null)
        {
            target = new GameObject(name);
            target.transform.position = position;
        }

        if (target.GetComponent<T>() == null)
        {
            target.AddComponent<T>();
        }
    }

    private static void CreatePCs()
    {
        GameObject legacyPc = GameObject.Find("PC_Test");
        if (legacyPc != null)
        {
            Object.DestroyImmediate(legacyPc);
        }

        Vector3[] pcPositions =
        {
            new Vector3(1.2f, 2.8f, 0f),
            new Vector3(3.8f, 2.8f, 0f),
            new Vector3(6.4f, 2.8f, 0f),
            new Vector3(1.2f, -0.7f, 0f),
            new Vector3(3.8f, -0.7f, 0f),
        };

        for (int i = 0; i < pcPositions.Length; i++)
        {
            GameObject pcObject = GameObject.Find($"PC_{i + 1:00}");
            if (pcObject == null)
            {
                pcObject = new GameObject($"PC_{i + 1:00}");
            }

            pcObject.transform.position = pcPositions[i];
            pcObject.transform.localScale = Vector3.one;

            SpriteRenderer renderer = pcObject.GetComponent<SpriteRenderer>() ?? pcObject.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateRuntimeSquareSprite();
            }

            renderer.color = Color.white;

            BoxCollider2D collider = pcObject.GetComponent<BoxCollider2D>() ?? pcObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;

            PC pc = pcObject.GetComponent<PC>();
            if (pc == null)
            {
                pc = pcObject.AddComponent<PC>();
            }

            pc.ConfigureYSorting();
        }
    }

    private static void NormalizeExpansionPCs()
    {
        PCExpansionManager expansionManager =
            Object.FindAnyObjectByType<PCExpansionManager>();

        if (expansionManager == null)
        {
            return;
        }

        expansionManager.NormalizeExistingExpansionPCs();
        EditorUtility.SetDirty(expansionManager);
    }

    private static void EnsureNavigationNetwork()
    {
        ClientNavigationManager.EnsureRuntimeGraph();
    }

    private static void EnsureClientSpawner()
    {
        GameObject spawnerObject = GameObject.Find("ClientSpawner");
        if (spawnerObject == null)
        {
            spawnerObject = new GameObject("ClientSpawner");
        }

        ClientSpawner spawner =
            spawnerObject.GetComponent<ClientSpawner>() ??
            spawnerObject.AddComponent<ClientSpawner>();
        spawner.ApplyLayoutPositions();
    }

    private static void EnsurePlayerPhysicsAndVisuals()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
        {
            Debug.LogWarning("Player не найден. Физика игрока не настроена.");
            return;
        }

        Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>() ??
            playerObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        EnsureSolidPlayerCollider(playerObject);

        SpriteRenderer renderer = playerObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            YSortRenderer.Ensure(playerObject, 20, -0.45f);
        }

        Camera mainCamera = Camera.main ?? Object.FindAnyObjectByType<Camera>();
        if (mainCamera != null)
        {
            CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>() ??
                mainCamera.gameObject.AddComponent<CameraFollow>();
            cameraFollow.SetTarget(playerObject.transform);

            CameraBounds2D cameraBounds =
                Object.FindAnyObjectByType<CameraBounds2D>();
            if (cameraBounds != null)
            {
                cameraFollow.SetBounds(cameraBounds);
            }
        }
    }

    private static void EnsureSolidPlayerCollider(GameObject playerObject)
    {
        foreach (Collider2D collider in
                 playerObject.GetComponents<Collider2D>())
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

        CircleCollider2D solidCollider =
            playerObject.AddComponent<CircleCollider2D>();
        solidCollider.radius = 0.38f;
        solidCollider.isTrigger = false;
    }

    private static Sprite CreateRuntimeSquareSprite()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }
}
