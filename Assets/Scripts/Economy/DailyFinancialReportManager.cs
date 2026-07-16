using System;
using UnityEngine;

public sealed class DailyFinancialReportManager : MonoBehaviour
{
    public static DailyFinancialReportManager Instance { get; private set; }

    private DailyFinancialReportData currentReport = new();
    private DailyFinancialReportData lastReport;

    public DailyFinancialReportData CurrentReport => currentReport;
    public DailyFinancialReportData LastReport => lastReport;
    public bool HasLastReport => lastReport != null && lastReport.day > 0;

    public event Action StatusChanged;
    public event Action<DailyFinancialReportData> ReportCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentReport.Clear(1);
    }

    private void Start()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.TransactionRecorded += OnTransactionRecorded;
        }

        int currentDay = GameDayManager.Instance != null
            ? GameDayManager.Instance.CurrentDay
            : 1;
        if (currentReport.day <= 0)
        {
            currentReport.Clear(currentDay);
        }
    }

    private void OnDestroy()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.TransactionRecorded -= OnTransactionRecorded;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void FinalizeDay(int completedDay)
    {
        currentReport.day = completedDay;
        lastReport = currentReport.Clone();
        currentReport = new DailyFinancialReportData();
        currentReport.Clear(completedDay + 1);
        StatusChanged?.Invoke();
        ReportCompleted?.Invoke(lastReport.Clone());
    }

    public DailyFinancialReportData CreateCurrentSaveData() => currentReport.Clone();
    public DailyFinancialReportData CreateLastSaveData() => lastReport?.Clone();

    public void RestoreState(
        DailyFinancialReportData savedCurrentReport,
        DailyFinancialReportData savedLastReport,
        int currentDay)
    {
        currentReport = savedCurrentReport?.Clone() ?? new DailyFinancialReportData();
        if (currentReport.day <= 0)
        {
            currentReport.Clear(currentDay);
        }

        lastReport = savedLastReport?.Clone();
        StatusChanged?.Invoke();
    }

    private void OnTransactionRecorded(EconomyTransactionRecord transaction)
    {
        int amount = transaction.Amount;
        switch (transaction.Category)
        {
            case EconomyTransactionCategory.SessionRevenue: currentReport.sessionRevenue += amount; break;
            case EconomyTransactionCategory.ConsumableRevenue: currentReport.consumableRevenue += amount; break;
            case EconomyTransactionCategory.DailyGoalReward: currentReport.bonusIncome += amount; break;
            case EconomyTransactionCategory.OtherIncome: currentReport.otherIncome += amount; break;
            case EconomyTransactionCategory.FixedOperatingCost: currentReport.fixedOperatingExpenses += amount; break;
            case EconomyTransactionCategory.Electricity: currentReport.electricityExpenses += amount; break;
            case EconomyTransactionCategory.StaffSalary: currentReport.staffSalaryExpenses += amount; break;
            case EconomyTransactionCategory.PCRepair: currentReport.pcRepairExpenses += amount; break;
            case EconomyTransactionCategory.EquipmentRepair: currentReport.equipmentRepairExpenses += amount; break;
            case EconomyTransactionCategory.ConsumableRestock: currentReport.consumableRestockExpenses += amount; break;
            case EconomyTransactionCategory.MarketingExpense: currentReport.marketingExpenses += amount; break;
            case EconomyTransactionCategory.RandomEventExpense: currentReport.randomEventExpenses += amount; break;
            case EconomyTransactionCategory.InternetSubscription: currentReport.internetSubscriptionExpenses += amount; break;
            case EconomyTransactionCategory.PCUpgrade: currentReport.pcUpgradeExpenses += amount; break;
            case EconomyTransactionCategory.ExpansionPurchase: currentReport.expansionExpenses += amount; break;
            case EconomyTransactionCategory.RoomUnlock: currentReport.roomUnlockExpenses += amount; break;
            case EconomyTransactionCategory.StaffHire: currentReport.staffHireExpenses += amount; break;
            case EconomyTransactionCategory.InternetConnection: currentReport.internetConnectionExpenses += amount; break;
            default: currentReport.otherExpenses += amount; break;
        }

        StatusChanged?.Invoke();
    }
}
