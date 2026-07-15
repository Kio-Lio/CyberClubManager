using UnityEngine;

public readonly struct ClientFeedbackData
{
    public ClientType ClientType { get; }
    public ClientSatisfaction Satisfaction { get; }

    public bool WasServed { get; }
    public int ReputationChange { get; }
    public float WaitingTime { get; }
    public float EquipmentCondition { get; }

    public string Message { get; }

    public ClientFeedbackData(
        ClientType clientType,
        ClientSatisfaction satisfaction,
        bool wasServed,
        int reputationChange,
        float waitingTime,
        float equipmentCondition,
        string message)
    {
        ClientType = clientType;
        Satisfaction = satisfaction;
        WasServed = wasServed;
        ReputationChange = reputationChange;
        WaitingTime = waitingTime;
        EquipmentCondition = Mathf.Clamp(equipmentCondition, 0f, 100f);
        Message = message;
    }
}
