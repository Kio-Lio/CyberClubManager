using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ClubWorldVisualBootstrap : MonoBehaviour
{
    private const string GameplaySceneName = "SampleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name != GameplaySceneName ||
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
    }

    private static void StylePC(PC pc)
    {
        if (pc != null && pc.GetComponent<PCVisualPresenter>() == null)
        {
            pc.gameObject.AddComponent<PCVisualPresenter>();
        }
    }
}
