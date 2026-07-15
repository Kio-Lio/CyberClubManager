public readonly struct PCSessionAnalyticsData
{
    public string PCName { get; }
    public PCTier Tier { get; }
    public ClientType ClientType { get; }
    public int SessionRevenue { get; }
    public int PricePercent { get; }

    public PCSessionAnalyticsData(
        string pcName,
        PCTier tier,
        ClientType clientType,
        int sessionRevenue,
        int pricePercent)
    {
        PCName = pcName;
        Tier = tier;
        ClientType = clientType;
        SessionRevenue = sessionRevenue;
        PricePercent = pricePercent;
    }
}
