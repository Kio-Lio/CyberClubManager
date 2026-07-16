using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class InternetProviderPanel : MonoBehaviour
{
    private static readonly InternetPlanType[] PlanTypes =
    {
        InternetPlanType.Basic,
        InternetPlanType.Gaming,
        InternetPlanType.Professional
    };

    public static InternetProviderPanel Instance { get; private set; }

    private GameObject rootObject;
    private Text activePlanText;
    private Text statusText;
    private readonly Text[] planTexts = new Text[3];
    private readonly Button[] planButtons = new Button[3];
    private Button closeButton;
    private bool isOpen;
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
        if (InternetProviderManager.Instance != null)
        {
            InternetProviderManager.Instance.StatusChanged += RefreshView;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }
    }

    private void OnDestroy()
    {
        if (InternetProviderManager.Instance != null)
        {
            InternetProviderManager.Instance.StatusChanged -= RefreshView;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
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

    public void Open()
    {
        if (isOpen || InternetProviderManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen) ||
            (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen) ||
            (ConsumableStockPanel.Instance != null && ConsumableStockPanel.Instance.IsOpen) ||
            (MarketingPanel.Instance != null && MarketingPanel.Instance.IsOpen) ||
            (DemandAnalyticsPanel.Instance != null && DemandAnalyticsPanel.Instance.IsOpen) ||
            (DailyFinancialReportPanel.Instance != null && DailyFinancialReportPanel.Instance.IsOpen) ||
            (ClubRandomEventPanel.Instance != null && ClubRandomEventPanel.Instance.IsOpen))
        {
            return;
        }

        isOpen = true;
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

    private void SelectPlan(int index)
    {
        if (index < 0 || index >= PlanTypes.Length)
        {
            return;
        }

        InternetProviderManager manager = InternetProviderManager.Instance;
        if (manager == null)
        {
            return;
        }

        bool changed = manager.TrySwitchPlan(PlanTypes[index]);
        statusText.text = changed
            ? "Тариф успешно подключен."
            : manager.LastStatusMessage;
        RefreshView();
    }

    private void OnMoneyChanged(int _) => RefreshView();

    private void RefreshView()
    {
        InternetProviderManager manager = InternetProviderManager.Instance;
        if (manager == null || activePlanText == null)
        {
            return;
        }

        InternetPlanDefinition active = manager.GetActivePlan();
        activePlanText.text = active == null
            ? "Текущий тариф: недоступен"
            : $"Текущий тариф: {active.DisplayName}\n" +
              $"Скорость сессий: ×{active.SessionSpeedMultiplier:F2} | " +
              $"Надежность: {active.Reliability * 100f:F1}% | " +
              $"Абонплата: {active.DailyCost} ₽/день";
        statusText.text = manager.LastStatusMessage;

        int balance = EconomyManager.Instance != null
            ? EconomyManager.Instance.Money
            : 0;
        for (int index = 0; index < PlanTypes.Length; index++)
        {
            InternetPlanDefinition plan = manager.GetPlan(PlanTypes[index]);
            if (plan == null)
            {
                planTexts[index].text = "Тариф недоступен";
                planButtons[index].interactable = false;
                continue;
            }

            planTexts[index].text =
                $"{plan.DisplayName}\n" +
                $"Подключение: {plan.ConnectionCost} ₽ | " +
                $"Абонплата: {plan.DailyCost} ₽/день\n" +
                $"Скорость: ×{plan.SessionSpeedMultiplier:F2} | " +
                $"Надежность: {plan.Reliability * 100f:F1}%";
            planButtons[index].interactable =
                manager.ActivePlan != plan.PlanType &&
                balance >= plan.ConnectionCost;
        }
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("InternetProviderPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.76f);

        GameObject panel = new GameObject("InternetProviderPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 850f);
        panel.GetComponent<Image>().color = new Color(0.025f, 0.12f, 0.14f, 0.99f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "ИНТЕРНЕТ-ПРОВАЙДЕР", 30, 54f, FontStyle.Bold, TextAnchor.MiddleCenter);
        activePlanText = CreateLabel(panel.transform, string.Empty, 21, 90f, FontStyle.Normal, TextAnchor.MiddleCenter);
        for (int index = 0; index < PlanTypes.Length; index++)
        {
            CreatePlanRow(panel.transform, index);
        }

        statusText = CreateLabel(panel.transform, string.Empty, 18, 42f, FontStyle.Normal, TextAnchor.MiddleCenter);
        closeButton = CreateButton(panel.transform, "Закрыть", Close, 58f, 180f);
    }

    private void CreatePlanRow(Transform parent, int index)
    {
        GameObject row = new GameObject("PlanRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 145f;
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.padding = new RectOffset(16, 16, 10, 10);
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        planTexts[index] = CreateLabel(
            row.transform,
            string.Empty,
            20,
            125f,
            FontStyle.Normal,
            TextAnchor.MiddleLeft
        );
        LayoutElement textLayout = planTexts[index].GetComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.preferredWidth = 540f;

        int selectedIndex = index;
        planButtons[index] = CreateButton(
            row.transform,
            "Подключить",
            () => SelectPlan(selectedIndex),
            58f,
            170f
        );
    }

    private Text CreateLabel(
        Transform parent,
        string content,
        int fontSize,
        float height,
        FontStyle fontStyle,
        TextAnchor alignment)
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

    private Button CreateButton(
        Transform parent,
        string caption,
        UnityEngine.Events.UnityAction action,
        float height,
        float width)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.04f, 0.45f, 0.52f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.08f, 0.66f, 0.73f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.02f, 0.25f, 0.30f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        LayoutElement buttonLayout = buttonObject.GetComponent<LayoutElement>();
        buttonLayout.preferredHeight = height;
        buttonLayout.preferredWidth = width;
        buttonLayout.flexibleWidth = 0f;

        Text text = CreateLabel(buttonObject.transform, caption, 19, height, FontStyle.Bold, TextAnchor.MiddleCenter);
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
