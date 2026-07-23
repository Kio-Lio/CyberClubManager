using UnityEngine;

public sealed class AdministratorDeskInteraction :
    MonoBehaviour,
    IInteractable
{
    public void Interact()
    {
        // The desk is selectable, but does not own a gameplay action yet.
    }

    public string GetInteractionPrompt()
    {
        return "Стойка администратора";
    }
}
