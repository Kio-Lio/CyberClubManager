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
    private Text bankruptcyRiskText;
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
    private MonoBehaviour interactionPromptSourceBehaviour;
    private IInteractionPromptSource interactionPromptSource;
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
        SubscribeToInteractionPromptSource();
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
        UnsubscribeFromInteractionPromptSource();

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
            typeof(RectTransform)
        );
        gameplayHUDRoot.AddComponent<ScalableUIRoot>();
        gameplayHUDRoot.transform.SetParent(transform, false);

        RectTransform rootRect = gameplayHUDRoot.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        compactSection = CreateContainer(
            "CompactSection",
            gameplayHUDRoot.transform
        );

        GameObject economyPanel = CreatePanel(
            "EconomyPanel",
            compactSection.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -20f),
            new Vector2(410f, 116f),
            2f
        );
        CreateSectionTitle("EconomyTitle", economyPanel.transform, "КЛУБ");
        balanceText = CreateLine(
            "BalanceText",
            economyPanel.transform,
            compactFontSize + 1,
            25f,
            new Color(0.3f, 1f, 0.38f)
        );
        dayText = CreateLine(
            "DayText",
            economyPanel.transform,
            compactFontSize,
            23f,
            new Color(0.82f, 0.9f, 1f)
        );
        dailyGoalText = CreateLine(
            "DailyGoalText",
            economyPanel.transform,
            Mathf.Max(14, expandedFontSize - 1),
            21f,
            new Color(0.34f, 0.78f, 1f)
        );
        dailyGoalText.horizontalOverflow = HorizontalWrapMode.Overflow;
        dailyGoalText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject progressionPanel = CreatePanel(
            "ProgressionPanel",
            compactSection.transform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -20f),
            new Vector2(440f, 92f),
            2f
        );
        CreateSectionTitle(
            "ProgressionTitle",
            progressionPanel.transform,
            "РЕПУТАЦИЯ И ПРОГРЕСС"
        );
        reputationText = CreateLine(
            "ReputationText",
            progressionPanel.transform,
            compactFontSize + 1,
            25f,
            new Color(0.24f, 0.68f, 1f)
        );
        clubLevelText = CreateLine(
            "ClubLevelText",
            progressionPanel.transform,
            compactFontSize,
            23f,
            new Color(0.82f, 0.9f, 1f)
        );

        GameObject operationsPanel = CreatePanel(
            "OperationsPanel",
            compactSection.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -20f),
            new Vector2(410f, 116f),
            2f
        );
        CreateSectionTitle(
            "OperationsTitle",
            operationsPanel.transform,
            "СОСТОЯНИЕ КЛУБА"
        );
        cleanlinessText = CreateLine(
            "CleanlinessText",
            operationsPanel.transform,
            compactFontSize,
            23f,
            new Color(0.3f, 1f, 0.38f)
        );
        clientQueueText = CreateLine(
            "ClientQueueText",
            operationsPanel.transform,
            compactFontSize,
            23f,
            new Color(0.82f, 0.9f, 1f)
        );
        bankruptcyRiskText = CreateLine(
            "BankruptcyRiskText",
            operationsPanel.transform,
            compactFontSize,
            23f,
            new Color(0.82f, 0.9f, 1f)
        );

        GameObject pcStatusPanel = CreatePanel(
            "PCStatusPanel",
            compactSection.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 20f),
            new Vector2(-40f, 56f),
            8f,
            true
        );
        CreateSectionTitle(
            "PCStatusTitle",
            pcStatusPanel.transform,
            "СТАТУС ПК"
        );
        pcStateText = CreateLine(
            "PCStateText",
            pcStatusPanel.transform,
            Mathf.Max(16, compactFontSize - 1),
            24f,
            new Color(0.82f, 0.9f, 1f)
        );
        pcTierText = CreateLine(
            "PCTierText",
            pcStatusPanel.transform,
            Mathf.Max(16, compactFontSize - 1),
            24f,
            new Color(0.3f, 1f, 0.38f)
        );

        // Compatibility containers keep the saved HUD mode and diagnostics API
        // intact without drawing the former permanent analytics panels.
        warningSection = CreateContainer(
            "WarningSection",
            gameplayHUDRoot.transform
        );
        warningText = CreateLine(
            "WarningText",
            warningSection.transform,
            1,
            1f,
            new Color(1f, 0.35f, 0.28f)
        );

        expandedSection = CreateContainer(
            "ExpandedSection",
            gameplayHUDRoot.transform
        );
        GameObject legacyData = CreateContainer(
            "LegacyDetailCache",
            expandedSection.transform
        );
        demandAnalyticsText = CreateLine("DemandAnalyticsText", legacyData.transform, 1);
        equipmentStatusText = CreateLine("EquipmentStatusText", legacyData.transform, 1);
        consumableStockText = CreateLine("ConsumableStockText", legacyData.transform, 1);
        pricingText = CreateLine("PricingText", legacyData.transform, 1);
        internetProviderText = CreateLine("InternetProviderText", legacyData.transform, 1);
        staffText = CreateLine("StaffText", legacyData.transform, 1);
        marketingText = CreateLine("MarketingText", legacyData.transform, 1);
        researchText = CreateLine("ResearchText", legacyData.transform, 1);
        roomStatusText = CreateLine("RoomStatusText", legacyData.transform, 1);
        expansionText = CreateLine("ExpansionText", legacyData.transform, 1);

        legacyData.SetActive(false);
        warningSection.SetActive(false);
        expandedSection.SetActive(false);
    }

    private GameObject CreateContainer(string name, Transform parent)
    {
        GameObject container = new GameObject(name, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        StretchToParent(container.GetComponent<RectTransform>());
        return container;
    }

    private GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        float spacing,
        bool horizontal = false)
    {
        GameObject section = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            horizontal
                ? typeof(HorizontalLayoutGroup)
                : typeof(VerticalLayoutGroup),
            typeof(Outline)
        );
        section.transform.SetParent(parent, false);

        RectTransform rect = section.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = section.GetComponent<Image>();
        image.color = new Color(0.018f, 0.04f, 0.07f, 0.94f);
        image.raycastTarget = false;

        HorizontalOrVerticalLayoutGroup layout = horizontal
            ? section.GetComponent<HorizontalLayoutGroup>()
            : section.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 8, 8);
        layout.spacing = spacing;
        layout.childAlignment = horizontal
            ? TextAnchor.MiddleLeft
            : TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Outline outline = section.GetComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.38f, 0.68f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        outline.useGraphicAlpha = true;
        return section;
    }

    private Text CreateSectionTitle(
        string name,
        Transform parent,
        string value)
    {
        Text title = CreateLine(
            name,
            parent,
            Mathf.Max(14, expandedFontSize - 1),
            22f,
            new Color(0.32f, 0.7f, 1f)
        );
        title.text = value;
        title.fontStyle = FontStyle.Bold;
        return title;
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
        element.flexibleWidth = 1f;
        return text;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
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
        panelRect.anchoredPosition = new Vector2(0f, 96f);
        panelRect.sizeDelta = new Vector2(760f, 58f);

        Image image = interactionPromptPanel.GetComponent<Image>();
        image.color = new Color(0.018f, 0.04f, 0.07f, 0.96f);
        image.raycastTarget = false;

        Outline outline = interactionPromptPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.38f, 0.68f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

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

    private void SubscribeToInteractionPromptSource()
    {
        ManagerModeController managerMode =
            FindAnyObjectByType<ManagerModeController>();
        interactionPromptSourceBehaviour = managerMode != null
            ? managerMode
            : FindAnyObjectByType<PlayerInteraction>();
        interactionPromptSource =
            interactionPromptSourceBehaviour as IInteractionPromptSource;

        if (interactionPromptSource == null)
        {
            return;
        }

        interactionPromptSource.PromptChanged += OnInteractionPromptChanged;
        currentInteractionPrompt = interactionPromptSource.CurrentPrompt;
    }

    private void UnsubscribeFromInteractionPromptSource()
    {
        if (interactionPromptSource != null)
        {
            interactionPromptSource.PromptChanged -= OnInteractionPromptChanged;
        }

        interactionPromptSource = null;
        interactionPromptSourceBehaviour = null;
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
            dailyGoalText.text = "Цель дня: —";
            return;
        }

        int progress = Mathf.Min(manager.CurrentProgress, manager.TargetValue);
        dailyGoalText.text = manager.GoalCompleted
            ? "Цель дня: выполнена"
            : $"Цель дня: {manager.GetGoalDescription()} · " +
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
        int reserved = 0;
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
            else if (pc.State == PCState.Broken || pc.HasBrokenEquipment)
                broken++;
            else if (pc.IsReserved)
                reserved++;
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
            $"Свободно {free} · Занято {occupied} · " +
            $"Резерв {reserved} · Сломано {broken}";
        pcTierText.text =
            $"Basic {basic} · Gaming {gaming} · Premium {premium}";
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
        RefreshBankruptcyRisk();

        // Detailed warnings are available through their contextual panels.
        // Keeping this compatibility object inactive avoids a fifth HUD block.
        warningSection.SetActive(false);
    }

    private void RefreshBankruptcyRisk()
    {
        if (bankruptcyRiskText == null)
        {
            return;
        }

        BankruptcyManager manager = BankruptcyManager.Instance;
        if (manager == null)
        {
            bankruptcyRiskText.text = "Риск банкротства: —";
            return;
        }

        int debtDays = manager.ConsecutiveDebtDays;
        int debtDaysToLose = manager.ConsecutiveDebtDaysToLose;
        bankruptcyRiskText.text =
            $"Риск банкротства: {debtDays}/{debtDaysToLose}";
        bankruptcyRiskText.color = debtDays > 0
            ? new Color(1f, 0.38f, 0.28f)
            : new Color(0.82f, 0.9f, 1f);
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
