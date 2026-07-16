using UnityEngine;

public sealed class ClubResearchTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        ClubResearchPanel.Instance?.Open();
    }

    public string GetInteractionPrompt()
    {
        return "Открыть исследования клуба";
    }
}
