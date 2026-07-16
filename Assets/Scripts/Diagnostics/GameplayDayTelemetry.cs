#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

[Serializable]
public sealed class GameplayDayTelemetry
{
    public int day;
    public int startBalance;
    public int endBalance;
    public int revenue;
    public int bonuses;
    public int expenses;
    public int netResult;
    public int servedClients;
    public int lostClients;
    public int regularClients;
    public int gamerClients;
    public int vipClients;
    public int excellentSatisfaction;
    public int normalSatisfaction;
    public int poorSatisfaction;
    public int priceLostClients;
    public int capacityLostClients;
    public int queueOverflowClients;
    public float basicUtilization;
    public float gamingUtilization;
    public float premiumUtilization;
    public int sessionRevenue;
    public int consumableRevenue;
    public int missedConsumableSales;
    public float endingCleanliness;
    public int endingTrashCount;
    public int brokenPCCount;
    public int criticalEquipmentPCCount;
    public int staffExpenses;
    public int technicianServices;
    public int cleanerTrashCleaned;
    public int staffPreventedLossEstimate;
    public int clubLevel;
    public int clubXP;
    public int reputation;
    public int purchasedPCCount;
    public int accessiblePCCount;
    public int unlockedRoomCount;
    public int researchLevels;
    public bool technicianHired;
    public bool cleanerHired;
    public string activeInternetPlan;
    public string activeMarketingCampaign;
    public string randomEvent;
    public bool bankruptcyRisk;
}
#endif
