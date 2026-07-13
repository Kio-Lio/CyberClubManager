using UnityEngine;

public sealed class PCExpansionTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (PCExpansionManager.Instance == null)
        {
            Debug.LogWarning("PCExpansionManager не найден в сцене.");
            return;
        }

        PCExpansionManager.Instance.TryPurchaseNextPC();
    }
}
