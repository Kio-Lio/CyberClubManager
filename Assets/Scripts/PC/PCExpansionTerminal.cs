using UnityEngine;

public sealed class PCExpansionTerminal : MonoBehaviour, IInteractable
{
    public string GetInteractionPrompt()
    {
        PCExpansionManager manager = PCExpansionManager.Instance;

        if (manager == null)
        {
            return "Терминал недоступен";
        }

        if (BankruptcyManager.Instance != null &&
            BankruptcyManager.Instance.IsGameOver)
        {
            return string.Empty;
        }

        if (!manager.HasAvailableSlot)
        {
            return "Все места для расширения куплены";
        }

        int cost = manager.PurchaseCost;

        if (EconomyManager.Instance != null &&
            EconomyManager.Instance.Money < cost)
        {
            return $"Новый ПК: {cost} ₽ — недостаточно денег";
        }

        return $"E — Купить новый ПК за {cost} ₽";
    }

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
