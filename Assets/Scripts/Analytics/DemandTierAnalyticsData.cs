using System;
using UnityEngine;

[Serializable]
public sealed class DemandTierAnalyticsData
{
    public PCTier tier;
    public float accessiblePCSeconds;
    public float occupiedPCSeconds;
    public int completedSessions;
    public int sessionRevenue;
    public int priceLostClients;
    public int estimatedPriceLostRevenue;
    public int capacityLostClients;

    public float UtilizationPercent => accessiblePCSeconds <= 0f
        ? 0f
        : Mathf.Clamp(
            occupiedPCSeconds / accessiblePCSeconds * 100f,
            0f,
            100f
        );

    public int AverageSessionRevenue => completedSessions > 0
        ? Mathf.RoundToInt(sessionRevenue / (float)completedSessions)
        : 0;

    public DemandTierAnalyticsData Clone()
    {
        return (DemandTierAnalyticsData)MemberwiseClone();
    }

    public void Reset(PCTier newTier)
    {
        tier = newTier;
        accessiblePCSeconds = 0f;
        occupiedPCSeconds = 0f;
        completedSessions = 0;
        sessionRevenue = 0;
        priceLostClients = 0;
        estimatedPriceLostRevenue = 0;
        capacityLostClients = 0;
    }
}
