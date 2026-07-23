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
}
