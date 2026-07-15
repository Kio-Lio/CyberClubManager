using System;

[Serializable]
public sealed class DemandAnalyticsReportData
{
    public int day;
    public DemandTierAnalyticsData basic = new();
    public DemandTierAnalyticsData gaming = new();
    public DemandTierAnalyticsData premium = new();
    public int queueOverflowClients;

    public int TotalCompletedSessions =>
        basic.completedSessions + gaming.completedSessions + premium.completedSessions;
    public int TotalSessionRevenue =>
        basic.sessionRevenue + gaming.sessionRevenue + premium.sessionRevenue;
    public int TotalPriceLostClients =>
        basic.priceLostClients + gaming.priceLostClients + premium.priceLostClients;
    public int TotalEstimatedLostRevenue =>
        basic.estimatedPriceLostRevenue + gaming.estimatedPriceLostRevenue +
        premium.estimatedPriceLostRevenue;
    public int TotalCapacityLostClients =>
        basic.capacityLostClients + gaming.capacityLostClients +
        premium.capacityLostClients;

    public DemandTierAnalyticsData GetTierData(PCTier tier)
    {
        return tier switch
        {
            PCTier.Basic => basic,
            PCTier.Gaming => gaming,
            PCTier.Premium => premium,
            _ => basic
        };
    }

    public void Reset(int newDay)
    {
        day = newDay;
        basic ??= new DemandTierAnalyticsData();
        gaming ??= new DemandTierAnalyticsData();
        premium ??= new DemandTierAnalyticsData();
        basic.Reset(PCTier.Basic);
        gaming.Reset(PCTier.Gaming);
        premium.Reset(PCTier.Premium);
        queueOverflowClients = 0;
    }

    public DemandAnalyticsReportData Clone()
    {
        return new DemandAnalyticsReportData
        {
            day = day,
            basic = basic?.Clone() ?? new DemandTierAnalyticsData(),
            gaming = gaming?.Clone() ?? new DemandTierAnalyticsData(),
            premium = premium?.Clone() ?? new DemandTierAnalyticsData(),
            queueOverflowClients = queueOverflowClients
        };
    }
}
