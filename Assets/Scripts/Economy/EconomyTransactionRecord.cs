public readonly struct EconomyTransactionRecord
{
    public int Amount { get; }
    public bool IsIncome { get; }
    public bool CountsAsRevenue { get; }
    public EconomyTransactionCategory Category { get; }

    public EconomyTransactionRecord(
        int amount,
        bool isIncome,
        bool countsAsRevenue,
        EconomyTransactionCategory category)
    {
        Amount = amount;
        IsIncome = isIncome;
        CountsAsRevenue = countsAsRevenue;
        Category = category;
    }
}
