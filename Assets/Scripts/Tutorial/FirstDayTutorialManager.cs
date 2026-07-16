using System;
using System.Collections;
using UnityEngine;

public sealed class FirstDayTutorialManager : MonoBehaviour
{
    public static FirstDayTutorialManager Instance { get; private set; }

    private TutorialStepDefinition[] steps;
    private int currentStepIndex;
    private int currentProgress;
    private bool tutorialStarted;
    private bool tutorialCompleted;
    private bool tutorialRepairStepPrepared;
    private bool firstDayRestrictionsActive;
    private TutorialWorldMarker worldMarker;

    public bool IsTutorialActive => tutorialStarted && !tutorialCompleted;
    public bool IsTutorialCompleted => tutorialCompleted;
    public bool TutorialStarted => tutorialStarted;
    public int CurrentStepIndex => currentStepIndex;
    public int CurrentProgress => currentProgress;
    public bool TutorialRepairStepPrepared => tutorialRepairStepPrepared;
    public TutorialStepDefinition CurrentStep => steps != null &&
        currentStepIndex >= 0 && currentStepIndex < steps.Length
            ? steps[currentStepIndex] : null;

    public bool ShouldForceTutorialClient => IsTutorialActive && CurrentStep != null &&
        (CurrentStep.StepType == TutorialStepType.WaitForFirstClient ||
         CurrentStep.StepType == TutorialStepType.CompleteFirstSession);
    public bool ShouldForceTutorialTrash => IsTutorialActive && CurrentStep != null &&
        CurrentStep.StepType == TutorialStepType.CompleteFirstSession;
    public bool SuppressRandomEvents => IsTutorialActive || firstDayRestrictionsActive;
    public bool SuppressProviderFailures => IsTutorialActive || firstDayRestrictionsActive;
    public bool SuppressAdvancedClients => IsTutorialActive || firstDayRestrictionsActive;
    public bool SuppressMarketingEffects => IsTutorialActive || firstDayRestrictionsActive;

    public event Action StepChanged;
    public event Action TutorialCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CreateDefaultSteps();
    }

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;
        foreach (PC pc in FindObjectsByType<PC>()) RegisterPC(pc);
        if (GameDayManager.Instance != null)
            GameDayManager.Instance.DayEnded += OnDayEnded;
        RefreshWorldMarker();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;
        foreach (PC pc in FindObjectsByType<PC>()) UnregisterPC(pc);
        if (GameDayManager.Instance != null)
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        RemoveWorldMarker();
        if (Instance == this) Instance = null;
    }

    public void StartTutorial()
    {
        tutorialStarted = true;
        tutorialCompleted = false;
        tutorialRepairStepPrepared = false;
        firstDayRestrictionsActive = true;
        currentStepIndex = 0;
        currentProgress = 0;
        GameDayManager.Instance?.InitializeNewGameDay(true);
        PrepareCurrentStep();
        StepChanged?.Invoke();
    }

    public void ReportAction(TutorialStepType action, int amount = 1)
    {
        if (!IsTutorialActive || CurrentStep == null || CurrentStep.StepType != action)
            return;
        currentProgress = Mathf.Clamp(currentProgress + Mathf.Max(1, amount),
            0, CurrentStep.RequiredProgress);
        if (currentProgress >= CurrentStep.RequiredProgress) AdvanceStep();
        else StepChanged?.Invoke();
    }

    public void SkipTutorial()
    {
        if (!IsTutorialActive) return;
        tutorialStarted = true;
        tutorialCompleted = true;
        currentStepIndex = steps.Length;
        currentProgress = 0;
        firstDayRestrictionsActive = true;
        RemoveWorldMarker();
        StepChanged?.Invoke();
        TutorialCompleted?.Invoke();
    }

    public void RestoreState(bool savedStarted, bool savedCompleted,
        int savedStepIndex, int savedProgress, bool repairStepPrepared)
    {
        tutorialStarted = savedStarted;
        tutorialCompleted = savedCompleted;
        tutorialRepairStepPrepared = repairStepPrepared;
        firstDayRestrictionsActive = IsTutorialActive;
        currentStepIndex = tutorialCompleted
            ? steps.Length
            : Mathf.Clamp(savedStepIndex, 0, steps.Length - 1);
        int required = CurrentStep != null ? CurrentStep.RequiredProgress : 1;
        currentProgress = Mathf.Clamp(savedProgress, 0, required - 1);
        if (IsTutorialActive) PrepareCurrentStep();
        else RemoveWorldMarker();
        StepChanged?.Invoke();
    }

    public void RestoreFirstDayRestrictions(int savedCurrentDay)
    {
        firstDayRestrictionsActive = IsTutorialActive ||
            (tutorialStarted && tutorialCompleted && savedCurrentDay == 1);
    }

    private void AdvanceStep()
    {
        currentStepIndex++;
        currentProgress = 0;
        if (currentStepIndex >= steps.Length)
        {
            CompleteTutorial();
            return;
        }
        PrepareCurrentStep();
        StepChanged?.Invoke();
    }

    private void CompleteTutorial()
    {
        if (tutorialCompleted) return;
        tutorialCompleted = true;
        currentStepIndex = steps.Length;
        currentProgress = 0;
        firstDayRestrictionsActive = false;
        RemoveWorldMarker();
        EconomyManager.Instance?.AddBonusMoney(500,
            EconomyTransactionCategory.TutorialReward);
        ClubProgressionManager.Instance?.AddExperience(50);
        ClubReputationManager.Instance?.AddReputation(3);
        TutorialCompleted?.Invoke();
        StepChanged?.Invoke();
    }

    private void PrepareCurrentStep()
    {
        if (CurrentStep?.StepType == TutorialStepType.RepairEquipment &&
            !tutorialRepairStepPrepared)
        {
            GameObject pcObject = GameObject.Find("PC_01");
            PC pc = pcObject != null ? pcObject.GetComponent<PC>() : null;
            pc?.SetEquipmentCondition(PCEquipmentType.Mouse, 40f);
            tutorialRepairStepPrepared = true;
        }
        RefreshWorldMarker();
        StartCoroutine(RefreshWorldMarkerNextFrame());
    }

    private IEnumerator RefreshWorldMarkerNextFrame()
    {
        yield return null;
        RefreshWorldMarker();
    }

    private void RegisterPC(PC pc)
    {
        if (pc != null)
        {
            pc.SessionCompleted -= OnPCSessionCompleted;
            pc.SessionCompleted += OnPCSessionCompleted;
        }
    }

    private void UnregisterPC(PC pc)
    {
        if (pc != null) pc.SessionCompleted -= OnPCSessionCompleted;
    }

    private void OnPCSessionCompleted(PC pc)
    {
        if (ShouldForceTutorialTrash)
            ClubCleanlinessManager.Instance?.EnsureTutorialTrash(pc);
        ReportAction(TutorialStepType.CompleteFirstSession);
        ReportAction(TutorialStepType.ServeClients);
    }

    private void OnDayEnded(int _, int __, int ___, int ____)
    {
        ReportAction(TutorialStepType.FinishDay);
        if (tutorialCompleted && firstDayRestrictionsActive)
            firstDayRestrictionsActive = false;
    }

    private void RefreshWorldMarker()
    {
        RemoveWorldMarker();
        Transform target = FindMarkerTarget();
        if (!IsTutorialActive || target == null) return;
        GameObject markerObject = new GameObject("TutorialWorldMarker");
        worldMarker = markerObject.AddComponent<TutorialWorldMarker>();
        worldMarker.Initialize(target);
    }

    private Transform FindMarkerTarget()
    {
        if (CurrentStep == null) return null;
        GameObject target = CurrentStep.StepType switch
        {
            TutorialStepType.ApproachPC => GameObject.Find("PC_01"),
            TutorialStepType.RepairEquipment =>
                GameObject.Find("PCMaintenanceTerminal") ?? GameObject.Find("MaintenanceTerminal"),
            TutorialStepType.RestockEnergyDrinks => GameObject.Find("ConsumableStockTerminal"),
            TutorialStepType.ChangeBasicPrice => GameObject.Find("PricingTerminal"),
            _ => null
        };
        if (CurrentStep.StepType == TutorialStepType.CleanTrash &&
            ClubCleanlinessManager.Instance != null &&
            ClubCleanlinessManager.Instance.ActiveTrashItems.Count > 0)
        {
            TrashItem trash = ClubCleanlinessManager.Instance.ActiveTrashItems[0];
            return trash != null ? trash.transform : null;
        }
        return target != null ? target.transform : null;
    }

    private void RemoveWorldMarker()
    {
        if (worldMarker != null) Destroy(worldMarker.gameObject);
        worldMarker = null;
    }

    private void CreateDefaultSteps()
    {
        steps = new[]
        {
            new TutorialStepDefinition(TutorialStepType.ApproachPC, "Осмотрите игровой зал", "Подойдите к любому компьютеру.", "Подойти к ПК"),
            new TutorialStepDefinition(TutorialStepType.WaitForFirstClient, "Первый посетитель", "Дождитесь прихода первого клиента.", "Дождаться клиента"),
            new TutorialStepDefinition(TutorialStepType.CompleteFirstSession, "Первая сессия", "Клиент автоматически займет подходящий ПК.", "Завершить первую сессию"),
            new TutorialStepDefinition(TutorialStepType.CleanTrash, "Поддерживайте чистоту", "После посетителей может оставаться мусор.", "Убрать мусор"),
            new TutorialStepDefinition(TutorialStepType.RepairEquipment, "Обслуживание оборудования", "Используйте синий терминал или подойдите к ПК.", "Отремонтировать мышь"),
            new TutorialStepDefinition(TutorialStepType.RestockEnergyDrinks, "Пополнение склада", "Купите упаковку энергетиков.", "Купить энергетики"),
            new TutorialStepDefinition(TutorialStepType.ChangeBasicPrice, "Настройка тарифа", "Установите тариф Basic на 110%.", "Basic: 110%"),
            new TutorialStepDefinition(TutorialStepType.ServeClients, "Самостоятельная работа", "Обслужите еще трех посетителей.", "Обслужено", 3),
            new TutorialStepDefinition(TutorialStepType.FinishDay, "Завершение смены", "Продолжайте работу до конца игрового дня.", "Дождаться конца дня"),
            new TutorialStepDefinition(TutorialStepType.ReviewFinancialReport, "Результаты дня", "Изучите финансовый отчет и продолжите игру.", "Закрыть отчет")
        };
    }
}
