using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClubWorldVisualBootstrap : MonoBehaviour
{
    private const string GameplaySceneName = "SampleScene";

    [SerializeField] private bool showDebugVisuals;

    public bool ShowDebugVisuals => DebugVisualsAreAllowed &&
        showDebugVisuals;

    private static bool DebugVisualsAreAllowed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    private static void TryInstall(Scene scene)
    {
        if (scene.name != GameplaySceneName ||
            FindAnyObjectByType<ClubWorldVisualBootstrap>() != null)
        {
            return;
        }

        new GameObject("ClubWorldVisualBootstrap")
            .AddComponent<ClubWorldVisualBootstrap>();
    }

    private void OnEnable()
    {
        PC.PCRegistered += StylePC;
    }

    private void Start()
    {
        ApplyVisuals();
        InvokeRepeating(nameof(ApplyVisuals), 1f, 1f);
    }

    private void OnDisable()
    {
        PC.PCRegistered -= StylePC;
        CancelInvoke();
    }

    private void ApplyVisuals()
    {
        foreach (PC pc in FindObjectsByType<PC>())
        {
            StylePC(pc);
        }

        foreach (Client client in FindObjectsByType<Client>())
        {
            StyleClient(client);
        }

        foreach (CleanerAgent cleaner in FindObjectsByType<CleanerAgent>())
        {
            StyleCleaner(cleaner);
        }

        foreach (TrashItem trash in FindObjectsByType<TrashItem>())
        {
            StyleTrash(trash);
        }

        foreach (RoomDoor door in FindObjectsByType<RoomDoor>())
        {
            StyleDoor(door);
        }

        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>())
        {
            if (behaviour is not IInteractable ||
                behaviour is PC ||
                !behaviour.GetType().Name.EndsWith("Terminal"))
            {
                continue;
            }

            if (behaviour.GetComponent<TerminalVisualPresenter>() == null)
            {
                behaviour.gameObject.AddComponent<TerminalVisualPresenter>();
            }
        }

        ApplyDebugVisualPolicy();
    }

    private void ApplyDebugVisualPolicy()
    {
        bool visible = ShowDebugVisuals;

        foreach (ClientNavigationNode node in
                 FindObjectsByType<ClientNavigationNode>())
        {
            SetRenderersVisible(node.gameObject, visible);
        }

        foreach (CameraBounds2D bounds in FindObjectsByType<CameraBounds2D>())
        {
            SetRenderersVisible(bounds.gameObject, visible);
        }
    }

    private static void SetRenderersVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in
                 target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.enabled = visible;
        }
    }

    private static void StylePC(PC pc)
    {
        if (pc != null && pc.GetComponent<PCVisualPresenter>() == null)
        {
            pc.gameObject.AddComponent<PCVisualPresenter>();
        }
    }

    private static void StyleClient(Client client)
    {
        if (client == null)
        {
            return;
        }

        client.transform.localScale = Vector3.one;
        CharacterVisualPresenter presenter =
            client.GetComponent<CharacterVisualPresenter>() ??
            client.gameObject.AddComponent<CharacterVisualPresenter>();
        presenter.ConfigureClient(client.Type);
    }

    private static void StyleCleaner(CleanerAgent cleaner)
    {
        if (cleaner == null)
        {
            return;
        }

        cleaner.transform.localScale = Vector3.one;
        CharacterVisualPresenter presenter =
            cleaner.GetComponent<CharacterVisualPresenter>() ??
            cleaner.gameObject.AddComponent<CharacterVisualPresenter>();
        presenter.ConfigureCleaner();
    }

    private static void StyleTrash(TrashItem trash)
    {
        if (trash != null &&
            trash.GetComponent<TrashVisualPresenter>() == null)
        {
            trash.gameObject.AddComponent<TrashVisualPresenter>();
        }
    }

    private static void StyleDoor(RoomDoor door)
    {
        if (door == null)
        {
            return;
        }

        RoomDoorVisualPresenter presenter =
            door.GetComponent<RoomDoorVisualPresenter>() ??
            door.gameObject.AddComponent<RoomDoorVisualPresenter>();
        presenter.RefreshState();
    }
}
