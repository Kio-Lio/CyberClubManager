using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class DemandAnalyticsPanel : MonoBehaviour
{
    public static DemandAnalyticsPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text reportText;
    private Button previousDayButton;
    private Button returnButton;
    private Button closeButton;
    private bool isOpen;
    private bool showingLastDay;
    private float previousTimeScale = 1f;
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private Font runtimeFont;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildInterface();
        rootObject.SetActive(false);
    }

    private void Start()
    {
        if (DemandAnalyticsManager.Instance != null)
        {
            DemandAnalyticsManager.Instance.StatusChanged += RefreshView;
        }
    }

    private void OnDestroy()
    {
        if (DemandAnalyticsManager.Instance != null)
        {
            DemandAnalyticsManager.Instance.StatusChanged -= RefreshView;
        }

        if (isOpen)
        {
            RestoreGameplayState();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Open(bool showLastDay)
    {
        if (isOpen || DemandAnalyticsManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen) ||
            (ConsumableStockPanel.Instance != null && ConsumableStockPanel.Instance.IsOpen) ||
            (MarketingPanel.Instance != null && MarketingPanel.Instance.IsOpen) ||
            (DailyFinancialReportPanel.Instance != null && DailyFinancialReportPanel.Instance.IsOpen))
        {
            return;
        }

        isOpen = true;
        showingLastDay = showLastDay && DemandAnalyticsManager.Instance.HasLastReport;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        CaptureCursorState();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureEventSystem();
        rootObject.SetActive(true);
        rootObject.transform.SetAsLastSibling();
        RefreshView();
        StartCoroutine(SelectDefaultButtonNextFrame());
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        rootObject.SetActive(false);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        RestoreGameplayState();
    }

    private void ToggleReport()
    {
        DemandAnalyticsManager manager = DemandAnalyticsManager.Instance;
        if (manager == null || !manager.HasLastReport)
        {
            return;
        }

        showingLastDay = !showingLastDay;
        RefreshView();
    }

    private void ReturnToPricing()
    {
        Close();
        PricingPanel.Instance?.Open();
    }

    private void RefreshView()
    {
        if (!isOpen || reportText == null)
        {
            return;
        }

        DemandAnalyticsManager manager = DemandAnalyticsManager.Instance;
        if (manager == null)
        {
            reportText.text = "АНАЛИТИКА СПРОСА\nНедоступна";
            return;
        }

        DemandAnalyticsReportData report = showingLastDay
            ? manager.LastReport
            : manager.CurrentReport;
        if (report == null)
        {
            reportText.text = "АНАЛИТИКА СПРОСА\nНет данных за прошлый день";
            return;
        }

        string period = showingLastDay ? "ПРОШЛЫЙ ДЕНЬ" : "ТЕКУЩИЙ ДЕНЬ";
        reportText.text =
            $"АНАЛИТИКА СПРОСА - {period} {report.day}\n\n" +
            BuildTierText("BASIC", report.basic) + "\n\n" +
            BuildTierText("GAMING", report.gaming) + "\n\n" +
            BuildTierText("PREMIUM", report.premium) + "\n\n" +
            $"Переполнение очереди: {report.queueOverflowClients}";

        previousDayButton.interactable = manager.HasLastReport;
        Text buttonText = previousDayButton.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            buttonText.text = showingLastDay ? "Текущий день" : "Прошлый день";
        }
    }

    private static string BuildTierText(
        string title,
        DemandTierAnalyticsData data)
    {
        if (data == null)
        {
            return $"{title}\nНет данных";
        }

        return
            $"{title} | ПК: {CountAccessiblePCs(data.tier)}\n" +
            $"Загрузка: {data.UtilizationPercent:F0}%\n" +
            $"Сессии: {data.completedSessions}\n" +
            $"Выручка: {data.sessionRevenue} ₽\n" +
            $"Средний чек: {data.AverageSessionRevenue} ₽\n" +
            $"Ушли из-за цены: {data.priceLostClients}\n" +
            $"Упущено: примерно {data.estimatedPriceLostRevenue} ₽\n" +
            $"Не дождались места: {data.capacityLostClients}";
    }

    private static int CountAccessiblePCs(PCTier tier)
    {
        int count = 0;
        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (pc != null && pc.Tier == tier && pc.HasRoomAccess)
            {
                count++;
            }
        }

        return count;
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("DemandAnalyticsPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObject panel = new GameObject("DemandAnalyticsPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 960f);
        panel.GetComponent<Image>().color = new Color(0.07f, 0.035f, 0.12f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        reportText = CreateLabel(panel.transform, string.Empty, 18, 740f, TextAnchor.UpperLeft, FontStyle.Normal);
        GameObject buttonRow = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonRow.transform.SetParent(panel.transform, false);
        buttonRow.GetComponent<LayoutElement>().preferredHeight = 58f;
        HorizontalLayoutGroup rowLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        previousDayButton = CreateButton(buttonRow.transform, "Прошлый день", ToggleReport);
        returnButton = CreateButton(buttonRow.transform, "К тарифам", ReturnToPricing);
        closeButton = CreateButton(buttonRow.transform, "Закрыть", Close);
    }

    private Text CreateLabel(Transform parent, string content, int fontSize, float height, TextAnchor alignment, FontStyle fontStyle)
    {
        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private Button CreateButton(Transform parent, string caption, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.33f, 0.16f, 0.48f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.50f, 0.27f, 0.68f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.18f, 0.08f, 0.28f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Text text = CreateLabel(buttonObject.transform, caption, 18, 58f, TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        return button;
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = previousTimeScale;
        if (!cursorStateCaptured)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateCaptured = false;
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        cursorStateCaptured = true;
    }

    private IEnumerator SelectDefaultButtonNextFrame()
    {
        yield return null;
        if (isOpen && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            closeButton.Select();
        }
    }

    private static void EnsureEventSystem()
    {
        EventSystem system = EventSystem.current ?? FindAnyObjectByType<EventSystem>();
        if (system == null)
        {
            GameObject systemObject = new GameObject("EventSystem", typeof(EventSystem));
            system = systemObject.GetComponent<EventSystem>();
        }

        if (system.GetComponent<InputSystemUIInputModule>() == null)
        {
            system.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }
}
