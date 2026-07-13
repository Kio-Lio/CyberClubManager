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

    public int CurrentDay => currentDay;
    public float TimeRemaining => timeRemaining;
    public float DayDuration => dayDuration;

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
        timeRemaining = dayDuration;
        SaveEconomySnapshot();
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
        int operatingExpenses = CalculateOperatingExpenses();

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ApplyMandatoryExpense(operatingExpenses);
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

        DayEnded?.Invoke(completedDay, income, expenses, profit);
        SaveEconomySnapshot();
    }

    private int CalculateOperatingExpenses()
    {
        PC[] pcs = FindObjectsByType<PC>();
        int electricityExpenses = 0;

        foreach (PC pc in pcs)
        {
            if (pc != null)
            {
                electricityExpenses += pc.DailyElectricityCost;
            }
        }

        return fixedDailyCost + electricityExpenses;
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
