using UnityEngine;

public enum ClientType
{
    Regular,
    Gamer,
    VIP
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

    private float moveSpeed;
    private float patienceRemaining;

    private Vector3 waitingPosition;
    private Vector3 seatPosition;
    private Vector3 exitPosition;

    private ClientState state;
    private bool outcomeRegistered;

    public ClientType Type => clientType;

    public void Initialize(
        ClientSpawner ownerSpawner,
        ClientType type,
        float speed,
        float patience,
        Vector3 exit,
        Vector3 initialWaitingPosition)
    {
        spawner = ownerSpawner;
        clientType = type;
        moveSpeed = speed;
        patienceRemaining = patience;
        exitPosition = exit;
        waitingPosition = initialWaitingPosition;
        state = ClientState.Waiting;
    }

    public bool CanUsePC(PC pc)
    {
        if (pc == null || !pc.IsAvailable)
        {
            return false;
        }

        return clientType switch
        {
            ClientType.Regular => true,
            ClientType.Gamer =>
                pc.Tier == PCTier.Gaming ||
                pc.Tier == PCTier.Premium,
            ClientType.VIP => pc.Tier == PCTier.Premium,
            _ => false
        };
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
        seatPosition = targetPc.transform.position + new Vector3(0f, -0.8f, 0f);
        state = ClientState.MovingToPC;
        Debug.Log(
            $"{name}: клиент типа {GetTypeDisplayName()} " +
            $"получил ПК класса {pc.GetTierDisplayName()}."
        );
    }

    private void UpdateWaiting()
    {
        MoveTowards(waitingPosition);
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

        MoveTowards(seatPosition);

        if (Vector3.Distance(transform.position, seatPosition) > 0.05f)
        {
            return;
        }

        if (targetPc.TryOccupyReserved())
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
        MoveTowards(exitPosition);

        if (Vector3.Distance(transform.position, exitPosition) <= 0.05f)
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

        state = ClientState.Leaving;
    }

    private void MoveTowards(Vector3 destination)
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime
        );
    }

    private void RegisterServedOutcome()
    {
        if (outcomeRegistered)
        {
            return;
        }

        outcomeRegistered = true;

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.RegisterServedClient();
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

        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.RegisterLostClient();
        }
        else
        {
            Debug.LogWarning("ClubReputationManager is missing from the scene.");
        }
    }
}
