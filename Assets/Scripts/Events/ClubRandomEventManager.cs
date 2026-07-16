using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ClubRandomEventManager : MonoBehaviour
{
    public static ClubRandomEventManager Instance { get; private set; }

    [Header("Generation")]
    [SerializeField, Range(0f, 1f)] private float dailyEventChance = 0.35f;
    [SerializeField, Min(0f)] private float minimumEventDelay = 15f;
    [SerializeField, Min(0f)] private float maximumEventDelay = 75f;

    [Header("Testing")]
    [SerializeField] private bool forceEvent;
    [SerializeField] private ClubRandomEventType forcedEvent;

    [Header("Internet outage")]
    [SerializeField, Min(1f)] private float internetOutageDuration = 25f;

    [Header("Power surge")]
    [SerializeField, Min(1)] private int minimumBrokenPCs = 1;
    [SerializeField, Min(1)] private int maximumBrokenPCs = 3;

    [Header("Inspection")]
    [SerializeField, Range(0f, 100f)] private float criticalEquipmentThreshold = 20f;
    [SerializeField, Min(0)] private int inspectionFinePerCriticalPC = 150;

    [Header("Temporary effects")]
    [SerializeField, Min(0.1f)] private float visitorRushDemandMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float viralPostDemandMultiplier = 1.15f;
    [SerializeField, Min(0.1f)] private float electricityCostMultiplier = 1.5f;

    [SerializeField] private ClubRandomEventState activeEvent = new();

    private float eventTimer;
    private bool eventRolledForCurrentDay;
    private string lastEventMessage = "Сегодня происшествий не было.";

    public ClubRandomEventType ActiveEventType => activeEvent != null
        ? activeEvent.eventType
        : ClubRandomEventType.None;
    public bool HasActiveEvent => ActiveEventType != ClubRandomEventType.None;
    public bool IsInternetUnavailable =>
        ActiveEventType == ClubRandomEventType.InternetOutage &&
        activeEvent.remainingSeconds > 0f;
    public int RemainingDays => activeEvent != null ? activeEvent.remainingDays : 0;
    public float RemainingSeconds => activeEvent != null ? activeEvent.remainingSeconds : 0f;
    public string LastEventMessage => lastEventMessage;
    public bool EventRolledForCurrentDay => eventRolledForCurrentDay;

    public event Action StatusChanged;
    public event Action<ClubRandomEventType, string> EventTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        activeEvent ??= new ClubRandomEventState();
        ResetDayEventTimer();
    }

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded += OnDayEnded;
        }
    }

    private void Update()
    {
        if (FirstDayTutorialManager.Instance != null &&
            FirstDayTutorialManager.Instance.SuppressRandomEvents)
        {
            return;
        }

        UpdateTimedEvent();

        if (eventRolledForCurrentDay || HasActiveEvent)
        {
            return;
        }

        eventTimer -= Time.deltaTime;
        if (eventTimer > 0f)
        {
            return;
        }

        eventRolledForCurrentDay = true;
        if (forceEvent)
        {
            TriggerEvent(forcedEvent);
            return;
        }

        if (UnityEngine.Random.value > dailyEventChance)
        {
            lastEventMessage = "Сегодня случайное событие не произошло.";
            StatusChanged?.Invoke();
            return;
        }

        TriggerRandomEvent();
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        minimumEventDelay = Mathf.Max(0f, minimumEventDelay);
        maximumEventDelay = Mathf.Max(minimumEventDelay, maximumEventDelay);
        internetOutageDuration = Mathf.Max(1f, internetOutageDuration);
        minimumBrokenPCs = Mathf.Max(1, minimumBrokenPCs);
        maximumBrokenPCs = Mathf.Max(minimumBrokenPCs, maximumBrokenPCs);
        inspectionFinePerCriticalPC = Mathf.Max(0, inspectionFinePerCriticalPC);
        visitorRushDemandMultiplier = Mathf.Max(0.1f, visitorRushDemandMultiplier);
        viralPostDemandMultiplier = Mathf.Max(0.1f, viralPostDemandMultiplier);
        electricityCostMultiplier = Mathf.Max(0.1f, electricityCostMultiplier);
    }

    public float GetDemandMultiplier()
    {
        return ActiveEventType switch
        {
            ClubRandomEventType.VisitorRush => visitorRushDemandMultiplier,
            ClubRandomEventType.ViralPost => viralPostDemandMultiplier,
            _ => 1f
        };
    }

    public float GetElectricityCostMultiplier()
    {
        return ActiveEventType == ClubRandomEventType.ElectricityPriceIncrease
            ? electricityCostMultiplier
            : 1f;
    }

    private void TriggerRandomEvent()
    {
        ClubRandomEventType[] possibleEvents =
        {
            ClubRandomEventType.VisitorRush,
            ClubRandomEventType.InternetOutage,
            ClubRandomEventType.PowerSurge,
            ClubRandomEventType.EquipmentInspection,
            ClubRandomEventType.ViralPost,
            ClubRandomEventType.ElectricityPriceIncrease
        };

        TriggerEvent(possibleEvents[UnityEngine.Random.Range(0, possibleEvents.Length)]);
    }

    public void TriggerEvent(ClubRandomEventType eventType)
    {
        if (eventType == ClubRandomEventType.None)
        {
            return;
        }

        eventRolledForCurrentDay = true;
        ClearActiveEvent();

        switch (eventType)
        {
            case ClubRandomEventType.VisitorRush:
                StartDayEvent(eventType, "Наплыв посетителей: поток клиентов увеличен.");
                break;
            case ClubRandomEventType.InternetOutage:
                ConfigureInternetOutage(
                    internetOutageDuration,
                    "Сбой интернет-линии."
                );
                break;
            case ClubRandomEventType.PowerSurge:
                ApplyPowerSurge();
                break;
            case ClubRandomEventType.EquipmentInspection:
                ApplyEquipmentInspection();
                break;
            case ClubRandomEventType.ViralPost:
                ApplyViralPost();
                break;
            case ClubRandomEventType.ElectricityPriceIncrease:
                StartDayEvent(eventType, "Повышенный тариф на электричество.");
                break;
        }

        Debug.Log(lastEventMessage);
        EventTriggered?.Invoke(eventType, lastEventMessage);
        StatusChanged?.Invoke();
    }

    public bool TriggerInternetOutage(float duration, string sourceMessage)
    {
        if (HasActiveEvent)
        {
            return false;
        }

        ConfigureInternetOutage(duration, sourceMessage);
        Debug.Log(lastEventMessage);
        EventTriggered?.Invoke(
            ClubRandomEventType.InternetOutage,
            lastEventMessage
        );
        StatusChanged?.Invoke();
        return true;
    }

    private void ConfigureInternetOutage(float duration, string sourceMessage)
    {
        activeEvent.eventType = ClubRandomEventType.InternetOutage;
        activeEvent.remainingSeconds = Mathf.Max(1f, duration);
        activeEvent.remainingDays = 0;
        lastEventMessage =
            $"{sourceMessage} Интернет недоступен " +
            $"{activeEvent.remainingSeconds:F0} сек.";
    }

    private void StartDayEvent(ClubRandomEventType eventType, string message)
    {
        activeEvent.eventType = eventType;
        activeEvent.remainingDays = 1;
        activeEvent.remainingSeconds = 0f;
        lastEventMessage = message;
    }

    private void UpdateTimedEvent()
    {
        if (ActiveEventType != ClubRandomEventType.InternetOutage)
        {
            return;
        }

        activeEvent.remainingSeconds -= Time.deltaTime;
        if (activeEvent.remainingSeconds > 0f)
        {
            return;
        }

        lastEventMessage = "Интернет восстановлен.";
        ClearActiveEvent();
        StatusChanged?.Invoke();
    }

    private void ApplyPowerSurge()
    {
        List<PC> candidates = new();
        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (pc != null && pc.IsAvailable)
            {
                candidates.Add(pc);
            }
        }

        int requestedCount = UnityEngine.Random.Range(
            minimumBrokenPCs,
            maximumBrokenPCs + 1
        );
        int brokenCount = Mathf.Min(requestedCount, candidates.Count);

        for (int index = 0; index < brokenCount; index++)
        {
            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            PC selectedPC = candidates[randomIndex];
            candidates.RemoveAt(randomIndex);
            selectedPC.ForceBreakdown();
        }

        lastEventMessage = $"Перепад напряжения: сломано ПК - {brokenCount}.";
        ClearActiveEvent();
    }

    private void ApplyEquipmentInspection()
    {
        int criticalPCCount = 0;
        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (pc != null && pc.HasRoomAccess &&
                pc.LowestEquipmentCondition <= criticalEquipmentThreshold)
            {
                criticalPCCount++;
            }
        }

        int totalFine = criticalPCCount * inspectionFinePerCriticalPC;
        if (totalFine > 0)
        {
            EconomyManager.Instance?.ApplyMandatoryExpense(
                totalFine,
                EconomyTransactionCategory.RandomEventExpense
            );
            lastEventMessage =
                $"Проверка оборудования: штраф {totalFine} ₽ за {criticalPCCount} ПК.";
        }
        else
        {
            lastEventMessage = "Проверка оборудования пройдена без штрафа.";
        }

        ClearActiveEvent();
    }

    private void ApplyViralPost()
    {
        ClubReputationManager.Instance?.AddReputation(5);
        StartDayEvent(
            ClubRandomEventType.ViralPost,
            "Удачная публикация: репутация +5, поток клиентов увеличен."
        );
    }

    private void OnDayEnded(int completedDay, int income, int expenses, int result)
    {
        if (activeEvent != null && activeEvent.remainingDays > 0)
        {
            activeEvent.remainingDays--;
            if (activeEvent.remainingDays <= 0)
            {
                ClubRandomEventType completed = activeEvent.eventType;
                ClearActiveEvent();
                lastEventMessage = $"{GetEventDisplayName(completed)}: эффект завершен.";
            }
        }

        eventRolledForCurrentDay = false;
        ResetDayEventTimer();
        StatusChanged?.Invoke();
    }

    private void ResetDayEventTimer()
    {
        float minimum = Mathf.Min(minimumEventDelay, maximumEventDelay);
        float maximum = Mathf.Max(minimumEventDelay, maximumEventDelay);
        eventTimer = UnityEngine.Random.Range(minimum, maximum);
    }

    private void ClearActiveEvent()
    {
        activeEvent ??= new ClubRandomEventState();
        activeEvent.eventType = ClubRandomEventType.None;
        activeEvent.remainingDays = 0;
        activeEvent.remainingSeconds = 0f;
    }

    public ClubRandomEventState CreateSaveData()
    {
        return activeEvent?.Clone();
    }

    public void RestoreState(ClubRandomEventState savedState, bool savedEventRolled)
    {
        activeEvent = savedState?.Clone() ?? new ClubRandomEventState();
        eventRolledForCurrentDay = savedEventRolled;
        if (!HasActiveEvent)
        {
            ClearActiveEvent();
        }

        ResetDayEventTimer();
        lastEventMessage = HasActiveEvent
            ? GetActiveEventStatus()
            : "Активного события нет.";
        StatusChanged?.Invoke();
    }

    public string GetActiveEventStatus()
    {
        return ActiveEventType switch
        {
            ClubRandomEventType.InternetOutage =>
                $"Сбой интернета: {activeEvent.remainingSeconds:F0} сек.",
            ClubRandomEventType.None => "Событие: нет",
            _ => $"{GetEventDisplayName(ActiveEventType)}: до конца дня"
        };
    }

    public static string GetEventDisplayName(ClubRandomEventType eventType)
    {
        return eventType switch
        {
            ClubRandomEventType.VisitorRush => "Наплыв посетителей",
            ClubRandomEventType.InternetOutage => "Сбой интернета",
            ClubRandomEventType.PowerSurge => "Перепад напряжения",
            ClubRandomEventType.EquipmentInspection => "Проверка оборудования",
            ClubRandomEventType.ViralPost => "Удачная публикация",
            ClubRandomEventType.ElectricityPriceIncrease =>
                "Повышенный тариф электричества",
            _ => "Нет события"
        };
    }
}
