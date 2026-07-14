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
}

[Serializable]
public class PCSaveData
{
    public string objectName;
    public int tier;
}
