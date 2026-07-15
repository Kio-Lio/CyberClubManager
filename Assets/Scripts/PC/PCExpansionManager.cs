using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PCExpansionManager : MonoBehaviour
{
    public static PCExpansionManager Instance { get; private set; }

    [Header("Purchase Settings")]
    [SerializeField] private int pcPurchaseCost = 500;

    [Header("Expansion Positions")]
    [SerializeField] private Vector3[] expansionPositions =
        CreateDefaultExpansionPositions();

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

        NormalizeExistingExpansionPCs();
        StatusChanged?.Invoke();
    }

    public void NormalizeExistingExpansionPCs()
    {
        ResetExpansionPositionsToDefault();

        int highestExistingSlot = -1;

        for (int index = 0; index < expansionPositions.Length; index++)
        {
            string pcName = $"PC_{index + 6:00}";
            GameObject pcObject = FindExistingUniquePCObject(pcName);

            if (pcObject == null)
            {
                continue;
            }

            highestExistingSlot = index;
            ConfigureExistingPC(pcObject, expansionPositions[index]);
        }

        if (highestExistingSlot >= 0)
        {
            nextSlotIndex = Mathf.Max(
                nextSlotIndex,
                highestExistingSlot + 1
            );
        }

        StatusChanged?.Invoke();
    }

    public void ResetExpansionPositionsToDefault()
    {
        expansionPositions = CreateDefaultExpansionPositions();
    }

    private static Vector3[] CreateDefaultExpansionPositions()
    {
        return new[]
        {
            new Vector3(6.4f, -0.7f, 0f),
            new Vector3(1.2f, -3.3f, 0f),
            new Vector3(3.8f, -3.3f, 0f),
            new Vector3(6.4f, -3.3f, 0f)
        };
    }

    private void OnProgressionChanged()
    {
        StatusChanged?.Invoke();
    }

    private void CreatePC(Vector3 position)
    {
        int pcNumber = nextSlotIndex + 6;
        string pcName = $"PC_{pcNumber:00}";
        GameObject pcObject = FindOrCreateUniquePCObject(pcName);

        ConfigureExistingPC(pcObject, position);
    }

    private static GameObject FindOrCreateUniquePCObject(string pcName)
    {
        GameObject keptObject = FindExistingUniquePCObject(pcName);

        return keptObject != null
            ? keptObject
            : new GameObject(pcName);
    }

    private static GameObject FindExistingUniquePCObject(string pcName)
    {
        GameObject keptObject = null;

        foreach (GameObject rootObject in
                 SceneManager.GetActiveScene().GetRootGameObjects())
        {
            keptObject = FindAndRemoveDuplicatePCObjects(
                rootObject.transform,
                pcName,
                keptObject
            );
        }

        return keptObject;
    }

    private static GameObject FindAndRemoveDuplicatePCObjects(
        Transform current,
        string pcName,
        GameObject keptObject)
    {
        if (current.name == pcName)
        {
            if (keptObject == null)
            {
                keptObject = current.gameObject;
            }
            else
            {
                DestroyDuplicate(current.gameObject);
                return keptObject;
            }
        }

        for (int index = current.childCount - 1; index >= 0; index--)
        {
            keptObject = FindAndRemoveDuplicatePCObjects(
                current.GetChild(index),
                pcName,
                keptObject
            );
        }

        return keptObject;
    }

    private static void DestroyDuplicate(GameObject duplicate)
    {
        if (duplicate == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(duplicate);
            return;
        }

        DestroyImmediate(duplicate);
    }

    private void ConfigureExistingPC(GameObject pcObject, Vector3 position)
    {
        pcObject.transform.position = position;
        pcObject.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer =
            pcObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = pcObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = GetGeneratedPCSprite();
        }

        spriteRenderer.color = Color.white;

        BoxCollider2D collider =
            pcObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = pcObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;

        PC pc = pcObject.GetComponent<PC>();

        if (pc == null)
        {
            pc = pcObject.AddComponent<PC>();
        }

        pc.ConfigureYSorting();

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
        ResetExpansionPositionsToDefault();
    }
}
