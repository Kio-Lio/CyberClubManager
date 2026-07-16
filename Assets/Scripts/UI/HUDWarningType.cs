public enum HUDWarningType
{
    QueueFull,
    CriticalEquipment,
    LowCleanliness,
    EmptyInventory,
    InternetOutage,
    BankruptcyRisk
}

public readonly struct HUDWarningData
{
    public HUDWarningType Type { get; }
    public string Message { get; }
    public int Priority { get; }

    public HUDWarningData(
        HUDWarningType type,
        string message,
        int priority)
    {
        Type = type;
        Message = message;
        Priority = priority;
    }
}
