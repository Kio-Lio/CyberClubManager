using System;
using UnityEngine;

public enum DailyGoalType
{
    ServeClients,
    EarnIncome,
    ReachReputation
}

public sealed class DailyGoalManager : MonoBehaviour
{
    public static DailyGoalManager Instance { get; private set; }

    private int activeGoalDay;
    private DailyGoalType goalType;
    private int targetValue;
    private int rewardMoney;
    private int servedClientsBaseline;
    private int incomeBaseline;
    private bool goalCompleted;
    private bool stateRestored;

    public int ActiveGoalDay => activeGoalDay;
    public DailyGoalType GoalType => goalType;
    public int TargetValue => targetValue;
    public int RewardMoney => rewardMoney;
    public int ServedClientsBaseline => servedClientsBaseline;
    public int IncomeBaseline => incomeBaseline;
    public bool GoalCompleted => goalCompleted;
    public int CurrentProgress => CalculateProgress();

    public event Action StatusChanged;
    public event Action GoalCompletedEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        SubscribeToEvents();

        if (!stateRestored)
        {
            int currentDay = GameDayManager.Instance != null
                ? GameDayManager.Instance.CurrentDay
                : 1;

            BeginGoalForDay(currentDay);
        }
        else
        {
            RefreshProgress();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeToEvents()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged += OnReputationChanged;
        }

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded += OnDayEnded;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        }

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged -= OnReputationChanged;
        }

        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }
    }

    private void OnMoneyChanged(int newBalance)
    {
        RefreshProgress();
    }

    private void OnReputationChanged()
    {
        RefreshProgress();
    }

    private void OnDayEnded(
        int completedDay,
        int income,
        int expenses,
        int profit)
    {
        int nextDay = GameDayManager.Instance != null
            ? GameDayManager.Instance.CurrentDay
            : completedDay + 1;

        BeginGoalForDay(nextDay);
    }

    private void BeginGoalForDay(int day)
    {
        activeGoalDay = Mathf.Max(1, day);

        int progressionCycle = (activeGoalDay - 1) / 3;
        int goalIndex = (activeGoalDay - 1) % 3;

        switch (goalIndex)
        {
            case 0:
                goalType = DailyGoalType.ServeClients;
                targetValue = 5 + progressionCycle * 2;
                rewardMoney = 200 + progressionCycle * 50;
                break;

            case 1:
                goalType = DailyGoalType.EarnIncome;
                targetValue = 700 + progressionCycle * 200;
                rewardMoney = 250 + progressionCycle * 75;
                break;

            default:
                goalType = DailyGoalType.ReachReputation;
                targetValue = Mathf.Min(80, 55 + progressionCycle * 5);
                rewardMoney = 300 + progressionCycle * 100;
                break;
        }

        servedClientsBaseline = ClubReputationManager.Instance != null
            ? ClubReputationManager.Instance.ServedClients
            : 0;

        incomeBaseline = EconomyManager.Instance != null
            ? EconomyManager.Instance.TotalIncome
            : 0;

        goalCompleted = false;
        stateRestored = true;

        Debug.Log(
            $"Новая цель на день {activeGoalDay}: " +
            $"{GetGoalDescription()}. " +
            $"Награда: {rewardMoney} ₽."
        );

        StatusChanged?.Invoke();
        RefreshProgress();
    }

    private void RefreshProgress()
    {
        StatusChanged?.Invoke();

        if (goalCompleted || CurrentProgress < targetValue)
        {
            return;
        }

        CompleteGoal();
    }

    private void CompleteGoal()
    {
        if (goalCompleted)
        {
            return;
        }

        goalCompleted = true;

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddBonusMoney(
                rewardMoney,
                EconomyTransactionCategory.DailyGoalReward
            );
        }
        else
        {
            Debug.LogWarning(
                "EconomyManager не найден. Награда за цель не выдана."
            );
        }

        Debug.Log(
            $"Цель дня выполнена: {GetGoalDescription()}. " +
            $"Награда: {rewardMoney} ₽."
        );

        StatusChanged?.Invoke();
        GoalCompletedEvent?.Invoke();
    }

    private int CalculateProgress()
    {
        switch (goalType)
        {
            case DailyGoalType.ServeClients:
                return ClubReputationManager.Instance == null
                    ? 0
                    : Mathf.Max(
                        0,
                        ClubReputationManager.Instance.ServedClients -
                        servedClientsBaseline
                    );

            case DailyGoalType.EarnIncome:
                return EconomyManager.Instance == null
                    ? 0
                    : Mathf.Max(
                        0,
                        EconomyManager.Instance.TotalIncome - incomeBaseline
                    );

            case DailyGoalType.ReachReputation:
                return ClubReputationManager.Instance != null
                    ? ClubReputationManager.Instance.Reputation
                    : 0;

            default:
                return 0;
        }
    }

    public string GetGoalDescription()
    {
        return goalType switch
        {
            DailyGoalType.ServeClients =>
                $"обслужить {targetValue} клиентов",
            DailyGoalType.EarnIncome =>
                $"получить {targetValue} ₽ выручки",
            DailyGoalType.ReachReputation =>
                $"достичь репутации {targetValue}/100",
            _ => "неизвестная цель"
        };
    }

    public void RestoreState(
        int savedActiveGoalDay,
        int savedGoalType,
        int savedTargetValue,
        int savedRewardMoney,
        int savedServedClientsBaseline,
        int savedIncomeBaseline,
        bool savedGoalCompleted)
    {
        bool validGoalType = Enum.IsDefined(
            typeof(DailyGoalType),
            savedGoalType
        );

        int currentDay = GameDayManager.Instance != null
            ? GameDayManager.Instance.CurrentDay
            : 1;

        if (!validGoalType ||
            savedActiveGoalDay != currentDay ||
            savedTargetValue <= 0 ||
            savedRewardMoney <= 0)
        {
            BeginGoalForDay(currentDay);
            return;
        }

        activeGoalDay = savedActiveGoalDay;
        goalType = (DailyGoalType)savedGoalType;
        targetValue = savedTargetValue;
        rewardMoney = savedRewardMoney;
        servedClientsBaseline = Mathf.Max(0, savedServedClientsBaseline);
        incomeBaseline = Mathf.Max(0, savedIncomeBaseline);
        goalCompleted = savedGoalCompleted;
        stateRestored = true;

        StatusChanged?.Invoke();
    }
}
