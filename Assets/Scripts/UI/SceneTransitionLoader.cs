using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneTransitionLoader
{
    public const string MainMenuSceneName = "MainMenu";
    public const string GameSceneName = "SampleScene";
    public const float MinimumDisplayDuration = 0.4f;

    public static IEnumerator LoadSceneAsync(string sceneName)
    {
        Time.timeScale = 1f;
        GameObject overlay = CreateLoadingOverlay();
        float startedAt = Time.realtimeSinceStartup;
        AsyncOperation operation = SceneManager.LoadSceneAsync(
            sceneName,
            LoadSceneMode.Single
        );

        if (operation == null)
        {
            Object.Destroy(overlay);
            Debug.LogError($"Could not load scene: {sceneName}");
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f ||
               Time.realtimeSinceStartup - startedAt < MinimumDisplayDuration)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
    }

    public static void CloseGameplayPanels()
    {
        if (ClubRandomEventPanel.Instance != null &&
            ClubRandomEventPanel.Instance.IsOpen)
        {
            ClubRandomEventPanel.Instance.Close();
        }

        if (InternetProviderPanel.Instance != null &&
            InternetProviderPanel.Instance.IsOpen)
        {
            InternetProviderPanel.Instance.Close();
        }

        if (ClubResearchPanel.Instance != null &&
            ClubResearchPanel.Instance.IsOpen)
        {
            ClubResearchPanel.Instance.Close();
        }

        if (PCMaintenancePanel.Instance != null &&
            PCMaintenancePanel.Instance.IsOpen)
        {
            PCMaintenancePanel.Instance.Close();
        }

        if (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen)
        {
            PricingPanel.Instance.Close();
        }

        if (ConsumableStockPanel.Instance != null &&
            ConsumableStockPanel.Instance.IsOpen)
        {
            ConsumableStockPanel.Instance.Close();
        }

        if (DailyFinancialReportPanel.Instance != null &&
            DailyFinancialReportPanel.Instance.IsOpen)
        {
            DailyFinancialReportPanel.Instance.Close();
        }

        if (MarketingPanel.Instance != null && MarketingPanel.Instance.IsOpen)
        {
            MarketingPanel.Instance.Close();
        }

        if (DemandAnalyticsPanel.Instance != null &&
            DemandAnalyticsPanel.Instance.IsOpen)
        {
            DemandAnalyticsPanel.Instance.Close();
        }

        Time.timeScale = 1f;
    }

    private static GameObject CreateLoadingOverlay()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject(
            "SceneLoadingOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        GameUserSettings.ApplyCanvasScale(
            scaler,
            new Vector2(1920f, 1080f)
        );

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(Image)
        );
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.sizeDelta = Vector2.zero;
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundObject.GetComponent<Image>().color =
            new Color(0.015f, 0.025f, 0.035f, 1f);

        GameObject textObject = new GameObject(
            "LoadingText",
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(700f, 80f);
        textRect.anchoredPosition = Vector2.zero;

        Text loadingText = textObject.GetComponent<Text>();
        loadingText.font = font;
        loadingText.text = "ЗАГРУЗКА КЛУБА...";
        loadingText.fontSize = 32;
        loadingText.fontStyle = FontStyle.Bold;
        loadingText.alignment = TextAnchor.MiddleCenter;
        loadingText.color = new Color(0.35f, 0.95f, 0.75f, 1f);
        loadingText.raycastTarget = false;

        return canvasObject;
    }
}
