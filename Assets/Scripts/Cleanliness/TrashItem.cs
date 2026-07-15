using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class TrashItem : MonoBehaviour, IInteractable
{
    private ClubCleanlinessManager owner;
    private bool reservedByCleaner;

    public string TrashId { get; private set; }
    public string SourcePCName { get; private set; }
    public bool IsReservedByCleaner => reservedByCleaner;

    public void Initialize(
        ClubCleanlinessManager manager,
        string trashId,
        string sourcePCName)
    {
        owner = manager;
        TrashId = trashId;
        SourcePCName = sourcePCName;
        reservedByCleaner = false;
    }

    public bool TryReserveForCleaner()
    {
        if (reservedByCleaner)
        {
            return false;
        }

        reservedByCleaner = true;
        return true;
    }

    public void ReleaseCleanerReservation()
    {
        reservedByCleaner = false;
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
        return reservedByCleaner
            ? "E - Убрать мусор (уборщик уже идёт)"
            : "E - Убрать мусор";
    }
}
