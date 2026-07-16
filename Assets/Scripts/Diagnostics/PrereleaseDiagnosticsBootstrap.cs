#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrereleaseDiagnosticsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene") return;

        if (GameplayTelemetryManager.Instance == null)
        {
            new GameObject("GameplayTelemetryManager")
                .AddComponent<GameplayTelemetryManager>();
        }

        if (PrereleaseQAPanel.Instance == null)
        {
            new GameObject("PrereleaseQAPanel")
                .AddComponent<PrereleaseQAPanel>();
        }
    }
}
#endif
