using UnityEngine;

public sealed class ConsumableStockTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        ConsumableStockPanel.Instance?.Open();
    }

    public string GetInteractionPrompt()
    {
        return "E - Open drinks and snacks stock";
    }
}
