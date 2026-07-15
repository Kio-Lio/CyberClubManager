using System;

[Serializable]
public sealed class ClubRandomEventState
{
    public ClubRandomEventType eventType;
    public int remainingDays;
    public float remainingSeconds;

    public ClubRandomEventState Clone()
    {
        return (ClubRandomEventState)MemberwiseClone();
    }
}
