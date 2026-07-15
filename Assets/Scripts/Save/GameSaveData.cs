using System;
using System.Collections.Generic;

[Serializable]
public class GameSaveData
{
    public int version = 1;

    public int money;
    public int totalIncome;
    public int totalExpenses;

    public int reputation;
    public int servedClients;
    public int lostClients;
    public int excellentClients;
    public int normalClients;
    public int poorClients;

    public int currentDay;
    public float timeRemaining;
    public int incomeAtDayStart;
    public int expensesAtDayStart;

    public int activeGoalDay;
    public int dailyGoalType;
    public int dailyGoalTarget;
    public int dailyGoalReward;
    public int dailyGoalServedBaseline;
    public int dailyGoalIncomeBaseline;
    public bool dailyGoalCompleted;

    public int clubLevel;
    public int clubExperience;

    public int consecutiveDebtDays;

    public int purchasedPCCount;
    public List<PCSaveData> pcs = new();
    public RoomDoorSaveData[] roomDoors;
    public PCEquipmentSaveData[] pcEquipment;
    public bool technicianHired;
    public bool cleanerHired;
    public TrashSaveData[] trashItems;
    public int basicPricePercent;
    public int gamingPricePercent;
    public int premiumPricePercent;
    public int energyDrinkStock;
    public int snackStock;
    public int consumableItemsSold;
    public int consumableRevenue;
    public int missedConsumableSales;
    public DailyFinancialReportData currentFinancialReport;
    public DailyFinancialReportData lastFinancialReport;
}

[Serializable]
public class PCSaveData
{
    public string objectName;
    public int tier;
}

[Serializable]
public sealed class RoomDoorSaveData
{
    public string doorId;
    public bool isUnlocked;
}

[Serializable]
public sealed class PCEquipmentSaveData
{
    public string pcName;
    public float keyboardCondition;
    public float mouseCondition;
    public float chairCondition;
}
