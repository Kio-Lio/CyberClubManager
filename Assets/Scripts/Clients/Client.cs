using System.Collections.Generic;
using UnityEngine;

public enum ClientType
{
    Regular,
    Gamer,
    VIP
}

public enum ClientSatisfaction
{
    Poor,
    Normal,
    Excellent
}

public sealed class Client : MonoBehaviour
{
    private enum ClientState
    {
        Waiting,
        MovingToPC,
        Playing,
        Leaving
    }

    private ClientSpawner spawner;
    private PC targetPc;
    private ClientType clientType;
    private int priceTolerancePercent = 100;

    private float moveSpeed;
    private float patienceRemaining;
    private float initialPatience;
    private float waitingTime;
    private float assignedPCEquipmentCondition = 100f;
    private float assignedClubCleanliness = 100f;

    private readonly List<Vector3> navigationPath = new();
    private int navigationPathIndex;
    private bool hasNavigationPath;

    private Vector3 waitingPosition;
    private Vector3 seatPosition;
    private Vector3 exitPosition;

    private ClientState state;
    private bool outcomeRegistered;
    private ClientSatisfaction satisfaction = ClientSatisfaction.Excellent;

    public ClientType Type => clientType;
    public float WaitingTime => waitingTime;
    public ClientSatisfaction Satisfaction => satisfaction;
    public int PriceTolerancePercent => priceTolerancePercent;

    public void Initialize(
        ClientSpawner ownerSpawner,
        ClientType type,
        float speed,
        float patience,
        Vector3 exit,
        Vector3 initialWaitingPosition,
        int clientPriceTolerancePercent)
    {
        spawner = ownerSpawner;
        clientType = type;
        moveSpeed = speed;
        initialPatience = Mathf.Max(0.1f, patience);
        patienceRemaining = initialPatience;
        waitingTime = 0f;
        exitPosition = exit;
        waitingPosition = initialWaitingPosition;
        priceTolerancePercent = Mathf.Max(1, clientPriceTolerancePercent);
        state = ClientState.Waiting;
    }

    public bool IsTierCompatible(PC pc)
    {
        return pc != null && IsTierCompatible(clientType, pc.Tier);
    }

    public static bool IsTierCompatible(
        ClientType clientType,
        PCTier tier)
    {
        return clientType switch
        {
            ClientType.Regular => true,
            ClientType.Gamer =>
                tier == PCTier.Gaming || tier == PCTier.Premium,
            ClientType.VIP => tier == PCTier.Premium,
            _ => false
        };
    }

    public bool CanAffordPC(PC pc)
    {
        return pc == null || PricingManager.Instance == null ||
            PricingManager.Instance.CanClientAcceptPrice(pc.Tier, priceTolerancePercent);
    }

    public bool CanUsePC(PC pc)
    {
        return pc != null && pc.IsAvailable && IsTierCompatible(pc) && CanAffordPC(pc);
    }

    public string GetTypeDisplayName()
    {
        return clientType switch
        {
            ClientType.Regular => "Обычный",
            ClientType.Gamer => "Геймер",
            ClientType.VIP => "VIP",
            _ => clientType.ToString()
        };
    }

    public string GetSatisfactionDisplayName()
    {
        return satisfaction switch
        {
            ClientSatisfaction.Excellent => "Отлично",
            ClientSatisfaction.Normal => "Нормально",
            ClientSatisfaction.Poor => "Плохо",
            _ => satisfaction.ToString()
        };
    }

    private void Update()
    {
        switch (state)
        {
            case ClientState.Waiting:
                UpdateWaiting();
                break;
            case ClientState.MovingToPC:
                UpdateMovingToPC();
                break;
            case ClientState.Playing:
                UpdatePlaying();
                break;
            case ClientState.Leaving:
                UpdateLeaving();
                break;
        }
    }

    public void SetWaitingPosition(Vector3 newPosition)
    {
        waitingPosition = newPosition;
    }

    public void ResumeWaiting()
    {
        targetPc = null;
        state = ClientState.Waiting;
    }

    public void AssignPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        targetPc = pc;
        assignedPCEquipmentCondition = targetPc.LowestEquipmentCondition;
        assignedClubCleanliness = ClubCleanlinessManager.Instance != null
            ? ClubCleanlinessManager.Instance.Cleanliness
            : 100f;
        seatPosition = targetPc.transform.position + new Vector3(0f, -0.8f, 0f);
        BeginNavigation(seatPosition, targetPc.ApproachNode);
        satisfaction = CalculateSatisfaction();
        satisfaction = ApplyEquipmentSatisfactionPenalty(
            satisfaction,
            assignedPCEquipmentCondition
        );
        satisfaction = ApplyCleanlinessSatisfactionPenalty(
            satisfaction,
            assignedClubCleanliness
        );
        state = ClientState.MovingToPC;
        Debug.Log(
            $"{name}: клиент типа {GetTypeDisplayName()} " +
            $"получил ПК класса {pc.GetTierDisplayName()}. " +
            $"Ожидание: {waitingTime:F1} сек. " +
            $"Оценка: {GetSatisfactionDisplayName()}."
        );
    }

    private void UpdateWaiting()
    {
        MoveTowards(waitingPosition);
        waitingTime += Time.deltaTime;
        patienceRemaining -= Time.deltaTime;

        if (patienceRemaining > 0f)
        {
            return;
        }

        spawner?.RemoveFromQueue(this);
        RegisterLostOutcome();
        Debug.Log($"{name}: client left after waiting too long.");
        BeginLeaving();
    }

    private void UpdateMovingToPC()
    {
        if (targetPc == null)
        {
            spawner?.ReturnToQueue(this);
            return;
        }

        if (!UpdateNavigation())
        {
            return;
        }

        if (targetPc.TryOccupyReserved(clientType))
        {
            state = ClientState.Playing;
            Debug.Log($"{name}: client started playing.");
        }
        else
        {
            targetPc.CancelReservation();
            targetPc = null;
            Debug.Log($"{name}: assigned PC became unavailable.");
            spawner?.ReturnToQueue(this);
        }
    }

    private void UpdatePlaying()
    {
        if (targetPc != null && targetPc.IsOccupied)
        {
            return;
        }

        RegisterServedOutcome();
        Debug.Log($"{name}: client completed the visit and is leaving.");
        BeginLeaving();
    }

    private void UpdateLeaving()
    {
        if (UpdateNavigation())
        {
            Destroy(gameObject);
        }
    }

    private void BeginLeaving()
    {
        if (targetPc != null)
        {
            targetPc.CancelReservation();
            targetPc = null;
        }

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance ??
            ClientNavigationManager.EnsureRuntimeGraph();

        BeginNavigation(exitPosition, navigation.ExitNode);
        state = ClientState.Leaving;
    }

    private void BeginNavigation(
        Vector3 destination,
        ClientNavigationNode destinationNode = null)
    {
        navigationPath.Clear();
        navigationPathIndex = 0;
        hasNavigationPath = false;

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance ??
            ClientNavigationManager.EnsureRuntimeGraph();

        if (navigation == null || destinationNode == null)
        {
            navigationPath.Add(destination);
            hasNavigationPath = true;
            return;
        }

        ClientNavigationNode startNode = navigation.FindClosestNode(
            transform.position
        );

        if (startNode != null)
        {
            navigationPath.AddRange(
                navigation.BuildPath(startNode, destinationNode)
            );
        }

        if (navigationPath.Count == 0 ||
            Vector3.Distance(navigationPath[^1], destination) > 0.05f)
        {
            navigationPath.Add(destination);
        }

        hasNavigationPath = navigationPath.Count > 0;
    }

    private bool UpdateNavigation()
    {
        if (!hasNavigationPath ||
            navigationPathIndex >= navigationPath.Count)
        {
            return true;
        }

        Vector3 targetPosition = navigationPath[navigationPathIndex];
        MoveTowards(targetPosition);

        if (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            return false;
        }

        navigationPathIndex++;

        if (navigationPathIndex < navigationPath.Count)
        {
            return false;
        }

        hasNavigationPath = false;
        return true;
    }

    private void MoveTowards(Vector3 destination)
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime
        );
    }

    private ClientSatisfaction CalculateSatisfaction()
    {
        float patienceRatio = Mathf.Clamp01(
            patienceRemaining / initialPatience
        );

        if (patienceRatio >= 0.7f)
        {
            return ClientSatisfaction.Excellent;
        }

        if (patienceRatio >= 0.35f)
        {
            return ClientSatisfaction.Normal;
        }

        return ClientSatisfaction.Poor;
    }

    private static ClientSatisfaction ApplyEquipmentSatisfactionPenalty(
        ClientSatisfaction currentSatisfaction,
        float equipmentCondition)
    {
        int penaltySteps = equipmentCondition <= 20f
            ? 2
            : equipmentCondition <= 50f
                ? 1
                : 0;

        int satisfactionValue = Mathf.Clamp(
            (int)currentSatisfaction - penaltySteps,
            (int)ClientSatisfaction.Poor,
            (int)ClientSatisfaction.Excellent
        );

        return (ClientSatisfaction)satisfactionValue;
    }

    private static ClientSatisfaction ApplyCleanlinessSatisfactionPenalty(
        ClientSatisfaction currentSatisfaction,
        float cleanliness)
    {
        int penaltySteps = cleanliness < 35f
            ? 2
            : cleanliness < 70f
                ? 1
                : 0;

        int satisfactionValue = Mathf.Clamp(
            (int)currentSatisfaction - penaltySteps,
            (int)ClientSatisfaction.Poor,
            (int)ClientSatisfaction.Excellent
        );

        return (ClientSatisfaction)satisfactionValue;
    }

    private void RegisterServedOutcome()
    {
        if (outcomeRegistered)
        {
            return;
        }

        outcomeRegistered = true;

        ConsumableInventoryManager.Instance?.TrySellToClient(clientType);

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.RegisterServedClient(
                clientType,
                satisfaction,
                waitingTime,
                assignedPCEquipmentCondition,
                assignedClubCleanliness
            );
        }
        else
        {
            Debug.LogWarning("ClubReputationManager is missing from the scene.");
        }
    }

    private void RegisterLostOutcome()
    {
        if (outcomeRegistered)
        {
            return;
        }

        outcomeRegistered = true;

        DemandAnalyticsManager.Instance?.RecordClientDeparture(
            clientType,
            priceTolerancePercent
        );

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.RegisterLostClient(
                clientType,
                waitingTime
            );
        }
        else
        {
            Debug.LogWarning("ClubReputationManager is missing from the scene.");
        }
    }
}
