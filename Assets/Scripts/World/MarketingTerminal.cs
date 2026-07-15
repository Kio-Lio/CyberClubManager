using UnityEngine;

public sealed class MarketingTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        MarketingPanel.Instance?.Open();
    }

    public string GetInteractionPrompt()
    {
        return "E - Open marketing management";
    }
}
