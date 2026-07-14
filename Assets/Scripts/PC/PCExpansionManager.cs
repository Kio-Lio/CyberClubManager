using System;
using UnityEngine;

public sealed class PCExpansionManager : MonoBehaviour
{
    private static readonly Vector3[] DefaultExpansionPositions =
    {
        new Vector3(6.2f, -1.4f, 0f),
        new Vector3(1.4f, -3.4f, 0f),
        new Vector3(3.8f, -3.4f, 0f),
        new Vector3(6.2f, -3.4f, 0f)
    };

    public static PCExpansionManager Instance { get; private set; }

    [Header("Purchase Settings")]
    [SerializeField] private int pcPurchaseCost = 500;

    [Header("Expansion Positions")]
    [SerializeField] private Vector3[] expansionPositions =
        DefaultExpansionPositions;

    private int nextSlotIndex;
    private Sprite generatedPCSprite;

    public int PurchaseCost => pcPurchaseCost;
    public int PurchasedPCCount => nextSlotIndex;
    public int TotalExpansionSlots => expansionPositions.Length;
    public int UnlockedSlotCount
    {
        get
        {
            int totalSlots = expansionPositions.Length;

            if (ClubProgressionManager.Instance == null)
            {
                return totalSlots;
            }

            return Mathf.Min(
                totalSlots,
                ClubProgressionManager.Instance.UnlockedExpansionSlots
            );
        }
    }

    public int RemainingSlots =>
        Mathf.Max(0, UnlockedSlotCount - nextSlotIndex);
    public bool HasAvailableSlot =>
        nextSlotIndex < UnlockedSlotCount &&
        nextSlotIndex < expansionPositions.Length;

    public event Action StatusChanged;

    private void Awake()
    {
        EnsureExpansionPositions();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (ClubProgressionManager.Instance != null)
        {
            ClubProgressionManager.Instance.StatusChanged +=
                OnProgressionChanged;
        }
    }

    private void OnDestroy()
    {
        if (ClubProgressionManager.Instance != null)
        {
            ClubProgressionManager.Instance.StatusChanged -=
                OnProgressionChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryPurchaseNextPC()
    {
        EnsureExpansionPositions();

        if (BankruptcyManager.Instance != null && BankruptcyManager.Instance.IsGameOver)
        {
            return false;
        }

        if (nextSlotIndex >= UnlockedSlotCount &&
            nextSlotIndex < expansionPositions.Length)
        {
            int requiredLevel = Mathf.Min(4, nextSlotIndex + 1);

            Debug.Log(
                $"Следующее место для ПК откроется на уровне клуба {requiredLevel}."
            );

            return false;
        }

        if (!HasAvailableSlot)
        {
            Debug.Log("Все доступные места для ПК уже заняты.");
            return false;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager не найден. Покупка невозможна.");
            return false;
        }

        if (!EconomyManager.Instance.SpendMoney(pcPurchaseCost))
        {
            Debug.Log($"Для покупки нового ПК требуется {pcPurchaseCost} ₽.");
            return false;
        }

        Vector3 position = expansionPositions[nextSlotIndex];
        CreatePC(position);
        nextSlotIndex++;

        Debug.Log(
            $"Куплен новый ПК за {pcPurchaseCost} ₽. " +
            $"Свободных мест для расширения: {RemainingSlots}."
        );

        StatusChanged?.Invoke();
        return true;
    }

    public void RestorePurchasedPCs(int savedPurchasedPCCount)
    {
        EnsureExpansionPositions();

        int targetCount = Mathf.Clamp(
            savedPurchasedPCCount,
            0,
            expansionPositions.Length
        );

        while (nextSlotIndex < targetCount)
        {
            CreatePC(expansionPositions[nextSlotIndex]);
            nextSlotIndex++;
        }

        StatusChanged?.Invoke();
    }

    private void OnProgressionChanged()
    {
        StatusChanged?.Invoke();
    }

    private void CreatePC(Vector3 position)
    {
        int pcNumber = FindObjectsByType<PC>().Length + 1;
        GameObject pcObject = new GameObject($"PC_{pcNumber:00}");
        pcObject.transform.position = position;

        SpriteRenderer spriteRenderer = pcObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedPCSprite();
        spriteRenderer.color = Color.white;

        BoxCollider2D collider = pcObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;
        PC pc = pcObject.AddComponent<PC>();

        ClientNavigationManager navigation =
            ClientNavigationManager.Instance ??
            ClientNavigationManager.EnsureRuntimeGraph();
        navigation.EnsureApproachNode(pc);
    }

    private Sprite GetGeneratedPCSprite()
    {
        if (generatedPCSprite != null)
        {
            return generatedPCSprite;
        }

        Texture2D texture = new Texture2D(16, 16)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedPCSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f
        );

        return generatedPCSprite;
    }

    private void OnValidate()
    {
        EnsureExpansionPositions();
        pcPurchaseCost = Mathf.Max(1, pcPurchaseCost);
    }

    private void EnsureExpansionPositions()
    {
        if (expansionPositions != null &&
            expansionPositions.Length == DefaultExpansionPositions.Length)
        {
            bool matchesDefault = true;

            for (int i = 0; i < DefaultExpansionPositions.Length; i++)
            {
                if (expansionPositions[i] == DefaultExpansionPositions[i])
                {
                    continue;
                }

                matchesDefault = false;
                break;
            }

            if (matchesDefault)
            {
                return;
            }
        }

        expansionPositions = (Vector3[])DefaultExpansionPositions.Clone();
    }
}
