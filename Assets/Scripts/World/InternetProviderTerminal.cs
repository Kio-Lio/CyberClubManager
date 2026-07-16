using UnityEngine;

public sealed class InternetProviderTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        InternetProviderPanel.Instance?.Open();
    }

    public string GetInteractionPrompt()
    {
        return "Открыть управление интернетом";
    }
}
