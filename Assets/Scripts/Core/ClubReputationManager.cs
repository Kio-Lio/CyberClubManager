using System;
using UnityEngine;

public sealed class ClubReputationManager : MonoBehaviour
{
    public static ClubReputationManager Instance { get; private set; }

    [SerializeField, Range(0, 100)] private int reputation = 50;

    private int servedClients;
    private int lostClients;
    private int excellentClients;
    private int normalClients;
    private int poorClients;

    public int Reputation => reputation;
    public int ServedClients => servedClients;
    public int LostClients => lostClients;
    public int ExcellentClients => excellentClients;
    public int NormalClients => normalClients;
    public int PoorClients => poorClients;
    public float NormalizedReputation => reputation / 100f;

    public event Action StatusChanged;
    public event Action ClientServed;
    public event Action<ClientFeedbackData> ClientFeedbackCreated;

    public void RestoreState(
        int savedReputation,
        int savedServedClients,
        int savedLostClients)
    {
        RestoreState(
            savedReputation,
            savedServedClients,
            savedLostClients,
            0,
            0,
            0
        );
    }

    public void RestoreState(
        int savedReputation,
        int savedServedClients,
        int savedLostClients,
        int savedExcellentClients,
        int savedNormalClients,
        int savedPoorClients)
    {
        reputation = Mathf.Clamp(savedReputation, 0, 100);
        servedClients = Mathf.Max(0, savedServedClients);
        lostClients = Mathf.Max(0, savedLostClients);
        excellentClients = Mathf.Max(0, savedExcellentClients);
        normalClients = Mathf.Max(0, savedNormalClients);
        poorClients = Mathf.Max(0, savedPoorClients);
        StatusChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterServedClient()
    {
        RegisterServedClient(
            ClientType.Regular,
            ClientSatisfaction.Normal,
            0f
        );
    }

    public void RegisterServedClient(ClientType clientType)
    {
        RegisterServedClient(
            clientType,
            ClientSatisfaction.Normal,
            0f
        );
    }

    public void RegisterServedClient(
        ClientType clientType,
        ClientSatisfaction satisfaction)
    {
        RegisterServedClient(clientType, satisfaction, 0f);
    }

    public void RegisterServedClient(
        ClientType clientType,
        ClientSatisfaction satisfaction,
        float waitingTime)
    {
        RegisterServedClient(
            clientType,
            satisfaction,
            waitingTime,
            100f
        );
    }

    public void RegisterServedClient(
        ClientType clientType,
        ClientSatisfaction satisfaction,
        float waitingTime,
        float equipmentCondition)
    {
        int typeReward = GetServedReputationReward(clientType);
        int satisfactionModifier = GetSatisfactionReputationModifier(
            satisfaction
        );
        int totalReputationChange = typeReward + satisfactionModifier;
        servedClients++;
        reputation = Mathf.Clamp(
            reputation + totalReputationChange,
            0,
            100
        );

        switch (satisfaction)
        {
            case ClientSatisfaction.Excellent:
                excellentClients++;
                break;
            case ClientSatisfaction.Normal:
                normalClients++;
                break;
            case ClientSatisfaction.Poor:
                poorClients++;
                break;
        }

        string changePrefix = totalReputationChange >= 0 ? "+" : string.Empty;

        Debug.Log(
            $"Обслужен клиент типа {GetClientTypeDisplayName(clientType)}. " +
            $"Оценка: {GetSatisfactionDisplayName(satisfaction)}. " +
            $"Репутация {changePrefix}{totalReputationChange}. " +
            $"Текущая репутация: {reputation}/100. " +
            $"Всего обслужено: {servedClients}."
        );

        StatusChanged?.Invoke();
        ClientServed?.Invoke();
        ClientFeedbackCreated?.Invoke(
            new ClientFeedbackData(
                clientType,
                satisfaction,
                true,
                totalReputationChange,
                Mathf.Max(0f, waitingTime),
                Mathf.Clamp(equipmentCondition, 0f, 100f),
                GetServedFeedbackMessage(satisfaction, equipmentCondition)
            )
        );
    }

    public void RegisterLostClient()
    {
        RegisterLostClient(ClientType.Regular, 0f);
    }

    public void RegisterLostClient(
        ClientType clientType,
        float waitingTime = 0f)
    {
        int reputationPenalty = GetLostReputationPenalty(clientType);
        lostClients++;
        reputation = Mathf.Clamp(reputation - reputationPenalty, 0, 100);

        Debug.Log(
            $"Потерян клиент типа {GetClientTypeDisplayName(clientType)}. " +
            $"Репутация -{reputationPenalty}. " +
            $"Текущая репутация: {reputation}/100. " +
            $"Всего потеряно: {lostClients}."
        );

        StatusChanged?.Invoke();
        ClientFeedbackCreated?.Invoke(
            new ClientFeedbackData(
                clientType,
                ClientSatisfaction.Poor,
                false,
                -reputationPenalty,
                Mathf.Max(0f, waitingTime),
                100f,
                "Не дождался подходящего компьютера и ушел."
            )
        );
    }

    private static int GetServedReputationReward(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => 1,
            ClientType.Gamer => 2,
            ClientType.VIP => 4,
            _ => 1
        };
    }

    private static int GetLostReputationPenalty(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => 5,
            ClientType.Gamer => 7,
            ClientType.VIP => 10,
            _ => 5
        };
    }

    private static int GetSatisfactionReputationModifier(
        ClientSatisfaction satisfaction)
    {
        return satisfaction switch
        {
            ClientSatisfaction.Excellent => 2,
            ClientSatisfaction.Normal => 0,
            ClientSatisfaction.Poor => -2,
            _ => 0
        };
    }

    private static string GetServedFeedbackMessage(
        ClientSatisfaction satisfaction,
        float equipmentCondition)
    {
        if (equipmentCondition <= 20f)
        {
            return "Оборудование почти сломано, играть неудобно.";
        }

        if (equipmentCondition <= 50f)
        {
            return "Периферия уже заметно изношена.";
        }

        return satisfaction switch
        {
            ClientSatisfaction.Excellent =>
                "Все отлично, быстро нашли место!",
            ClientSatisfaction.Normal =>
                "Нормально, но пришлось немного подождать.",
            ClientSatisfaction.Poor =>
                "Слишком долго ждал свободный компьютер.",
            _ => "Посещение завершено."
        };
    }

    private static string GetClientTypeDisplayName(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => "Обычный",
            ClientType.Gamer => "Геймер",
            ClientType.VIP => "VIP",
            _ => clientType.ToString()
        };
    }

    private static string GetSatisfactionDisplayName(
        ClientSatisfaction satisfaction)
    {
        return satisfaction switch
        {
            ClientSatisfaction.Excellent => "Отлично",
            ClientSatisfaction.Normal => "Нормально",
            ClientSatisfaction.Poor => "Плохо",
            _ => satisfaction.ToString()
        };
    }
}
