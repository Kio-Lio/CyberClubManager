using UnityEngine;

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

    private float moveSpeed;
    private float patienceRemaining;

    private Vector3 waitingPosition;
    private Vector3 seatPosition;
    private Vector3 exitPosition;

    private ClientState state;

    public void Initialize(
        ClientSpawner ownerSpawner,
        float speed,
        float patience,
        Vector3 exit,
        Vector3 initialWaitingPosition)
    {
        spawner = ownerSpawner;
        moveSpeed = speed;
        patienceRemaining = patience;
        exitPosition = exit;
        waitingPosition = initialWaitingPosition;
        state = ClientState.Waiting;
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

        Debug.Log($"{name}: клиент получил свободный ПК.");
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
        Debug.Log($"{name}: клиент не дождался свободного ПК и уходит.");
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
            Debug.Log($"{name}: клиент начал играть.");
        }
        else
        {
            targetPc.CancelReservation();
            targetPc = null;
            Debug.Log($"{name}: назначенный ПК оказался недоступен.");
            spawner?.ReturnToQueue(this);
        }
    }

    private void UpdatePlaying()
    {
        if (targetPc != null && targetPc.IsOccupied)
        {
            return;
        }

        Debug.Log($"{name}: клиент завершил посещение и уходит.");
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
}
