using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum ManagerNavigationSection
{
    Maintenance,
    Pricing,
    Stock,
    Marketing,
    Internet,
    Research,
    Analytics
}

public sealed class ManagerNavigationBar : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.012f, 0.038f, 0.075f, 0.97f);
    private static readonly Color AccentColor =
        new(0.05f, 0.70f, 1f, 1f);

    private GameObject navigationRoot;
    private GameObject sectionPanel;
    private Text toggleText;
    private Button[] sectionButtons;
    private Button overviewButton;
    private Button focusButton;
    private Button buildButton;
    private ManagerModeController managerMode;
    private ManagerCommandBar commandBar;

    public int ButtonCount => sectionButtons?.Length ?? 0;
    public bool IsVisible => navigationRoot != null &&
        navigationRoot.activeSelf;
    public bool IsExpanded => sectionPanel != null &&
        sectionPanel.activeSelf;

    private void Awake()
    {
        BuildNavigation();
        SetExpanded(false);
    }

    private void Start()
    {
        managerMode = FindAnyObjectByType<ManagerModeController>();
        commandBar = GetComponent<ManagerCommandBar>() ??
            FindAnyObjectByType<ManagerCommandBar>();
        RefreshAvailability();
    }

    private void Update()
    {
        bool hudVisible = ClubHUDCanvas.Instance == null ||
            ClubHUDCanvas.Instance.CurrentMode != ClubHUDMode.Hidden;
        bool placementActive = ManagerBuildController.Instance != null &&
            ManagerBuildController.Instance.IsPlacing;
        bool visible = hudVisible && !GameplayInputState.IsBlocked &&
            !placementActive;

        if (navigationRoot.activeSelf != visible)
        {
            navigationRoot.SetActive(visible);
        }

        if (!visible && IsExpanded)
        {
            SetExpanded(false);
        }

        RefreshAvailability();
    }

    public void ToggleExpanded()
    {
        SetExpanded(!IsExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        sectionPanel?.SetActive(expanded);
        if (toggleText != null)
        {
            toggleText.text = expanded
                ? "УПРАВЛЕНИЕ  −"
                : "УПРАВЛЕНИЕ  +";
        }
    }

    public bool TryOpenSection(ManagerNavigationSection section)
    {
        if (GameplayInputState.IsBlocked)
        {
            return false;
        }

        bool opened;
        switch (section)
        {
            case ManagerNavigationSection.Maintenance:
                PCMaintenancePanel.Instance?.Open();
                opened = PCMaintenancePanel.Instance != null &&
                    PCMaintenancePanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Pricing:
                PricingPanel.Instance?.Open();
                opened = PricingPanel.Instance != null &&
                    PricingPanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Stock:
                ConsumableStockPanel.Instance?.Open();
                opened = ConsumableStockPanel.Instance != null &&
                    ConsumableStockPanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Marketing:
                MarketingPanel.Instance?.Open();
                opened = MarketingPanel.Instance != null &&
                    MarketingPanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Internet:
                InternetProviderPanel.Instance?.Open();
                opened = InternetProviderPanel.Instance != null &&
                    InternetProviderPanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Research:
                ClubResearchPanel.Instance?.Open();
                opened = ClubResearchPanel.Instance != null &&
                    ClubResearchPanel.Instance.IsOpen;
                break;

            case ManagerNavigationSection.Analytics:
                DemandAnalyticsPanel.Instance?.Open(false);
                opened = DemandAnalyticsPanel.Instance != null &&
                    DemandAnalyticsPanel.Instance.IsOpen;
                break;

            default:
                opened = false;
                break;
        }

        if (opened)
        {
            SetExpanded(false);
        }

        return opened;
    }

    public bool ShowClubOverview()
    {
        managerMode ??= FindAnyObjectByType<ManagerModeController>();
        bool shown = managerMode != null && managerMode.ShowClubOverview();
        if (shown)
        {
            SetExpanded(false);
        }
        return shown;
    }

    public bool FocusSelectedObject()
    {
        managerMode ??= FindAnyObjectByType<ManagerModeController>();
        bool focused = managerMode != null && managerMode.FocusSelectedObject();
        if (focused)
        {
            SetExpanded(false);
        }
        return focused;
    }

    public bool TryBeginPCPlacement()
    {
        commandBar ??= GetComponent<ManagerCommandBar>() ??
            FindAnyObjectByType<ManagerCommandBar>();
        bool started = commandBar != null &&
            commandBar.TryBeginPCPlacement();
        if (started)
        {
            SetExpanded(false);
        }
        return started;
    }

    private void BuildNavigation()
    {
        navigationRoot = new GameObject(
            "ManagerNavigationBar",
            typeof(RectTransform)
        );
        navigationRoot.AddComponent<ScalableUIRoot>();
        navigationRoot.transform.SetParent(transform, false);

        RectTransform rootRect = navigationRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.one;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.anchoredPosition = new Vector2(-20f, -146f);
        rootRect.sizeDelta = new Vector2(220f, 42f);

        Button toggleButton = CreateButton(
            navigationRoot.transform,
            "ManagerNavigationToggle",
            string.Empty,
            ToggleExpanded
        );
        RectTransform toggleRect = toggleButton.GetComponent<RectTransform>();
        toggleRect.anchorMin = Vector2.zero;
        toggleRect.anchorMax = Vector2.one;
        toggleRect.offsetMin = Vector2.zero;
        toggleRect.offsetMax = Vector2.zero;
        toggleText = toggleButton.GetComponentInChildren<Text>();

        sectionPanel = new GameObject(
            "ManagerNavigationSections",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup),
            typeof(Outline)
        );
        sectionPanel.transform.SetParent(navigationRoot.transform, false);

        RectTransform panelRect = sectionPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -8f);
        panelRect.sizeDelta = new Vector2(400f, 216f);

        Image image = sectionPanel.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;

        Outline outline = sectionPanel.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.70f, 1f, 0.62f);
        outline.effectDistance = new Vector2(2f, -2f);

        GridLayoutGroup grid = sectionPanel.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.spacing = new Vector2(8f, 6f);
        grid.cellSize = new Vector2(186f, 34f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        overviewButton = CreateButton(
            sectionPanel.transform,
            "OverviewButton",
            "ОБЗОР",
            () => ShowClubOverview()
        );
        focusButton = CreateButton(
            sectionPanel.transform,
            "FocusButton",
            "ФОКУС",
            () => FocusSelectedObject()
        );

        sectionButtons = new[]
        {
            CreateSectionButton("ServiceButton", "СЕРВИС",
                ManagerNavigationSection.Maintenance),
            CreateSectionButton("PricingButton", "ТАРИФЫ",
                ManagerNavigationSection.Pricing),
            CreateSectionButton("StockButton", "СКЛАД",
                ManagerNavigationSection.Stock),
            CreateSectionButton("MarketingButton", "МАРКЕТИНГ",
                ManagerNavigationSection.Marketing),
            CreateSectionButton("InternetButton", "ИНТЕРНЕТ",
                ManagerNavigationSection.Internet),
            CreateSectionButton("ResearchButton", "ИССЛЕДОВАНИЯ",
                ManagerNavigationSection.Research),
            CreateSectionButton("AnalyticsButton", "АНАЛИТИКА",
                ManagerNavigationSection.Analytics)
        };

        buildButton = CreateButton(
            sectionPanel.transform,
            "BuildPCButton",
            "СТРОИТЬ ПК",
            () => TryBeginPCPlacement()
        );
    }

    private Button CreateSectionButton(
        string name,
        string label,
        ManagerNavigationSection section)
    {
        return CreateButton(
            sectionPanel.transform,
            name,
            label,
            () => TryOpenSection(section)
        );
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        UnityAction action)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Outline)
        );
        buttonObject.transform.SetParent(parent, false);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.025f, 0.11f, 0.19f, 1f);
        colors.highlightedColor = new Color(0.025f, 0.32f, 0.50f, 1f);
        colors.pressedColor = AccentColor;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.07f, 0.09f, 0.12f, 0.82f);
        button.colors = colors;
        button.onClick.AddListener(action);

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.06f, 0.44f, 0.72f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 3f);
        textRect.offsetMax = new Vector2(-8f, -3f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.fontStyle = FontStyle.Bold;
        text.text = label;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return button;
    }

    private void RefreshAvailability()
    {
        if (sectionButtons == null || sectionButtons.Length != 7)
        {
            return;
        }

        sectionButtons[0].interactable = PCMaintenancePanel.Instance != null;
        sectionButtons[1].interactable = PricingPanel.Instance != null;
        sectionButtons[2].interactable = ConsumableStockPanel.Instance != null;
        sectionButtons[3].interactable = MarketingPanel.Instance != null;
        sectionButtons[4].interactable = InternetProviderPanel.Instance != null;
        sectionButtons[5].interactable = ClubResearchPanel.Instance != null;
        sectionButtons[6].interactable = DemandAnalyticsPanel.Instance != null;

        managerMode ??= FindAnyObjectByType<ManagerModeController>();
        commandBar ??= GetComponent<ManagerCommandBar>() ??
            FindAnyObjectByType<ManagerCommandBar>();
        overviewButton.interactable = managerMode != null;
        focusButton.interactable = managerMode != null &&
            managerMode.SelectedBehaviour != null;
        buildButton.interactable = commandBar != null &&
            commandBar.CanPurchasePC;
    }
}
