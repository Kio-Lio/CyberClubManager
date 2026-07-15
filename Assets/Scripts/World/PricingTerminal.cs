using UnityEngine;

public sealed class PricingTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        PricingPanel.Instance?.Open();
    }

    public string GetInteractionPrompt()
    {
        return "E - Pricing management";
    }
}
