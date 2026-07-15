using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PCMaintenanceTerminal : MonoBehaviour, IInteractable
{
    private void Awake()
    {
        YSortRenderer.Ensure(gameObject, 12, -0.45f);
    }

    public void Interact()
    {
        if (PCMaintenancePanel.Instance == null)
        {
            Debug.LogWarning("PCMaintenancePanel не найден.");
            return;
        }

        PCMaintenancePanel.Instance.Open();
    }

    public string GetInteractionPrompt()
    {
        return "E - Открыть терминал обслуживания ПК";
    }
}
