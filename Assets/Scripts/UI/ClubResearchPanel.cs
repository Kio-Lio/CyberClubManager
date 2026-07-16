using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class ClubResearchPanel : MonoBehaviour
{
    private static readonly ClubResearchType[] ResearchTypes =
    {
        ClubResearchType.ReliableComponents,
        ClubResearchType.DurableEquipment,
        ClubResearchType.EfficientCleaning,
        ClubResearchType.WholesalePurchasing,
        ClubResearchType.BrandPromotion,
        ClubResearchType.NetworkOptimization,
        ClubResearchType.EnergyEfficiency
    };

    public static ClubResearchPanel Instance { get; private set; }

    private readonly Text[] researchTexts = new Text[ResearchTypes.Length];
    private readonly Button[] researchButtons = new Button[ResearchTypes.Length];
    private GameObject rootObject;
    private Text statusText;
    private Button closeButton;
    private Font runtimeFont;
    private bool isOpen;
    private float previousTimeScale = 1f;
    private bool cursorStateCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

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
        if (ClubResearchManager.Instance != null)
            ClubResearchManager.Instance.StatusChanged += RefreshView;
        if (ClubProgressionManager.Instance != null)
            ClubProgressionManager.Instance.StatusChanged += RefreshView;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
    }

    private void OnDestroy()
    {
        if (ClubResearchManager.Instance != null)
            ClubResearchManager.Instance.StatusChanged -= RefreshView;
        if (ClubProgressionManager.Instance != null)
            ClubProgressionManager.Instance.StatusChanged -= RefreshView;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        if (isOpen) RestoreGameplayState();
        if (Instance == this) Instance = null;
    }

    public void Open()
    {
        if (isOpen || ClubResearchManager.Instance == null ||
            (PauseMenuController.Instance != null && PauseMenuController.Instance.IsMenuOpen) ||
            (PCMaintenancePanel.Instance != null && PCMaintenancePanel.Instance.IsOpen) ||
            (PricingPanel.Instance != null && PricingPanel.Instance.IsOpen) ||
            (ConsumableStockPanel.Instance != null && ConsumableStockPanel.Instance.IsOpen) ||
            (MarketingPanel.Instance != null && MarketingPanel.Instance.IsOpen) ||
            (DemandAnalyticsPanel.Instance != null && DemandAnalyticsPanel.Instance.IsOpen) ||
            (InternetProviderPanel.Instance != null && InternetProviderPanel.Instance.IsOpen) ||
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
        if (!isOpen) return;
        isOpen = false;
        rootObject.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        RestoreGameplayState();
    }

    private void PurchaseResearch(int index)
    {
        if (index < 0 || index >= ResearchTypes.Length) return;
        ClubResearchManager.Instance?.TryPurchaseResearch(ResearchTypes[index]);
        RefreshView();
    }

    private void OnMoneyChanged(int _) => RefreshView();

    private void RefreshView()
    {
        ClubResearchManager manager = ClubResearchManager.Instance;
        if (manager == null || statusText == null) return;

        int balance = EconomyManager.Instance != null ? EconomyManager.Instance.Money : 0;
        int clubLevel = ClubProgressionManager.Instance != null
            ? ClubProgressionManager.Instance.Level : 1;
        statusText.text = manager.LastStatusMessage;

        for (int index = 0; index < ResearchTypes.Length; index++)
        {
            ClubResearchType type = ResearchTypes[index];
            ClubResearchDefinition definition = manager.GetDefinition(type);
            if (definition == null)
            {
                researchTexts[index].text = "Исследование недоступно";
                researchButtons[index].interactable = false;
                continue;
            }

            int level = manager.GetLevel(type);
            int nextLevel = level + 1;
            bool completed = level >= definition.MaximumLevel;
            int requiredLevel = completed ? 0 : manager.GetRequiredClubLevel(nextLevel);
            int cost = completed ? 0 : manager.GetNextLevelCost(type);
            string nextEffect = completed ? "максимальный уровень" : GetEffectText(type, nextLevel);
            researchTexts[index].text =
                $"{definition.DisplayName}\n" +
                $"Уровень: {level}/{definition.MaximumLevel} | " +
                $"Эффект: {GetEffectText(type, level)}\n" +
                $"Следующий: {nextEffect}\n" +
                (completed ? "Исследование завершено" :
                    $"Стоимость: {cost} ₽ | Требуется уровень клуба: {requiredLevel}");
            researchButtons[index].interactable = !completed &&
                clubLevel >= requiredLevel && balance >= cost;
        }
    }

    private static string GetEffectText(ClubResearchType type, int level)
    {
        if (level <= 0) return "нет";
        return type switch
        {
            ClubResearchType.ReliableComponents => $"поломки −{level * 10}%",
            ClubResearchType.DurableEquipment => $"износ −{level * 15}%",
            ClubResearchType.EfficientCleaning => $"скорость уборщика +{level * 20}%",
            ClubResearchType.WholesalePurchasing => $"закупки −{level * 10}%",
            ClubResearchType.BrandPromotion => $"маркетинг −{level * 10}%",
            ClubResearchType.NetworkOptimization => $"скорость сессий +{level * 5}%",
            ClubResearchType.EnergyEfficiency => $"электричество −{level * 8}%",
            _ => "нет"
        };
    }

    private void BuildInterface()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rootObject = new GameObject("ClubResearchPanelRoot", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(transform, false);
        Stretch(rootObject.GetComponent<RectTransform>());
        rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        GameObject panel = new GameObject("ClubResearchPanel", typeof(RectTransform),
            typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(rootObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(980f, 940f);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.12f, 0.09f, 0.99f);
        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(28, 28, 22, 22);
        panelLayout.spacing = 10f;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateLabel(panel.transform, "ИССЛЕДОВАНИЯ КЛУБА", 30, 50f,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        CreateScrollArea(panel.transform);
        statusText = CreateLabel(panel.transform, string.Empty, 18, 38f,
            FontStyle.Normal, TextAnchor.MiddleCenter);
        closeButton = CreateButton(panel.transform, "Закрыть", Close, 56f, 220f);
    }

    private void CreateScrollArea(Transform parent)
    {
        GameObject area = new GameObject("ResearchScrollArea", typeof(RectTransform),
            typeof(Image), typeof(LayoutElement), typeof(ScrollRect));
        area.transform.SetParent(parent, false);
        area.GetComponent<Image>().color = new Color(0.02f, 0.05f, 0.04f, 0.75f);
        area.GetComponent<LayoutElement>().preferredHeight = 740f;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform),
            typeof(Image), typeof(Mask));
        viewport.transform.SetParent(area.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-30f, -8f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(10, 10, 10, 10);
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int index = 0; index < ResearchTypes.Length; index++)
            CreateResearchRow(content.transform, index);

        Scrollbar scrollbar = CreateScrollbar(area.transform);
        ScrollRect scrollRect = area.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void CreateResearchRow(Transform parent, int index)
    {
        GameObject row = new GameObject($"ResearchRow_{index}", typeof(RectTransform),
            typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = new Color(0.08f, 0.18f, 0.13f, 0.95f);
        row.GetComponent<LayoutElement>().preferredHeight = 165f;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 10, 10);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        researchTexts[index] = CreateLabel(row.transform, string.Empty, 19, 145f,
            FontStyle.Normal, TextAnchor.MiddleLeft);
        LayoutElement textLayout = researchTexts[index].GetComponent<LayoutElement>();
        textLayout.preferredWidth = 690f;
        textLayout.flexibleWidth = 1f;
        int selectedIndex = index;
        researchButtons[index] = CreateButton(row.transform, "Исследовать",
            () => PurchaseResearch(selectedIndex), 58f, 180f);
    }

    private Scrollbar CreateScrollbar(Transform parent)
    {
        GameObject bar = new GameObject("Scrollbar", typeof(RectTransform),
            typeof(Image), typeof(Scrollbar));
        bar.transform.SetParent(parent, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(1f, 1f);
        barRect.offsetMin = new Vector2(-22f, 8f);
        barRect.offsetMax = new Vector2(-6f, -8f);
        bar.GetComponent<Image>().color = new Color(0.05f, 0.10f, 0.08f, 1f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(bar.transform, false);
        Stretch(handle.GetComponent<RectTransform>());
        handle.GetComponent<Image>().color = new Color(0.44f, 0.78f, 0.58f, 1f);
        Scrollbar scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private Text CreateLabel(Transform parent, string content, int size, float height,
        FontStyle style, TextAnchor alignment)
    {
        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(Text),
            typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = runtimeFont;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        label.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private Button CreateButton(Transform parent, string caption,
        UnityEngine.Events.UnityAction action, float height, float width)
    {
        GameObject buttonObject = new GameObject(caption, typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.55f, 0.34f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.32f, 0.72f, 0.47f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.12f, 0.36f, 0.22f, 1f);
        colors.disabledColor = new Color(0.18f, 0.22f, 0.19f, 0.8f);
        button.colors = colors;
        LayoutElement element = buttonObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;

        Text text = CreateLabel(buttonObject.transform, caption, 18, height,
            FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.GetComponent<RectTransform>();
        Stretch(textRect);
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        return button;
    }

    private void RestoreGameplayState()
    {
        Time.timeScale = previousTimeScale;
        if (!cursorStateCaptured) return;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateCaptured = false;
    }

    private void CaptureCursorState()
    {
        if (cursorStateCaptured) return;
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
            system.gameObject.AddComponent<InputSystemUIInputModule>();
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
