using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClubHUDCanvas : MonoBehaviour
{
    public static ClubHUDCanvas Instance { get; private set; }

    [Header("Canvas Settings")]
    [SerializeField] private Vector2 referenceResolution =
        new(1920f, 1080f);
    [SerializeField, Range(0f, 1f)] private float widthHeightMatch = 0.5f;
    [SerializeField, Min(400f)] private float hudWidth = 520f;

    [Header("HUD Settings")]
    [SerializeField] private ClubHUDMode currentMode = ClubHUDMode.Compact;
    [SerializeField, Min(14)] private int compactFontSize = 20;
    [SerializeField, Min(12)] private int expandedFontSize = 18;

    private readonly List<PC> pcs = new();
    private readonly List<HUDWarningData> warnings = new();

    private GameObject gameplayHUDRoot;
    private GameObject compactSection;
    private GameObject expandedSection;
    private GameObject warningSection;
    private GameObject interactionPromptPanel;

    private Text dayText;
    private Text balanceText;
    private Text reputationText;
    private Text clubLevelText;
    private Text pcStateText;
    private Text dailyGoalText;
    private Text warningText;

    private Text clientQueueText;
    private Text cleanlinessText;
    private Text equipmentStatusText;
    private Text consumableStockText;
    private Text pricingText;
    private Text internetProviderText;
    private Text staffText;
    private Text marketingText;
    private Text researchText;
    private Text demandAnalyticsText;
    private Text roomStatusText;
    private Text pcTierText;
    private Text expansionText;

    private Text interactionPromptText;
    private PlayerInteraction playerInteraction;
    private ClientSpawner clientSpawner;
    private string currentInteractionPrompt = string.Empty;
    private bool temporarilyHidden;
    private Font runtimeFont;

    public ClubHUDMode CurrentMode => currentMode;
    public IReadOnlyList<HUDWarningData> ActiveWarnings => warnings;
    public bool IsTemporarilyHidden => temporarilyHidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildCanvas();
        SetMode(currentMode);
    }

    private void Start()
    {
        SubscribeToManagers();
        RegisterExistingPCs();
        SubscribeToPlayerInteraction();
        SubscribeToClientSpawner();
        RefreshAll();
    }

    private void Update()
    {
        RefreshDayTimer();
        SetTemporarilyHidden(GameplayInputState.IsBlocked);
        RefreshInteractionPromptVisibility();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManagers();
        UnsubscribeFromPCs();
        UnsubscribeFromPlayerInteraction();

        if (clientSpawner != null)
        {
            clientSpawner.QueueChanged -= RefreshClientQueue;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetMode(ClubHUDMode mode)
    {
        currentMode = Enum.IsDefined(typeof(ClubHUDMode), mode)
            ? mode
            : ClubHUDMode.Compact;

        bool compactVisible = currentMode == ClubHUDMode.Compact ||
            currentMode == ClubHUDMode.Expanded;
        bool expandedVisible = currentMode == ClubHUDMode.Expanded;

        compactSection?.SetActive(compactVisible);
        expandedSection?.SetActive(expandedVisible);
        RefreshAll();
    }

    public void ToggleHUDMode()
    {
        ClubHUDMode nextMode = currentMode switch
        {
            ClubHUDMode.Compact => ClubHUDMode.Expanded,
            ClubHUDMode.Expanded => ClubHUDMode.Hidden,
            _ => ClubHUDMode.Compact
        };

        SetMode(nextMode);
    }

    public void SetTemporarilyHidden(bool hidden)
    {
        if (temporarilyHidden == hidden)
        {
            return;
        }

        temporarilyHidden = hidden;
        gameplayHUDRoot?.SetActive(!hidden);

        if (!hidden)
        {
            SetMode(currentMode);
        }
    }

    private void BuildCanvas()
    {
        runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = widthHeightMatch;
        GameUserSettings.ApplyCanvasScale(scaler, referenceResolution);

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        CreateGameplayHUDRoot();
        CreateInteractionPrompt();
    }

    private void CreateGameplayHUDRoot()
    {
        gameplayHUDRoot = new GameObject(
            "GameplayHUDRoot",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );
        gameplayHUDRoot.AddComponent<ScalableUIRoot>();
        gameplayHUDRoot.transform.SetParent(transform, false);

        RectTransform rootRect = gameplayHUDRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(20f, -20f);
        rootRect.sizeDelta = new Vector2(
            Mathf.Min(hudWidth, referenceResolution.x * 0.3f),
            0f
        );

        VerticalLayoutGroup layout =
            gameplayHUDRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            gameplayHUDRoot.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        compactSection = CreateSection(
            "CompactSection",
            new Color(0.035f, 0.045f, 0.06f, 0.9f),
            4f
        );
        dayText = CreateLine("DayText", compactSection.transform, compactFontSize);
        balanceText = CreateLine("BalanceText", compactSection.transform, compactFontSize);
        reputationText = CreateLine("ReputationText", compactSection.transform, compactFontSize);
        clubLevelText = CreateLine("ClubLevelText", compactSection.transform, compactFontSize);
        pcStateText = CreateLine("PCStateText", compactSection.transform, compactFontSize);
        dailyGoalText = CreateLine("DailyGoalText", compactSection.transform, compactFontSize, 44f);

        warningSection = CreateSection(
            "WarningSection",
            new Color(0.48f, 0.12f, 0.06f, 0.96f),
            0f
        );
        warningText = CreateLine(
            "WarningText",
            warningSection.transform,
            compactFontSize,
            44f,
            new Color(1f, 0.94f, 0.78f)
        );

        expandedSection = CreateSection(
            "ExpandedSection",
            new Color(0.035f, 0.045f, 0.06f, 0.92f),
            2f
        );
        clientQueueText = CreateLine("ClientQueueText", expandedSection.transform, expandedFontSize);
        cleanlinessText = CreateLine("CleanlinessText", expandedSection.transform, expandedFontSize);
        equipmentStatusText = CreateLine("EquipmentStatusText", expandedSection.transform, expandedFontSize);
        consumableStockText = CreateLine("ConsumableStockText", expandedSection.transform, expandedFontSize);
        pricingText = CreateLine("PricingText", expandedSection.transform, expandedFontSize);
        internetProviderText = CreateLine("InternetProviderText", expandedSection.transform, expandedFontSize);
        staffText = CreateLine("StaffText", expandedSection.transform, expandedFontSize);
        marketingText = CreateLine("MarketingText", expandedSection.transform, expandedFontSize);
        researchText = CreateLine("ResearchText", expandedSection.transform, expandedFontSize);
        demandAnalyticsText = CreateLine("DemandAnalyticsText", expandedSection.transform, expandedFontSize);
        roomStatusText = CreateLine("RoomStatusText", expandedSection.transform, expandedFontSize);
        pcTierText = CreateLine("PCTierText", expandedSection.transform, expandedFontSize);
        expansionText = CreateLine("ExpansionText", expandedSection.transform, expandedFontSize);

        warningSection.SetActive(false);
        expandedSection.SetActive(false);
    }

    private GameObject CreateSection(string name, Color color, float spacing)
    {
        GameObject section = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );
        section.transform.SetParent(gameplayHUDRoot.transform, false);

        Image image = section.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = section.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return section;
    }

    private Text CreateLine(
        string name,
        Transform parent,
        int size,
        float preferredHeight = 30f,
        Color? color = null)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Text),
            typeof(LayoutElement)
        );
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = size;
        text.color = color ?? Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredHeight = preferredHeight;
        return text;
    }

    private void CreateInteractionPrompt()
    {
        interactionPromptPanel = new GameObject(
            "InteractionPrompt",
            typeof(RectTransform),
            typeof(Image)
        );
        interactionPromptPanel.AddComponent<ScalableUIRoot>();
        interactionPromptPanel.transform.SetParent(transform, false);

        RectTransform panelRect =
            interactionPromptPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 28f);
        panelRect.sizeDelta = new Vector2(760f, 58f);

        Image image = interactionPromptPanel.GetComponent<Image>();
        image.color = new Color(0.035f, 0.045f, 0.06f, 0.92f);
        image.raycastTarget = false;

        GameObject textObject = new GameObject(
            "InteractionPromptText",
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(interactionPromptPanel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 4f);
        textRect.offsetMax = new Vector2(-14f, -4f);

        interactionPromptText = textObject.GetComponent<Text>();
        interactionPromptText.font = runtimeFont;
        interactionPromptText.fontSize = compactFontSize;
        interactionPromptText.color = Color.white;
        interactionPromptText.alignment = TextAnchor.MiddleCenter;
        interactionPromptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        interactionPromptText.verticalOverflow = VerticalWrapMode.Overflow;
        interactionPromptText.raycastTarget = false;
        interactionPromptPanel.SetActive(false);
    }

    private void SubscribeToManagers()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        if (ClubProgressionManager.Instance != null)
            ClubProgressionManager.Instance.StatusChanged += RefreshClubProgression;
        if (ClubReputationManager.Instance != null)
            ClubReputationManager.Instance.StatusChanged += RefreshReputation;
        if (DailyGoalManager.Instance != null)
            DailyGoalManager.Instance.StatusChanged += RefreshDailyGoal;
        if (BankruptcyManager.Instance != null)
            BankruptcyManager.Instance.StatusChanged += RefreshWarnings;
        if (PCExpansionManager.Instance != null)
            PCExpansionManager.Instance.StatusChanged += RefreshExpansion;
        if (RoomUnlockManager.Instance != null)
            RoomUnlockManager.Instance.StatusChanged += OnRoomStatusChanged;
        if (TechnicianManager.Instance != null)
            TechnicianManager.Instance.StatusChanged += RefreshStaff;
        if (CleanerManager.Instance != null)
            CleanerManager.Instance.StatusChanged += RefreshStaff;
        if (ClubCleanlinessManager.Instance != null)
            ClubCleanlinessManager.Instance.StatusChanged += RefreshCleanliness;
        if (PricingManager.Instance != null)
            PricingManager.Instance.StatusChanged += RefreshPricing;
        if (ConsumableInventoryManager.Instance != null)
            ConsumableInventoryManager.Instance.StatusChanged += RefreshConsumableStock;
        if (MarketingManager.Instance != null)
            MarketingManager.Instance.StatusChanged += RefreshMarketing;
        if (DemandAnalyticsManager.Instance != null)
            DemandAnalyticsManager.Instance.StatusChanged += RefreshDemandAnalytics;
        if (ClubRandomEventManager.Instance != null)
            ClubRandomEventManager.Instance.StatusChanged += RefreshInternetProvider;
        if (InternetProviderManager.Instance != null)
            InternetProviderManager.Instance.StatusChanged += RefreshInternetProvider;
        if (ClubResearchManager.Instance != null)
            ClubResearchManager.Instance.StatusChanged += RefreshResearch;

        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;
    }

    private void UnsubscribeFromManagers()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        if (ClubProgressionManager.Instance != null)
            ClubProgressionManager.Instance.StatusChanged -= RefreshClubProgression;
        if (ClubReputationManager.Instance != null)
            ClubReputationManager.Instance.StatusChanged -= RefreshReputation;
        if (DailyGoalManager.Instance != null)
            DailyGoalManager.Instance.StatusChanged -= RefreshDailyGoal;
        if (BankruptcyManager.Instance != null)
            BankruptcyManager.Instance.StatusChanged -= RefreshWarnings;
        if (PCExpansionManager.Instance != null)
            PCExpansionManager.Instance.StatusChanged -= RefreshExpansion;
        if (RoomUnlockManager.Instance != null)
            RoomUnlockManager.Instance.StatusChanged -= OnRoomStatusChanged;
        if (TechnicianManager.Instance != null)
            TechnicianManager.Instance.StatusChanged -= RefreshStaff;
        if (CleanerManager.Instance != null)
            CleanerManager.Instance.StatusChanged -= RefreshStaff;
        if (ClubCleanlinessManager.Instance != null)
            ClubCleanlinessManager.Instance.StatusChanged -= RefreshCleanliness;
        if (PricingManager.Instance != null)
            PricingManager.Instance.StatusChanged -= RefreshPricing;
        if (ConsumableInventoryManager.Instance != null)
            ConsumableInventoryManager.Instance.StatusChanged -= RefreshConsumableStock;
        if (MarketingManager.Instance != null)
            MarketingManager.Instance.StatusChanged -= RefreshMarketing;
        if (DemandAnalyticsManager.Instance != null)
            DemandAnalyticsManager.Instance.StatusChanged -= RefreshDemandAnalytics;
        if (ClubRandomEventManager.Instance != null)
            ClubRandomEventManager.Instance.StatusChanged -= RefreshInternetProvider;
        if (InternetProviderManager.Instance != null)
            InternetProviderManager.Instance.StatusChanged -= RefreshInternetProvider;
        if (ClubResearchManager.Instance != null)
            ClubResearchManager.Instance.StatusChanged -= RefreshResearch;

        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;
    }

    private void RegisterExistingPCs()
    {
        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }
    }

    private void RegisterPC(PC pc)
    {
        if (pc == null || pcs.Contains(pc))
        {
            return;
        }

        pcs.Add(pc);
        pc.StateChanged += OnPCStateChanged;
        pc.TierChanged += OnPCTierChanged;
        pc.EquipmentChanged += OnPCEquipmentChanged;
        RefreshPCInformation();
        RefreshEquipmentStatus();
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.StateChanged -= OnPCStateChanged;
        pc.TierChanged -= OnPCTierChanged;
        pc.EquipmentChanged -= OnPCEquipmentChanged;
        pcs.Remove(pc);
        RefreshPCInformation();
        RefreshEquipmentStatus();
    }

    private void UnsubscribeFromPCs()
    {
        foreach (PC pc in pcs)
        {
            if (pc == null)
            {
                continue;
            }

            pc.StateChanged -= OnPCStateChanged;
            pc.TierChanged -= OnPCTierChanged;
            pc.EquipmentChanged -= OnPCEquipmentChanged;
        }

        pcs.Clear();
    }

    private void SubscribeToPlayerInteraction()
    {
        playerInteraction = FindAnyObjectByType<PlayerInteraction>();
        if (playerInteraction == null)
        {
            return;
        }

        playerInteraction.PromptChanged += OnInteractionPromptChanged;
        currentInteractionPrompt = playerInteraction.CurrentPrompt;
    }

    private void UnsubscribeFromPlayerInteraction()
    {
        if (playerInteraction != null)
        {
            playerInteraction.PromptChanged -= OnInteractionPromptChanged;
        }
    }

    private void SubscribeToClientSpawner()
    {
        clientSpawner = FindAnyObjectByType<ClientSpawner>();
        if (clientSpawner == null)
        {
            RefreshClientQueue();
            return;
        }

        clientSpawner.QueueChanged += RefreshClientQueue;
        RefreshClientQueue();
    }

    private void RefreshAll()
    {
        RefreshBalance();
        RefreshDayTimer();
        RefreshReputation();
        RefreshClubProgression();
        RefreshPCInformation();
        RefreshDailyGoal();
        RefreshClientQueue();
        RefreshCleanliness();
        RefreshEquipmentStatus();
        RefreshConsumableStock();
        RefreshPricing();
        RefreshInternetProvider();
        RefreshStaff();
        RefreshMarketing();
        RefreshResearch();
        RefreshDemandAnalytics();
        RefreshRoomStatus();
        RefreshExpansion();
        RefreshWarnings();
        RefreshInteractionPromptVisibility();
    }

    private void OnMoneyChanged(int money)
    {
        balanceText.text = $"Баланс: {money:N0} ₽";
        RefreshWarnings();
    }

    private void RefreshBalance()
    {
        int money = EconomyManager.Instance != null
            ? EconomyManager.Instance.Money
            : 0;
        balanceText.text = $"Баланс: {money:N0} ₽";
    }

    private void RefreshDayTimer()
    {
        GameDayManager manager = GameDayManager.Instance;
        if (manager == null || dayText == null)
        {
            return;
        }

        int remaining = Mathf.Max(0, Mathf.CeilToInt(manager.TimeRemaining));
        dayText.text =
            $"День {manager.CurrentDay} · {remaining / 60:00}:{remaining % 60:00}";
    }

    private void RefreshReputation()
    {
        ClubReputationManager manager = ClubReputationManager.Instance;
        reputationText.text = manager == null
            ? "Репутация: —"
            : $"Репутация: {manager.Reputation}";
    }

    private void RefreshClubProgression()
    {
        ClubProgressionManager manager = ClubProgressionManager.Instance;
        if (manager == null)
        {
            clubLevelText.text = "Уровень клуба: —";
            return;
        }

        clubLevelText.text = manager.IsMaxLevel
            ? $"Уровень клуба: {manager.Level} · максимум"
            : $"Уровень клуба: {manager.Level} · XP " +
              $"{manager.Experience}/{manager.ExperienceToNextLevel}";
    }

    private void RefreshDailyGoal()
    {
        DailyGoalManager manager = DailyGoalManager.Instance;
        if (manager == null)
        {
            dailyGoalText.text = "Цель: —";
            return;
        }

        int progress = Mathf.Min(manager.CurrentProgress, manager.TargetValue);
        dailyGoalText.text = manager.GoalCompleted
            ? $"Цель: выполнена · {manager.GetGoalDescription()}"
            : $"Цель: {manager.GetGoalDescription()} · " +
              $"{progress}/{manager.TargetValue}";
    }

    private void OnPCStateChanged(PCState state)
    {
        RefreshPCInformation();
        RefreshWarnings();
    }

    private void OnPCTierChanged(PCTier tier)
    {
        RefreshPCInformation();
    }

    private void OnPCEquipmentChanged()
    {
        RefreshEquipmentStatus();
        RefreshWarnings();
    }

    private void RefreshPCInformation()
    {
        pcs.RemoveAll(pc => pc == null);
        int free = 0;
        int occupied = 0;
        int broken = 0;
        int basic = 0;
        int gaming = 0;
        int premium = 0;

        foreach (PC pc in pcs)
        {
            if (!pc.HasRoomAccess)
            {
                continue;
            }

            if (pc.State == PCState.Occupied)
                occupied++;
            else if (pc.State == PCState.Broken || !pc.IsAvailable)
                broken++;
            else
                free++;

            switch (pc.Tier)
            {
                case PCTier.Basic:
                    basic++;
                    break;
                case PCTier.Gaming:
                    gaming++;
                    break;
                case PCTier.Premium:
                    premium++;
                    break;
            }
        }

        pcStateText.text =
            $"ПК: {free} свободно · {occupied} занято · {broken} сломано";
        pcTierText.text = $"Классы ПК: B {basic} · G {gaming} · P {premium}";
    }

    private void RefreshClientQueue()
    {
        clientQueueText.text = clientSpawner == null
            ? "Очередь: —"
            : $"Очередь: {clientSpawner.WaitingClientCount}/" +
              $"{clientSpawner.MaxQueueSize}";
        RefreshWarnings();
    }

    private void RefreshCleanliness()
    {
        ClubCleanlinessManager manager = ClubCleanlinessManager.Instance;
        cleanlinessText.text = manager == null
            ? "Чистота: —"
            : $"Чистота: {manager.Cleanliness:F0}% · мусор {manager.TrashCount}";
        RefreshWarnings();
    }

    private void RefreshEquipmentStatus()
    {
        GetEquipmentCounts(out int worn, out int critical);
        equipmentStatusText.text =
            $"Оборудование: {worn} изношено · {critical} критично";
        RefreshWarnings();
    }

    private void GetEquipmentCounts(out int worn, out int critical)
    {
        worn = 0;
        critical = 0;

        foreach (PC pc in pcs)
        {
            if (pc == null || !pc.HasRoomAccess)
            {
                continue;
            }

            if (pc.LowestEquipmentCondition <= 20f)
            {
                critical++;
                worn++;
            }
            else if (pc.LowestEquipmentCondition <= 50f)
            {
                worn++;
            }
        }
    }

    private void RefreshConsumableStock()
    {
        ConsumableInventoryManager manager = ConsumableInventoryManager.Instance;
        consumableStockText.text = manager == null
            ? "Склад: —"
            : $"Склад: {manager.EnergyDrinkStock} энергетиков · " +
              $"{manager.SnackStock} снеков";
        RefreshWarnings();
    }

    private void RefreshPricing()
    {
        PricingManager manager = PricingManager.Instance;
        pricingText.text = manager == null
            ? "Тарифы: —"
            : $"Тарифы: B {manager.GetPricePercent(PCTier.Basic)}% · " +
              $"G {manager.GetPricePercent(PCTier.Gaming)}% · " +
              $"P {manager.GetPricePercent(PCTier.Premium)}%";
    }

    private void RefreshInternetProvider()
    {
        InternetPlanDefinition plan =
            InternetProviderManager.Instance?.GetActivePlan();
        internetProviderText.text = plan == null
            ? "Интернет: —"
            : $"Интернет: {plan.DisplayName} ×{plan.SessionSpeedMultiplier:F2}";
        RefreshWarnings();
    }

    private void RefreshStaff()
    {
        bool technician = TechnicianManager.Instance != null &&
            TechnicianManager.Instance.TechnicianHired;
        bool cleaner = CleanerManager.Instance != null &&
            CleanerManager.Instance.CleanerHired;
        staffText.text =
            $"Персонал: техник {(technician ? "работает" : "не нанят")} · " +
            $"уборщик {(cleaner ? "работает" : "не нанят")}";
    }

    private void RefreshMarketing()
    {
        MarketingManager manager = MarketingManager.Instance;
        if (manager == null || !manager.HasActiveCampaign)
        {
            marketingText.text = "Маркетинг: нет кампании";
            return;
        }

        MarketingCampaignDefinition definition =
            manager.GetDefinition(manager.ActiveCampaign);
        marketingText.text =
            $"Маркетинг: {definition?.DisplayName ?? manager.ActiveCampaign.ToString()} · " +
            $"{manager.RemainingDays} дн.";
    }

    private void RefreshResearch()
    {
        ClubResearchManager manager = ClubResearchManager.Instance;
        researchText.text = manager == null
            ? "Исследования: —"
            : $"Исследования: {manager.TotalPurchasedLevels} уровней";
    }

    private void RefreshDemandAnalytics()
    {
        DemandAnalyticsManager manager = DemandAnalyticsManager.Instance;
        if (manager == null)
        {
            demandAnalyticsText.text = "Загрузка: —";
            return;
        }

        DemandAnalyticsReportData report = manager.CurrentReport;
        demandAnalyticsText.text =
            $"Загрузка: B {report.basic.UtilizationPercent:F0}% · " +
            $"G {report.gaming.UtilizationPercent:F0}% · " +
            $"P {report.premium.UtilizationPercent:F0}%";
    }

    private void OnRoomStatusChanged()
    {
        RefreshRoomStatus();
        RefreshPCInformation();
    }

    private void RefreshRoomStatus()
    {
        RoomUnlockManager manager = RoomUnlockManager.Instance;
        if (manager == null)
        {
            roomStatusText.text = "Комнаты: —";
            return;
        }

        int unlocked = 0;
        foreach (RoomDoor door in manager.RoomDoors)
        {
            if (door != null && door.IsUnlocked)
            {
                unlocked++;
            }
        }

        roomStatusText.text =
            $"Комнаты: {unlocked}/{manager.RoomDoors.Count} открыто";
    }

    private void RefreshExpansion()
    {
        PCExpansionManager manager = PCExpansionManager.Instance;
        expansionText.text = manager == null
            ? "Расширение: —"
            : $"Расширение: {manager.RemainingSlots} мест · " +
              $"новый ПК {manager.PurchaseCost:N0} ₽";
    }

    private void RefreshWarnings()
    {
        if (warningSection == null || warningText == null)
        {
            return;
        }

        warnings.Clear();
        AddQueueWarning();
        AddEquipmentWarning();
        AddCleanlinessWarning();
        AddInventoryWarning();
        AddInternetWarning();
        AddBankruptcyWarning();
        warnings.Sort((left, right) => right.Priority.CompareTo(left.Priority));

        warningText.text = warnings.Count > 0
            ? warnings[0].Message
            : string.Empty;
        bool visible = !temporarilyHidden &&
            currentMode != ClubHUDMode.Hidden && warnings.Count > 0;
        warningSection.SetActive(visible);
    }

    private void AddQueueWarning()
    {
        if (clientSpawner == null || clientSpawner.MaxQueueSize <= 0 ||
            clientSpawner.WaitingClientCount < clientSpawner.MaxQueueSize)
        {
            return;
        }

        warnings.Add(new HUDWarningData(
            HUDWarningType.QueueFull,
            "Очередь заполнена — клиенты уходят",
            80
        ));
    }

    private void AddEquipmentWarning()
    {
        GetEquipmentCounts(out _, out int critical);
        if (critical <= 0)
        {
            return;
        }

        warnings.Add(new HUDWarningData(
            HUDWarningType.CriticalEquipment,
            $"Критическое оборудование: {critical} ПК",
            70
        ));
    }

    private void AddCleanlinessWarning()
    {
        ClubCleanlinessManager manager = ClubCleanlinessManager.Instance;
        if (manager == null || manager.Cleanliness >= 50f)
        {
            return;
        }

        warnings.Add(new HUDWarningData(
            HUDWarningType.LowCleanliness,
            $"Низкая чистота: {manager.Cleanliness:F0}%",
            60
        ));
    }

    private void AddInventoryWarning()
    {
        ConsumableInventoryManager manager = ConsumableInventoryManager.Instance;
        if (manager == null ||
            (manager.EnergyDrinkStock > 0 && manager.SnackStock > 0))
        {
            return;
        }

        string product = manager.EnergyDrinkStock <= 0 && manager.SnackStock <= 0
            ? "энергетики и снеки"
            : manager.EnergyDrinkStock <= 0 ? "энергетики" : "снеки";
        warnings.Add(new HUDWarningData(
            HUDWarningType.EmptyInventory,
            $"Закончились товары: {product}",
            50
        ));
    }

    private void AddInternetWarning()
    {
        ClubRandomEventManager manager = ClubRandomEventManager.Instance;
        if (manager == null || !manager.IsInternetUnavailable)
        {
            return;
        }

        warnings.Add(new HUDWarningData(
            HUDWarningType.InternetOutage,
            "Интернет недоступен — новые сессии не запускаются",
            90
        ));
    }

    private void AddBankruptcyWarning()
    {
        BankruptcyManager manager = BankruptcyManager.Instance;
        EconomyManager economy = EconomyManager.Instance;
        if (manager == null || economy == null ||
            (manager.ConsecutiveDebtDays <= 0 &&
             economy.Money > manager.BankruptcyThreshold))
        {
            return;
        }

        warnings.Add(new HUDWarningData(
            HUDWarningType.BankruptcyRisk,
            $"Высокий риск банкротства: баланс {economy.Money:N0} ₽ · " +
            $"{manager.ConsecutiveDebtDays}/{manager.ConsecutiveDebtDaysToLose} дней",
            100
        ));
    }

    private void OnInteractionPromptChanged(string prompt)
    {
        currentInteractionPrompt = prompt ?? string.Empty;
        RefreshInteractionPromptVisibility();
    }

    private void RefreshInteractionPromptVisibility()
    {
        if (interactionPromptPanel == null || interactionPromptText == null)
        {
            return;
        }

        bool visible = !GameplayInputState.IsBlocked &&
            !string.IsNullOrWhiteSpace(currentInteractionPrompt);
        interactionPromptPanel.SetActive(visible);
        if (visible)
        {
            interactionPromptText.text = currentInteractionPrompt;
        }
    }

    private void OnValidate()
    {
        referenceResolution.x = Mathf.Max(640f, referenceResolution.x);
        referenceResolution.y = Mathf.Max(360f, referenceResolution.y);
        hudWidth = Mathf.Clamp(hudWidth, 400f, referenceResolution.x * 0.3f);
        compactFontSize = Mathf.Max(14, compactFontSize);
        expandedFontSize = Mathf.Max(12, expandedFontSize);
    }
}
