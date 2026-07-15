using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class TrashItem : MonoBehaviour, IInteractable
{
    private ClubCleanlinessManager owner;

    public string TrashId { get; private set; }
    public string SourcePCName { get; private set; }

    public void Initialize(
        ClubCleanlinessManager manager,
        string trashId,
        string sourcePCName)
    {
        owner = manager;
        TrashId = trashId;
        SourcePCName = sourcePCName;
    }

    public void Interact()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        owner.CleanTrash(this);
    }

    public string GetInteractionPrompt()
    {
        return "E - Убрать мусор";
    }
}
