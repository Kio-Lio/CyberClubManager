using System;

[Serializable]
public sealed class GameSaveSummary
{
    public bool isValid;

    public int day;
    public int balance;
    public int clubLevel;
    public int reputation;

    public DateTime savedAt;
}
