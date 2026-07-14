using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ClubHUDCanvas : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private Vector2 referenceResolution =
        new Vector2(1920f, 1080f);

    [SerializeField, Range(0f, 1f)]
    private float widthHeightMatch = 0.5f;

    [Header("Text Settings")]
    [SerializeField] private int fontSize = 22;

    private readonly List<PC> pcs = new();

    private Text balanceText;
    private Text clubLevelText;
    private Text pcStateText;
    private Text reputationText;
    private Text dayText;
    private Text dailyGoalText;
    private Text dayReportText;
    private Text financialRiskText;
    private Text expansionText;
    private Text pcTierText;

    private GameObject interactionPromptPanel;
    private Text interactionPromptText;

    private PlayerInteraction playerInteraction;
    private string currentInteractionPrompt = string.Empty;
    private string lastDayReport = "Итоги прошлого дня: пока нет";
    private Font runtimeFont;

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        SubscribeToManagers();
        RegisterExistingPCs();
        SubscribeToPlayerInteraction();
        RefreshAll();
    }

    private void Update()
    {
        RefreshDayTimer();
        RefreshInteractionPromptVisibility();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManagers();
        UnsubscribeFromPCs();
        UnsubscribeFromPlayerInteraction();
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

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        CreateInformationPanel();
        CreateInteractionPrompt();
    }

    private void CreateInformationPanel()
    {
        GameObject panelObject = new GameObject(
            "InformationPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );

        panelObject.transform.SetParent(transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(850f, 0f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.03f, 0.04f, 0.06f, 0.82f);
        panelImage.raycastTarget = false;

        VerticalLayoutGroup layout =
            panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            panelObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform panelTransform = panelObject.transform;
        balanceText = CreateInformationLine("BalanceText", panelTransform);
        clubLevelText = CreateInformationLine(
            "ClubLevelText",
            panelTransform
        );
        pcStateText = CreateInformationLine("PCStateText", panelTransform);
        reputationText = CreateInformationLine("ReputationText", panelTransform);
        dayText = CreateInformationLine("DayText", panelTransform);
        dailyGoalText = CreateInformationLine(
            "DailyGoalText",
            panelTransform,
            54f
        );
        dayReportText = CreateInformationLine("DayReportText", panelTransform, 54f);
        financialRiskText =
            CreateInformationLine("FinancialRiskText", panelTransform);
        expansionText = CreateInformationLine("ExpansionText", panelTransform);
        pcTierText = CreateInformationLine("PCTierText", panelTransform, 54f);
    }

    private Text CreateInformationLine(
        string objectName,
        Transform parent,
        float preferredHeight = 32f)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Text),
            typeof(LayoutElement)
        );

        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = runtimeFont;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        return text;
    }

    private void CreateInteractionPrompt()
    {
        interactionPromptPanel = new GameObject(
            "InteractionPromptPanel",
            typeof(RectTransform),
            typeof(Image)
        );

        interactionPromptPanel.transform.SetParent(transform, false);

        RectTransform panelRect =
            interactionPromptPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 30f);
        panelRect.sizeDelta = new Vector2(820f, 62f);

        Image panelImage = interactionPromptPanel.GetComponent<Image>();
        panelImage.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject(
            "InteractionPromptText",
            typeof(RectTransform),
            typeof(Text)
        );

        textObject.transform.SetParent(interactionPromptPanel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);

        interactionPromptText = textObject.GetComponent<Text>();
        interactionPromptText.font = runtimeFont;
        interactionPromptText.fontSize = fontSize;
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
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }

        if (ClubProgressionManager.Instance != null)
        {
            ClubProgressionManager.Instance.StatusChanged +=
                RefreshClubProgression;
        }

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged += RefreshReputation;
        }

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded += OnDayEnded;
        }

        if (DailyGoalManager.Instance != null)
        {
            DailyGoalManager.Instance.StatusChanged += RefreshDailyGoal;
        }

        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.StatusChanged += RefreshFinancialRisk;
        }

        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged += RefreshExpansion;
        }

        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;
    }

    private void UnsubscribeFromManagers()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        }

        if (ClubProgressionManager.Instance != null)
        {
            ClubProgressionManager.Instance.StatusChanged -=
                RefreshClubProgression;
        }

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged -= RefreshReputation;
        }

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }

        if (DailyGoalManager.Instance != null)
        {
            DailyGoalManager.Instance.StatusChanged -= RefreshDailyGoal;
        }

        if (BankruptcyManager.Instance != null)
        {
            BankruptcyManager.Instance.StatusChanged -= RefreshFinancialRisk;
        }

        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= RefreshExpansion;
        }

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
        RefreshPCInformation();
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.StateChanged -= OnPCStateChanged;
        pc.TierChanged -= OnPCTierChanged;
        pcs.Remove(pc);
        RefreshPCInformation();
    }

    private void UnsubscribeFromPCs()
    {
        foreach (PC pc in pcs)
        {
            if (pc != null)
            {
                pc.StateChanged -= OnPCStateChanged;
                pc.TierChanged -= OnPCTierChanged;
            }
        }

        pcs.Clear();
    }

    private void SubscribeToPlayerInteraction()
    {
        playerInteraction = FindAnyObjectByType<PlayerInteraction>();

        if (playerInteraction == null)
        {
            Debug.LogWarning(
                "PlayerInteraction не найден. Canvas-подсказка отключена."
            );
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

    private void RefreshAll()
    {
        RefreshBalance();
        RefreshClubProgression();
        RefreshPCInformation();
        RefreshReputation();
        RefreshDayTimer();
        RefreshDailyGoal();
        RefreshFinancialRisk();
        RefreshExpansion();
        RefreshInteractionPromptVisibility();
    }

    private void OnMoneyChanged(int newBalance)
    {
        balanceText.text = $"Баланс: {newBalance} ₽";
    }

    private void RefreshBalance()
    {
        int balance = EconomyManager.Instance != null
            ? EconomyManager.Instance.Money
            : 0;

        balanceText.text = $"Баланс: {balance} ₽";
    }

    private void RefreshClubProgression()
    {
        if (clubLevelText == null)
        {
            return;
        }

        ClubProgressionManager manager = ClubProgressionManager.Instance;

        if (manager == null)
        {
            clubLevelText.text = "Уровень клуба: недоступен";
            return;
        }

        if (manager.IsMaxLevel)
        {
            clubLevelText.text =
                $"Уровень клуба: {manager.Level} — максимальный";
            return;
        }

        clubLevelText.text =
            $"Уровень клуба: {manager.Level} | " +
            $"Опыт: {manager.Experience}/" +
            $"{manager.ExperienceToNextLevel}";
    }

    private void OnPCStateChanged(PCState newState)
    {
        RefreshPCInformation();
    }

    private void OnPCTierChanged(PCTier newTier)
    {
        RefreshPCInformation();
    }

    private void RefreshPCInformation()
    {
        pcs.RemoveAll(pc => pc == null);

        int freeCount = 0;
        int occupiedCount = 0;
        int brokenCount = 0;
        int basicCount = 0;
        int gamingCount = 0;
        int premiumCount = 0;

        foreach (PC pc in pcs)
        {
            switch (pc.State)
            {
                case PCState.Free:
                    freeCount++;
                    break;
                case PCState.Occupied:
                    occupiedCount++;
                    break;
                case PCState.Broken:
                    brokenCount++;
                    break;
            }

            switch (pc.Tier)
            {
                case PCTier.Basic:
                    basicCount++;
                    break;
                case PCTier.Gaming:
                    gamingCount++;
                    break;
                case PCTier.Premium:
                    premiumCount++;
                    break;
            }
        }

        pcStateText.text =
            $"Свободно: {freeCount} | " +
            $"Занято: {occupiedCount} | " +
            $"Сломано: {brokenCount}";

        pcTierText.text =
            $"ПК: Basic {basicCount} | " +
            $"Gaming {gamingCount} | " +
            $"Premium {premiumCount}\n" +
            $"Улучшения: {PC.BasicToGamingUpgradeCost} ₽ / " +
            $"{PC.GamingToPremiumUpgradeCost} ₽";
    }

    private void RefreshReputation()
    {
        ClubReputationManager manager = ClubReputationManager.Instance;

        if (manager == null)
        {
            reputationText.text = "Репутация: недоступна";
            return;
        }

        reputationText.text =
            $"Репутация: {manager.Reputation}/100 | " +
            $"Обслужено: {manager.ServedClients} | " +
            $"Потеряно: {manager.LostClients}";
    }

    private void RefreshDayTimer()
    {
        GameDayManager manager = GameDayManager.Instance;

        if (manager == null)
        {
            return;
        }

        int remainingSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt(manager.TimeRemaining)
        );

        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;

        dayText.text =
            $"День: {manager.CurrentDay} | " +
            $"До конца дня: {minutes:00}:{seconds:00}";

        dayReportText.text = lastDayReport;
    }

    private void RefreshDailyGoal()
    {
        if (dailyGoalText == null)
        {
            return;
        }

        DailyGoalManager manager = DailyGoalManager.Instance;

        if (manager == null)
        {
            dailyGoalText.text = "Цель дня: недоступна";
            return;
        }

        int displayedProgress = Mathf.Min(
            manager.CurrentProgress,
            manager.TargetValue
        );

        if (manager.GoalCompleted)
        {
            dailyGoalText.text =
                $"Цель дня выполнена: {manager.GetGoalDescription()}\n" +
                $"Награда получена: {manager.RewardMoney} ₽";
            return;
        }

        dailyGoalText.text =
            $"Цель дня: {manager.GetGoalDescription()} | " +
            $"{displayedProgress}/{manager.TargetValue}\n" +
            $"Награда: {manager.RewardMoney} ₽";
    }

    private void OnDayEnded(
        int completedDay,
        int income,
        int expenses,
        int profit)
    {
        string profitPrefix = profit >= 0 ? "+" : string.Empty;

        lastDayReport =
            $"День {completedDay}: " +
            $"доход {income} ₽ | " +
            $"расходы {expenses} ₽ | " +
            $"итог {profitPrefix}{profit} ₽";

        RefreshDayTimer();
    }

    private void RefreshFinancialRisk()
    {
        BankruptcyManager manager = BankruptcyManager.Instance;

        if (manager == null)
        {
            financialRiskText.text = "Финансовый риск: недоступен";
            return;
        }

        if (manager.ConsecutiveDebtDays == 0)
        {
            financialRiskText.text =
                $"Финансовый риск: отсутствует | " +
                $"Порог: {manager.BankruptcyThreshold} ₽";
            return;
        }

        financialRiskText.text =
            $"Критический долг: " +
            $"{manager.ConsecutiveDebtDays}/" +
            $"{manager.ConsecutiveDebtDaysToLose} дней | " +
            $"Порог: {manager.BankruptcyThreshold} ₽";
    }

    private void RefreshExpansion()
    {
        PCExpansionManager manager = PCExpansionManager.Instance;

        if (manager == null)
        {
            expansionText.text = "Расширение клуба: недоступно";
            return;
        }

        expansionText.text =
            $"Новый ПК: {manager.PurchaseCost} ₽ | " +
            $"Доступно мест: {manager.RemainingSlots} | " +
            $"Открыто: {manager.UnlockedSlotCount}/" +
            $"{manager.TotalExpansionSlots}";
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

        bool pauseMenuOpen = PauseMenuController.Instance != null &&
            PauseMenuController.Instance.IsMenuOpen;

        bool shouldShow = !pauseMenuOpen &&
            !string.IsNullOrWhiteSpace(currentInteractionPrompt);

        if (interactionPromptPanel.activeSelf != shouldShow)
        {
            interactionPromptPanel.SetActive(shouldShow);
        }

        if (shouldShow)
        {
            interactionPromptText.text = currentInteractionPrompt;
        }
    }

    private void OnValidate()
    {
        referenceResolution.x = Mathf.Max(640f, referenceResolution.x);
        referenceResolution.y = Mathf.Max(360f, referenceResolution.y);
        fontSize = Mathf.Max(12, fontSize);
    }
}
