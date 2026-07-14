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
        EnsureObjectWithComponent<SaveManager>("SaveManager", Vector3.zero);
        CreatePCs();
        EnsureNavigationNetwork();
        EnsureExpansionTerminal();
        EnsureObjectWithComponent<ClientSpawner>("ClientSpawner", new Vector3(-6f, 0f, 0f));
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
            new Vector3(2f, 2f, 0f),
            new Vector3(4f, 2f, 0f),
            new Vector3(6f, 2f, 0f),
            new Vector3(2f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
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
            collider.isTrigger = false;

            if (pcObject.GetComponent<PC>() == null)
            {
                pcObject.AddComponent<PC>();
            }
        }
    }

    private static void EnsureNavigationNetwork()
    {
        ClientNavigationManager.EnsureRuntimeGraph();
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
