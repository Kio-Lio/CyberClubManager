using System;

[Serializable]
public sealed class DailyFinancialReportData
{
    public int day;
    public int sessionRevenue;
    public int consumableRevenue;
    public int bonusIncome;
    public int otherIncome;
    public int fixedOperatingExpenses;
    public int electricityExpenses;
    public int staffSalaryExpenses;
    public int pcRepairExpenses;
    public int equipmentRepairExpenses;
    public int consumableRestockExpenses;
    public int marketingExpenses;
    public int randomEventExpenses;
    public int internetSubscriptionExpenses;
    public int pcUpgradeExpenses;
    public int expansionExpenses;
    public int roomUnlockExpenses;
    public int staffHireExpenses;
    public int internetConnectionExpenses;
    public int otherExpenses;

    public int Revenue => sessionRevenue + consumableRevenue + otherIncome;
    public int Bonuses => bonusIncome;
    public int OperatingExpenses =>
        fixedOperatingExpenses + electricityExpenses + staffSalaryExpenses +
        pcRepairExpenses + equipmentRepairExpenses + consumableRestockExpenses +
        marketingExpenses + randomEventExpenses + internetSubscriptionExpenses +
        otherExpenses;
    public int InvestmentExpenses =>
        pcUpgradeExpenses + expansionExpenses + roomUnlockExpenses +
        staffHireExpenses + internetConnectionExpenses;
    public int TotalExpenses => OperatingExpenses + InvestmentExpenses;
    public int NetCashChange => Revenue + Bonuses - TotalExpenses;

    public DailyFinancialReportData Clone()
    {
        return (DailyFinancialReportData)MemberwiseClone();
    }

    public void Clear(int newDay)
    {
        day = newDay;
        sessionRevenue = 0;
        consumableRevenue = 0;
        bonusIncome = 0;
        otherIncome = 0;
        fixedOperatingExpenses = 0;
        electricityExpenses = 0;
        staffSalaryExpenses = 0;
        pcRepairExpenses = 0;
        equipmentRepairExpenses = 0;
        consumableRestockExpenses = 0;
        marketingExpenses = 0;
        randomEventExpenses = 0;
        internetSubscriptionExpenses = 0;
        pcUpgradeExpenses = 0;
        expansionExpenses = 0;
        roomUnlockExpenses = 0;
        staffHireExpenses = 0;
        internetConnectionExpenses = 0;
        otherExpenses = 0;
    }
}
