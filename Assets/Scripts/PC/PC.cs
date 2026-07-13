using System;
using System.Collections;
using UnityEngine;

public enum PCState
{
    Free,
    Occupied,
    Broken
}

[RequireComponent(typeof(SpriteRenderer))]
public class PC : MonoBehaviour, IInteractable
{
    public static event Action<PC> PCRegistered;
    public static event Action<PC> PCUnregistered;

    [Header("Session Settings")]
    [SerializeField] private PCState state = PCState.Free;
    [SerializeField] private int sessionPrice = 100;
    [SerializeField] private float sessionDuration = 10f;

    [Header("Breakdown Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float breakdownChance = 0.25f;
    [SerializeField] private int repairCost = 50;

    private bool isReserved;

    [Header("Visual Settings")]
    [SerializeField] private Color freeColor = Color.white;
    [SerializeField] private Color occupiedColor = Color.yellow;
    [SerializeField] private Color brokenColor = Color.red;

    private SpriteRenderer spriteRenderer;
    private Coroutine sessionCoroutine;

    public PCState State => state;
    public bool IsFree => state == PCState.Free;
    public bool IsOccupied => state == PCState.Occupied;
    public bool IsBroken => state == PCState.Broken;
    public bool IsAvailable => IsFree && !isReserved;
    public event Action<PCState> StateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        PCRegistered = null;
        PCUnregistered = null;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    private void OnEnable()
    {
        PCRegistered?.Invoke(this);
    }

    private void OnDisable()
    {
        PCUnregistered?.Invoke(this);
    }

    private void OnValidate()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    public void Interact()
    {
        switch (state)
        {
            case PCState.Free:
                Debug.Log($"{name}: ПК свободен и ожидает клиента.");
                break;
            case PCState.Occupied:
                Debug.Log($"{name}: ПК занят. Клиент уже играет.");
                break;
            case PCState.Broken:
                TryRepair();
                break;
        }
    }

    public void SetState(PCState newState)
    {
        if (state == newState)
        {
            return;
        }

        state = newState;

        if (state != PCState.Free)
        {
            isReserved = false;
        }

        UpdateVisual();
        StateChanged?.Invoke(state);
    }

    public bool TryOccupy()
    {
        if (!IsAvailable)
        {
            return false;
        }

        Occupy();
        return true;
    }

    public bool TryReserve()
    {
        if (!IsAvailable)
        {
            return false;
        }

        isReserved = true;
        return true;
    }

    public void CancelReservation()
    {
        if (IsFree)
        {
            isReserved = false;
        }
    }

    public bool TryOccupyReserved()
    {
        if (!IsFree || !isReserved)
        {
            return false;
        }

        isReserved = false;
        Occupy();
        return true;
    }

    private void Occupy()
    {
        SetState(PCState.Occupied);
        Debug.Log("Клиент посажен за ПК. ПК теперь занят.");

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(sessionPrice);
        }
        else
        {
            Debug.LogWarning("EconomyManager не найден в сцене.");
        }

        if (sessionCoroutine != null)
        {
            StopCoroutine(sessionCoroutine);
        }

        sessionCoroutine = StartCoroutine(SessionTimer());
    }

    private IEnumerator SessionTimer()
    {
        Debug.Log($"Игровая сессия началась. Длительность: {sessionDuration} секунд.");
        yield return new WaitForSeconds(sessionDuration);
        CompleteSession();
    }

    private void CompleteSession()
    {
        if (!IsOccupied)
        {
            return;
        }

        sessionCoroutine = null;

        if (UnityEngine.Random.value < breakdownChance)
        {
            SetState(PCState.Broken);
            Debug.Log($"{name}: игровая сессия завершена, но ПК сломался.");
        }
        else
        {
            SetState(PCState.Free);
            Debug.Log($"{name}: игровая сессия завершена. ПК снова свободен.");
        }
    }

    private void TryRepair()
    {
        if (!IsBroken)
        {
            return;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager не найден. Ремонт невозможен.");
            return;
        }

        if (!EconomyManager.Instance.SpendMoney(repairCost))
        {
            Debug.Log($"{name}: недостаточно денег для ремонта. Нужно {repairCost}.");
            return;
        }

        SetState(PCState.Free);
        Debug.Log($"{name}: ПК отремонтирован и снова доступен.");
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        switch (state)
        {
            case PCState.Free:
                spriteRenderer.color = freeColor;
                break;
            case PCState.Occupied:
                spriteRenderer.color = occupiedColor;
                break;
            case PCState.Broken:
                spriteRenderer.color = brokenColor;
                break;
        }
    }
}
