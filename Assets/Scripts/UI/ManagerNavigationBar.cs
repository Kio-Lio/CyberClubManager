using UnityEngine;
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
    private Button[] sectionButtons;

    public int ButtonCount => sectionButtons?.Length ?? 0;
    public bool IsVisible => navigationRoot != null &&
        navigationRoot.activeSelf;

    private void Awake()
    {
        BuildNavigation();
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

        RefreshAvailability();
    }

    public bool TryOpenSection(ManagerNavigationSection section)
    {
        if (GameplayInputState.IsBlocked)
        {
            return false;
        }

        switch (section)
        {
            case ManagerNavigationSection.Maintenance:
                PCMaintenancePanel.Instance?.Open();
                return PCMaintenancePanel.Instance != null &&
                    PCMaintenancePanel.Instance.IsOpen;

            case ManagerNavigationSection.Pricing:
                PricingPanel.Instance?.Open();
                return PricingPanel.Instance != null &&
                    PricingPanel.Instance.IsOpen;

            case ManagerNavigationSection.Stock:
                ConsumableStockPanel.Instance?.Open();
                return ConsumableStockPanel.Instance != null &&
                    ConsumableStockPanel.Instance.IsOpen;

            case ManagerNavigationSection.Marketing:
                MarketingPanel.Instance?.Open();
                return MarketingPanel.Instance != null &&
                    MarketingPanel.Instance.IsOpen;

            case ManagerNavigationSection.Internet:
                InternetProviderPanel.Instance?.Open();
                return InternetProviderPanel.Instance != null &&
                    InternetProviderPanel.Instance.IsOpen;

            case ManagerNavigationSection.Research:
                ClubResearchPanel.Instance?.Open();
                return ClubResearchPanel.Instance != null &&
                    ClubResearchPanel.Instance.IsOpen;

            case ManagerNavigationSection.Analytics:
                DemandAnalyticsPanel.Instance?.Open(false);
                return DemandAnalyticsPanel.Instance != null &&
                    DemandAnalyticsPanel.Instance.IsOpen;

            default:
                return false;
        }
    }

    private void BuildNavigation()
    {
        navigationRoot = new GameObject(
            "ManagerNavigationBar",
            typeof(RectTransform),
            typeof(Image),
            typeof(GridLayoutGroup),
            typeof(Outline)
        );
        navigationRoot.AddComponent<ScalableUIRoot>();
        navigationRoot.transform.SetParent(transform, false);

        RectTransform rect = navigationRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-18f, -232f);
        rect.sizeDelta = new Vector2(420f, 188f);

        Image image = navigationRoot.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = true;

        Outline outline = navigationRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.70f, 1f, 0.62f);
        outline.effectDistance = new Vector2(2f, -2f);

        GridLayoutGroup grid = navigationRoot.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 10, 10);
        grid.spacing = new Vector2(8f, 8f);
        grid.cellSize = new Vector2(194f, 36f);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        sectionButtons = new[]
        {
            CreateButton("ServiceButton", "СЕРВИС",
                ManagerNavigationSection.Maintenance),
            CreateButton("PricingButton", "ТАРИФЫ",
                ManagerNavigationSection.Pricing),
            CreateButton("StockButton", "СКЛАД",
                ManagerNavigationSection.Stock),
            CreateButton("MarketingButton", "МАРКЕТИНГ",
                ManagerNavigationSection.Marketing),
            CreateButton("InternetButton", "ИНТЕРНЕТ",
                ManagerNavigationSection.Internet),
            CreateButton("ResearchButton", "ИССЛЕДОВАНИЯ",
                ManagerNavigationSection.Research),
            CreateButton("AnalyticsButton", "АНАЛИТИКА",
                ManagerNavigationSection.Analytics)
        };
    }

    private Button CreateButton(
        string name,
        string label,
        ManagerNavigationSection section)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(Outline)
        );
        buttonObject.transform.SetParent(navigationRoot.transform, false);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.025f, 0.11f, 0.19f, 1f);
        colors.highlightedColor = new Color(0.025f, 0.32f, 0.50f, 1f);
        colors.pressedColor = AccentColor;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.07f, 0.09f, 0.12f, 0.82f);
        button.colors = colors;
        button.onClick.AddListener(() => TryOpenSection(section));

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
    }
}
