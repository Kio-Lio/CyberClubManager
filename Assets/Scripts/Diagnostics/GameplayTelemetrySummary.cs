#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

[Serializable]
public sealed class GameplayTelemetrySummary
{
    public int firstDay;
    public int lastDay;
    public int daysAnalyzed;
    public int startingBalance;
    public int endingBalance;
    public int totalRevenue;
    public int totalBonuses;
    public int totalExpenses;
    public int totalNetResult;
    public int totalServedClients;
    public int totalLostClients;
    public int finalClubLevel;
    public int finalReputation;
    public int finalPCCount;
    public int finalUnlockedRooms;
    public int finalResearchLevels;
    public int warningCount;
}
#endif
