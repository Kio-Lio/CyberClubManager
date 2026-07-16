using System;
using UnityEngine;

public sealed class GameDayManager : MonoBehaviour
{
    public static GameDayManager Instance { get; private set; }

    [Header("Day Settings")]
    [SerializeField] private float dayDuration = 120f;

    [Header("Operating Expenses")]
    [SerializeField] private int fixedDailyCost = 200;

    private int currentDay = 1;
    private float timeRemaining;
    private int incomeAtDayStart;
    private int expensesAtDayStart;
    private bool stateRestored;

    public int CurrentDay => currentDay;
    public float TimeRemaining => timeRemaining;
    public float DayDuration => dayDuration;
    public int IncomeAtDayStart => incomeAtDayStart;
    public int ExpensesAtDayStart => expensesAtDayStart;

    public event Action<int, int, int, int> DayEnded;

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
        if (stateRestored)
        {
            return;
        }

        timeRemaining = dayDuration;
        SaveEconomySnapshot();
    }

    public void RestoreState(
        int savedCurrentDay,
        float savedTimeRemaining,
        int savedIncomeAtDayStart,
        int savedExpensesAtDayStart)
    {
        currentDay = Mathf.Max(1, savedCurrentDay);
        timeRemaining = Mathf.Clamp(savedTimeRemaining, 0.1f, dayDuration);
        incomeAtDayStart = Mathf.Max(0, savedIncomeAtDayStart);
        expensesAtDayStart = Mathf.Max(0, savedExpensesAtDayStart);
        stateRestored = true;
    }

    private void Update()
    {
        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            CompleteDay();
        }
    }

    private void CompleteDay()
    {
        int completedDay = currentDay;
        CalculateOperatingExpenseBreakdown(
            out int fixedOperatingCost,
            out int electricityCost,
            out int staffCost
        );
        int internetCost = InternetProviderManager.Instance != null
            ? InternetProviderManager.Instance.GetDailyCost()
            : 0;
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ApplyMandatoryExpense(
                fixedOperatingCost,
                EconomyTransactionCategory.FixedOperatingCost
            );
            EconomyManager.Instance.ApplyMandatoryExpense(
                electricityCost,
                EconomyTransactionCategory.Electricity
            );
            EconomyManager.Instance.ApplyMandatoryExpense(
                staffCost,
                EconomyTransactionCategory.StaffSalary
            );
            if (internetCost > 0)
            {
                EconomyManager.Instance.ApplyMandatoryExpense(
                    internetCost,
                    EconomyTransactionCategory.InternetSubscription
                );
            }
        }
        else
        {
            Debug.LogWarning("EconomyManager is missing. Daily expenses were not applied.");
        }

        int income = 0;
        int expenses = 0;

        if (EconomyManager.Instance != null)
        {
            income = EconomyManager.Instance.TotalIncome - incomeAtDayStart;
            expenses = EconomyManager.Instance.TotalExpenses - expensesAtDayStart;
        }

        int profit = income - expenses;

        Debug.Log(
            $"Day {completedDay} completed. Income: {income}. " +
            $"Expenses: {expenses}. Result: {profit}."
        );

        currentDay++;
        timeRemaining = dayDuration;

        DailyFinancialReportManager.Instance?.FinalizeDay(completedDay);
        DemandAnalyticsManager.Instance?.FinalizeDay(completedDay);

        DayEnded?.Invoke(completedDay, income, expenses, profit);
        SaveEconomySnapshot();
    }

    private int CalculateOperatingExpenses()
    {
        CalculateOperatingExpenseBreakdown(
            out int fixedOperatingCost,
            out int electricityCost,
            out int staffCost
        );
        int internetCost = InternetProviderManager.Instance != null
            ? InternetProviderManager.Instance.GetDailyCost()
            : 0;
        return fixedOperatingCost + electricityCost + staffCost + internetCost;
    }

    private void CalculateOperatingExpenseBreakdown(
        out int fixedOperatingCost,
        out int electricityExpenses,
        out int staffCost)
    {
        PC[] pcs = FindObjectsByType<PC>();
        electricityExpenses = 0;

        foreach (PC pc in pcs)
        {
            if (pc != null)
            {
                electricityExpenses += pc.DailyElectricityCost;
            }
        }

        float electricityMultiplier = ClubRandomEventManager.Instance != null
            ? ClubRandomEventManager.Instance.GetElectricityCostMultiplier()
            : 1f;
        electricityExpenses = Mathf.RoundToInt(
            electricityExpenses * electricityMultiplier
        );

        int technicianCost = TechnicianManager.Instance != null
            ? TechnicianManager.Instance.GetDailyOperatingCost()
            : 0;
        int cleanerCost = CleanerManager.Instance != null
            ? CleanerManager.Instance.GetDailyOperatingCost()
            : 0;
        staffCost = technicianCost + cleanerCost;
        fixedOperatingCost = fixedDailyCost;
    }

    private void SaveEconomySnapshot()
    {
        if (EconomyManager.Instance == null)
        {
            incomeAtDayStart = 0;
            expensesAtDayStart = 0;
            return;
        }

        incomeAtDayStart = EconomyManager.Instance.TotalIncome;
        expensesAtDayStart = EconomyManager.Instance.TotalExpenses;
    }

    private void OnValidate()
    {
        dayDuration = Mathf.Max(1f, dayDuration);
        fixedDailyCost = Mathf.Max(0, fixedDailyCost);
    }
}
