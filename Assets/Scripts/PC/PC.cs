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
    [SerializeField] private PCState state = PCState.Free;
    [SerializeField] private int sessionPrice = 100;
    [SerializeField] private float sessionDuration = 10f;

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

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
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
                TryOccupy();
                break;
            case PCState.Occupied:
                Debug.Log("ПК занят. Клиент уже играет.");
                break;
            case PCState.Broken:
                Debug.Log("ПК сломан. Нужно починить.");
                break;
            default:
                Debug.LogWarning($"Неизвестное состояние ПК: {state}");
                break;
        }
    }

    public void SetState(PCState newState)
    {
        state = newState;
        UpdateVisual();
    }

    public bool TryOccupy()
    {
        if (!IsFree)
        {
            return false;
        }

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

        SetState(PCState.Free);
        sessionCoroutine = null;
        Debug.Log("Клиент завершил игровую сессию. ПК снова свободен.");
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
