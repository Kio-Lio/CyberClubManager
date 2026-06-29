using UnityEngine;

public enum PCState
{
    Free,
    Occupied,
    Broken
}

public class PC : MonoBehaviour, IInteractable
{
    [SerializeField] private PCState state = PCState.Free;

    public PCState State => state;

    public void SetState(PCState newState)
    {
        state = newState;
    }

    public void Interact()
    {
        switch (state)
        {
            case PCState.Free:
                Debug.Log("ПК свободен. Можно посадить клиента.");
                break;
            case PCState.Occupied:
                Debug.Log("ПК занят");
                break;
            case PCState.Broken:
                Debug.Log("ПК сломан, требуется ремонт");
                break;
            default:
                Debug.LogWarning($"Неизвестное состояние ПК: {state}");
                break;
        }
    }
}
