using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PCExpansionTerminal : MonoBehaviour, IInteractable
{
    private void Awake()
    {
        ConfigureYSorting();
    }

    public void ConfigureYSorting()
    {
        YSortRenderer.Ensure(gameObject, 12, -0.45f);
    }

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

        if (manager.PurchasedPCCount >= manager.UnlockedSlotCount &&
            manager.PurchasedPCCount < manager.TotalExpansionSlots)
        {
            int requiredLevel = Mathf.Min(
                4,
                manager.PurchasedPCCount + 1
            );

            return
                $"Следующее место откроется на уровне клуба {requiredLevel}";
        }

        if (manager.PurchasedPCCount >= manager.TotalExpansionSlots)
        {
            return "Все места для расширения куплены";
        }

        int cost = manager.PurchaseCost;

        if (EconomyManager.Instance != null &&
            EconomyManager.Instance.Money < cost)
        {
            return $"Новый ПК: {cost} ₽ — недостаточно денег";
        }

        if (ManagerBuildController.Instance != null)
        {
            return $"Разместить новый ПК за {cost} ₽";
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

        if (ManagerBuildController.Instance != null)
        {
            ManagerBuildController.Instance.BeginPCPlacement();
            return;
        }

        PCExpansionManager.Instance.TryPurchaseNextPC();
    }
}
