using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class RoomDoor : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    [SerializeField] private string doorId = "PrivateRoom01";
    [SerializeField] private string roomDisplayName = "Приватная комната";

    [Header("Unlock Requirements")]
    [SerializeField, Min(1)] private int requiredClubLevel = 3;
    [SerializeField, Min(0)] private int unlockCost = 1500;

    [Header("Navigation")]
    [SerializeField] private ClientNavigationNode doorNavigationNode;

    [Header("Visuals")]
    [SerializeField] private Color lockedColor =
        new Color(0.55f, 0.12f, 0.12f);
    [SerializeField] private Color unlockedColor =
        new Color(0.15f, 0.55f, 0.25f);

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D doorCollider;
    private bool isUnlocked;

    public string DoorId => doorId;
    public string RoomDisplayName => roomDisplayName;
    public int RequiredClubLevel => requiredClubLevel;
    public int UnlockCost => unlockCost;
    public bool IsUnlocked => isUnlocked;

    public event Action StatusChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        doorCollider = GetComponent<BoxCollider2D>();
        YSortRenderer.Ensure(gameObject, 5200, -0.45f);
        ApplyState();
    }

    public void Configure(
        string newDoorId,
        string newRoomDisplayName,
        int newRequiredLevel,
        int newUnlockCost,
        ClientNavigationNode navigationNode)
    {
        doorId = newDoorId;
        roomDisplayName = newRoomDisplayName;
        requiredClubLevel = Mathf.Max(1, newRequiredLevel);
        unlockCost = Mathf.Max(0, newUnlockCost);
        doorNavigationNode = navigationNode;
        ApplyState();
    }

    public void Interact()
    {
        if (isUnlocked)
        {
            return;
        }

        ClubProgressionManager progression = ClubProgressionManager.Instance;
        if (progression == null)
        {
            Debug.LogWarning("ClubProgressionManager не найден.");
            return;
        }

        if (progression.Level < requiredClubLevel)
        {
            Debug.Log($"{roomDisplayName} откроется на уровне клуба {requiredClubLevel}.");
            return;
        }

        EconomyManager economy = EconomyManager.Instance;
        if (economy == null)
        {
            Debug.LogWarning("EconomyManager не найден.");
            return;
        }

        if (!economy.SpendMoney(
            unlockCost,
            EconomyTransactionCategory.RoomUnlock
        ))
        {
            Debug.Log($"Недостаточно денег для открытия {roomDisplayName}. Нужно: {unlockCost} ₽.");
            return;
        }

        Unlock();
    }

    public string GetInteractionPrompt()
    {
        if (isUnlocked)
        {
            return $"{roomDisplayName}: открыта";
        }

        int currentLevel = ClubProgressionManager.Instance != null
            ? ClubProgressionManager.Instance.Level
            : 1;

        if (currentLevel < requiredClubLevel)
        {
            return $"{roomDisplayName}: требуется уровень клуба {requiredClubLevel}";
        }

        int balance = EconomyManager.Instance != null
            ? EconomyManager.Instance.Money
            : 0;

        if (balance < unlockCost)
        {
            return $"{roomDisplayName}: нужно {unlockCost} ₽";
        }

        return $"Открыть {roomDisplayName} — {unlockCost} ₽";
    }

    public void Unlock()
    {
        if (isUnlocked)
        {
            return;
        }

        isUnlocked = true;
        ApplyState();
        Debug.Log($"{roomDisplayName} открыта.");
        StatusChanged?.Invoke();
    }

    public void RestoreState(bool unlocked)
    {
        isUnlocked = unlocked;
        ApplyState();
        StatusChanged?.Invoke();
    }

    private void ApplyState()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        doorCollider ??= GetComponent<BoxCollider2D>();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = isUnlocked ? unlockedColor : lockedColor;
            spriteRenderer.enabled = !isUnlocked;
        }

        if (doorCollider != null)
        {
            doorCollider.enabled = !isUnlocked;
        }

        if (doorNavigationNode != null)
        {
            doorNavigationNode.SetWalkable(isUnlocked);
        }
    }

    private void OnValidate()
    {
        requiredClubLevel = Mathf.Max(1, requiredClubLevel);
        unlockCost = Mathf.Max(0, unlockCost);
    }
}
