using System;
using System.Collections;
using UnityEngine;

public enum PCState
{
    Free,
    Occupied,
    Broken
}

public enum PCTier
{
    Basic,
    Gaming,
    Premium
}

[RequireComponent(typeof(SpriteRenderer))]
public class PC : MonoBehaviour, IInteractable
{
    public const int BasicToGamingUpgradeCost = 700;
    public const int GamingToPremiumUpgradeCost = 1400;

    public static event Action<PC> PCRegistered;
    public static event Action<PC> PCUnregistered;

    [Header("Session Settings")]
    [SerializeField] private PCState state = PCState.Free;
    [SerializeField] private int sessionPrice = 100;
    [SerializeField] private float sessionDuration = 10f;

    [Header("Tier Settings")]
    [SerializeField] private PCTier tier = PCTier.Basic;
    [SerializeField] private int dailyElectricityCost = 20;

    [Header("Breakdown Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float breakdownChance = 0.25f;
    [SerializeField] private int repairCost = 50;

    private bool isReserved;

    [Header("Equipment")]
    [SerializeField] private PCEquipmentCondition keyboard =
        new PCEquipmentCondition(PCEquipmentType.Keyboard, 120);
    [SerializeField] private PCEquipmentCondition mouse =
        new PCEquipmentCondition(PCEquipmentType.Mouse, 100);
    [SerializeField] private PCEquipmentCondition chair =
        new PCEquipmentCondition(PCEquipmentType.Chair, 180);
    [SerializeField, Min(0f)] private float minimumWearPerSession = 2f;
    [SerializeField, Min(0f)] private float maximumWearPerSession = 6f;

    [Header("Room Access")]
    [SerializeField] private RoomDoor requiredRoomDoor;

    [Header("Navigation")]
    [SerializeField] private ClientNavigationNode approachNode;

    [Header("Visual Settings")]
    [SerializeField] private Color freeColor = Color.white;
    [SerializeField] private Color occupiedColor = Color.yellow;
    [SerializeField] private Color brokenColor = Color.red;

    private SpriteRenderer spriteRenderer;
    private Coroutine sessionCoroutine;
    private ClientType activeSessionClientType;

    public PCState State => state;
    public bool IsFree => state == PCState.Free;
    public bool IsOccupied => state == PCState.Occupied;
    public bool IsBroken => state == PCState.Broken;
    public bool IsReserved => isReserved;
    public bool HasInternetAccess =>
        ClubRandomEventManager.Instance == null ||
        !ClubRandomEventManager.Instance.IsInternetUnavailable;
    public bool IsAvailable =>
        IsFree && !isReserved && HasRoomAccess && !HasBrokenEquipment &&
        HasInternetAccess;
    public PCTier Tier => tier;
    public ClientNavigationNode ApproachNode => approachNode;
    public RoomDoor RequiredRoomDoor => requiredRoomDoor;
    public bool HasRoomAccess =>
        requiredRoomDoor == null || requiredRoomDoor.IsUnlocked;
    public bool CanServiceEquipment =>
        !IsOccupied && !isReserved && HasRoomAccess;
    public int DailyElectricityCost => dailyElectricityCost;
    public int BaseSessionPrice => sessionPrice;
    public int CurrentSessionPrice => PricingManager.Instance == null
        ? sessionPrice
        : PricingManager.Instance.GetSessionPrice(Tier, sessionPrice);
    public int LastSessionIncome { get; private set; }
    public PCEquipmentCondition Keyboard => keyboard;
    public PCEquipmentCondition Mouse => mouse;
    public PCEquipmentCondition Chair => chair;
    public bool HasBrokenEquipment =>
        keyboard.IsBroken || mouse.IsBroken || chair.IsBroken;
    public float LowestEquipmentCondition => Mathf.Min(
        keyboard.Condition,
        mouse.Condition,
        chair.Condition
    );
    public PCEquipmentType MostDamagedEquipmentType =>
        GetMostDamagedEquipment().EquipmentType;
    public float MostDamagedEquipmentCondition =>
        GetMostDamagedEquipment().Condition;
    public bool CanUpgrade => tier != PCTier.Premium;
    public int NextUpgradeCost
    {
        get
        {
            return tier switch
            {
                PCTier.Basic => BasicToGamingUpgradeCost,
                PCTier.Gaming => GamingToPremiumUpgradeCost,
                _ => 0
            };
        }
    }

    public event Action<PCState> StateChanged;
    public event Action<PCTier> TierChanged;
    public event Action EquipmentChanged;
    public event Action<PCSessionAnalyticsData> SessionAnalyticsCompleted;
    public event Action<PC> SessionCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        PCRegistered = null;
        PCUnregistered = null;
    }

    private void Awake()
    {
        EnsureEquipmentConditions();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ConfigureInteractionCollider();
        ConfigureYSorting();
        UpdateVisual();
    }

    private void OnEnable()
    {
        PCRegistered?.Invoke(this);
    }

    private void OnDisable()
    {
        PCUnregistered?.Invoke(this);
    }

    private void OnValidate()
    {
        EnsureEquipmentConditions();
        dailyElectricityCost = Mathf.Max(0, dailyElectricityCost);
        minimumWearPerSession = Mathf.Max(0f, minimumWearPerSession);
        maximumWearPerSession = Mathf.Max(0f, maximumWearPerSession);
        spriteRenderer = GetComponent<SpriteRenderer>();
        ConfigureInteractionCollider();
        UpdateVisual();
    }

    public void Interact()
    {
        if (!HasRoomAccess)
        {
            return;
        }

        if (IsBroken)
        {
            TryRepair();
            return;
        }

        PCEquipmentCondition damagedEquipment = GetMostDamagedEquipment();
        if (damagedEquipment.Condition < 100f)
        {
            TryRepairEquipment(damagedEquipment);
            return;
        }

        switch (state)
        {
            case PCState.Free:
                if (isReserved)
                {
                    Debug.Log(
                        $"{name}: ПК зарезервирован клиентом. " +
                        "Улучшение сейчас невозможно."
                    );
                }
                else
                {
                    TryUpgrade();
                }

                break;
            case PCState.Occupied:
                Debug.Log($"{name}: нельзя улучшить занятый ПК.");
                break;
        }
    }

    public string GetTierDisplayName()
    {
        return tier switch
        {
            PCTier.Basic => "Базовый",
            PCTier.Gaming => "Игровой",
            PCTier.Premium => "Премиальный",
            _ => tier.ToString()
        };
    }

    public string GetInteractionPrompt()
    {
        if (!HasRoomAccess)
        {
            return $"{name}: находится в закрытой комнате";
        }

        if (IsBroken)
        {
            return $"E - Отремонтировать за {repairCost} ₽";
        }

        PCEquipmentCondition damagedEquipment = GetMostDamagedEquipment();
        if (damagedEquipment.Condition < 100f)
        {
            return
                $"{name}: починить " +
                $"{GetEquipmentDisplayName(damagedEquipment.EquipmentType)} - " +
                $"{damagedEquipment.RepairCost} ₽ " +
                $"({damagedEquipment.Condition:F0}%)";
        }

        if (!HasRoomAccess)
        {
            return $"{name}: находится в закрытой комнате";
        }

        switch (state)
        {
            case PCState.Broken:
                return $"E — Отремонтировать за {repairCost} ₽";

            case PCState.Occupied:
                return "ПК занят";

            case PCState.Free:
                if (isReserved)
                {
                    return "ПК зарезервирован клиентом";
                }

                if (!CanUpgrade)
                {
                    return "Премиальный ПК — максимальный класс";
                }

                return
                    $"E — Улучшить до {GetNextTierDisplayName()} " +
                    $"за {NextUpgradeCost} ₽";

            default:
                return string.Empty;
        }
    }

    public void RestoreTier(PCTier savedTier)
    {
        if (!Enum.IsDefined(typeof(PCTier), savedTier))
        {
            savedTier = PCTier.Basic;
        }

        ApplyTier(savedTier);
    }

    public void SetApproachNode(ClientNavigationNode navigationNode)
    {
        approachNode = navigationNode;
    }

    public void SetRequiredRoomDoor(RoomDoor roomDoor)
    {
        requiredRoomDoor = roomDoor;
    }

    public void ConfigureYSorting()
    {
        YSortRenderer.Ensure(gameObject, 12, -0.45f);
    }

    private void ConfigureInteractionCollider()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();

        if (boxCollider == null)
        {
            return;
        }

        boxCollider.isTrigger = true;
    }

    public void SetState(PCState newState)
    {
        if (state == newState)
        {
            return;
        }

        state = newState;

        if (state != PCState.Free)
        {
            isReserved = false;
        }

        UpdateVisual();
        StateChanged?.Invoke(state);
    }

    public bool TryOccupy()
    {
        if (!IsAvailable)
        {
            return false;
        }

        Occupy(ClientType.Regular);
        return true;
    }

    public bool TryReserve()
    {
        if (HasBrokenEquipment)
        {
            return false;
        }

        if (!IsAvailable)
        {
            return false;
        }

        isReserved = true;
        return true;
    }

    public void CancelReservation()
    {
        if (IsFree)
        {
            isReserved = false;
        }
    }

    public bool ForceBreakdown()
    {
        if (IsOccupied || IsBroken)
        {
            return false;
        }

        isReserved = false;
        SetState(PCState.Broken);
        return true;
    }

    public bool TryOccupyReserved(ClientType clientType)
    {
        if (HasBrokenEquipment || !HasInternetAccess)
        {
            isReserved = false;
            return false;
        }

        if (!IsFree || !isReserved || !HasRoomAccess)
        {
            return false;
        }

        isReserved = false;
        Occupy(clientType);
        return true;
    }

    private void Occupy(ClientType clientType)
    {
        SetState(PCState.Occupied);
        activeSessionClientType = clientType;

        int pricedSessionIncome = CurrentSessionPrice;
        int clientBonus = GetClientSessionBonus(clientType);
        LastSessionIncome = pricedSessionIncome + clientBonus;

        Debug.Log(
            $"{name}: клиент типа {GetClientTypeDisplayName(clientType)} " +
            $"начал сессию. Доход: {LastSessionIncome} ₽."
        );

        Debug.Log(
            $"{name}: tariff {PricingManager.Instance?.GetPricePercent(Tier) ?? 100}%, " +
            $"session {pricedSessionIncome} RUB, bonus {clientBonus} RUB, " +
            $"total {LastSessionIncome} RUB."
        );

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(
                LastSessionIncome,
                EconomyTransactionCategory.SessionRevenue
            );
        }
        else
        {
            Debug.LogWarning("EconomyManager не найден в сцене.");
        }

        if (sessionCoroutine != null)
        {
            StopCoroutine(sessionCoroutine);
        }

        sessionCoroutine = StartCoroutine(SessionTimer());
    }

    public static int GetClientSessionBonus(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => 0,
            ClientType.Gamer => 40,
            ClientType.VIP => 100,
            _ => 0
        };
    }

    private static string GetClientTypeDisplayName(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => "Обычный",
            ClientType.Gamer => "Геймер",
            ClientType.VIP => "VIP",
            _ => clientType.ToString()
        };
    }

    private IEnumerator SessionTimer()
    {
        float effectiveDuration = GetEffectiveSessionDuration();
        Debug.Log(
            $"Игровая сессия началась. Длительность: " +
            $"{effectiveDuration:F2} секунд."
        );
        yield return new WaitForSeconds(effectiveDuration);
        CompleteSession();
    }

    private float GetEffectiveSessionDuration()
    {
        float speedMultiplier = InternetProviderManager.Instance != null
            ? InternetProviderManager.Instance.GetSessionSpeedMultiplier()
            : 1f;
        return sessionDuration / Mathf.Max(0.1f, speedMultiplier);
    }

    private void CompleteSession()
    {
        if (!IsOccupied)
        {
            return;
        }

        sessionCoroutine = null;
        ApplyEquipmentWear();

        int pricePercent = PricingManager.Instance != null
            ? PricingManager.Instance.GetPricePercent(Tier)
            : 100;
        SessionAnalyticsCompleted?.Invoke(
            new PCSessionAnalyticsData(
                name,
                Tier,
                activeSessionClientType,
                LastSessionIncome,
                pricePercent
            )
        );
        SessionCompleted?.Invoke(this);

        float breakdownMultiplier = ClubResearchManager.Instance != null
            ? ClubResearchManager.Instance.GetPCBreakdownMultiplier()
            : 1f;
        float effectiveBreakdownChance = breakdownChance * breakdownMultiplier;

        if (UnityEngine.Random.value < effectiveBreakdownChance)
        {
            SetState(PCState.Broken);
            Debug.Log($"{name}: игровая сессия завершена, но ПК сломался.");
        }
        else
        {
            SetState(PCState.Free);
            Debug.Log($"{name}: игровая сессия завершена. ПК снова свободен.");
        }
    }

    private void TryUpgrade()
    {
        if (!IsFree || isReserved || !HasRoomAccess)
        {
            Debug.Log($"{name}: сейчас этот ПК нельзя улучшить.");
            return;
        }

        if (!CanUpgrade)
        {
            Debug.Log($"{name}: ПК уже имеет максимальный класс.");
            return;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager не найден. Улучшение невозможно.");
            return;
        }

        int upgradeCost = NextUpgradeCost;
        if (!EconomyManager.Instance.SpendMoney(
            upgradeCost,
            EconomyTransactionCategory.PCUpgrade
        ))
        {
            Debug.Log($"{name}: для улучшения требуется {upgradeCost} ₽.");
            return;
        }

        PCTier nextTier = tier switch
        {
            PCTier.Basic => PCTier.Gaming,
            PCTier.Gaming => PCTier.Premium,
            _ => tier
        };

        ApplyTier(nextTier);

        Debug.Log(
            $"{name}: улучшен до класса {GetTierDisplayName()}. " +
            $"Стоимость: {upgradeCost} ₽."
        );
    }

    private void ApplyTier(PCTier newTier)
    {
        tier = newTier;

        switch (tier)
        {
            case PCTier.Basic:
                sessionPrice = 100;
                breakdownChance = 0.25f;
                repairCost = 50;
                dailyElectricityCost = 20;
                freeColor = Color.white;
                break;
            case PCTier.Gaming:
                sessionPrice = 160;
                breakdownChance = 0.18f;
                repairCost = 90;
                dailyElectricityCost = 30;
                freeColor = new Color(0.3f, 0.65f, 1f);
                break;
            case PCTier.Premium:
                sessionPrice = 250;
                breakdownChance = 0.10f;
                repairCost = 150;
                dailyElectricityCost = 45;
                freeColor = new Color(0.75f, 0.35f, 1f);
                break;
        }

        UpdateVisual();
        TierChanged?.Invoke(tier);
    }

    private string GetNextTierDisplayName()
    {
        return tier switch
        {
            PCTier.Basic => "Gaming",
            PCTier.Gaming => "Premium",
            _ => string.Empty
        };
    }

    private void TryRepair()
    {
        if (!IsBroken)
        {
            return;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("EconomyManager не найден. Ремонт невозможен.");
            return;
        }

        if (!EconomyManager.Instance.SpendMoney(
            repairCost,
            EconomyTransactionCategory.PCRepair
        ))
        {
            Debug.Log($"{name}: недостаточно денег для ремонта. Нужно {repairCost}.");
            return;
        }

        SetState(PCState.Free);
        Debug.Log($"{name}: ПК отремонтирован и снова доступен.");
    }

    public void RestoreEquipmentCondition(
        float keyboardCondition,
        float mouseCondition,
        float chairCondition)
    {
        EnsureEquipmentConditions();
        keyboard.RestoreCondition(keyboardCondition);
        mouse.RestoreCondition(mouseCondition);
        chair.RestoreCondition(chairCondition);
        EquipmentChanged?.Invoke();
        StateChanged?.Invoke(State);
    }

    public PCEquipmentCondition GetEquipment(PCEquipmentType equipmentType)
    {
        return equipmentType switch
        {
            PCEquipmentType.Keyboard => keyboard,
            PCEquipmentType.Mouse => mouse,
            PCEquipmentType.Chair => chair,
            _ => null
        };
    }

    public bool TryRepairEquipment(PCEquipmentType equipmentType)
    {
        return TryRepairEquipment(GetEquipment(equipmentType));
    }

    public int GetTotalEquipmentRepairCost()
    {
        int totalCost = 0;

        if (keyboard.Condition < 100f)
        {
            totalCost += keyboard.RepairCost;
        }

        if (mouse.Condition < 100f)
        {
            totalCost += mouse.RepairCost;
        }

        if (chair.Condition < 100f)
        {
            totalCost += chair.RepairCost;
        }

        return totalCost;
    }

    public bool TryRepairAllEquipment()
    {
        if (!CanServiceEquipment)
        {
            return false;
        }

        int totalCost = GetTotalEquipmentRepairCost();
        EconomyManager economy = EconomyManager.Instance;

        if (totalCost <= 0 || economy == null ||
            !economy.SpendMoney(
                totalCost,
                EconomyTransactionCategory.EquipmentRepair
            ))
        {
            return false;
        }

        if (keyboard.Condition < 100f)
        {
            keyboard.Repair();
        }

        if (mouse.Condition < 100f)
        {
            mouse.Repair();
        }

        if (chair.Condition < 100f)
        {
            chair.Repair();
        }

        Debug.Log($"{name}: всё оборудование отремонтировано за {totalCost} ₽.");
        EquipmentChanged?.Invoke();
        StateChanged?.Invoke(State);
        return true;
    }

    private void ApplyEquipmentWear()
    {
        float minimumWear = Mathf.Min(
            minimumWearPerSession,
            maximumWearPerSession
        );
        float maximumWear = Mathf.Max(
            minimumWearPerSession,
            maximumWearPerSession
        );

        float wearMultiplier = ClubResearchManager.Instance != null
            ? ClubResearchManager.Instance.GetEquipmentWearMultiplier()
            : 1f;
        float keyboardWear = UnityEngine.Random.Range(minimumWear, maximumWear);
        float mouseWear = UnityEngine.Random.Range(minimumWear, maximumWear);
        float chairWear = UnityEngine.Random.Range(minimumWear, maximumWear);

        keyboard.ApplyWear(keyboardWear * wearMultiplier);
        mouse.ApplyWear(mouseWear * wearMultiplier);
        chair.ApplyWear(chairWear * wearMultiplier);

        Debug.Log(
            $"{name}: состояние оборудования - " +
            $"клавиатура {keyboard.Condition:F0}%, " +
            $"мышь {mouse.Condition:F0}%, " +
            $"кресло {chair.Condition:F0}%."
        );

        EquipmentChanged?.Invoke();
    }

    private bool TryRepairEquipment(PCEquipmentCondition equipment)
    {
        if (equipment == null || equipment.Condition >= 100f ||
            !CanServiceEquipment)
        {
            return false;
        }

        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(
            equipment.RepairCost,
            EconomyTransactionCategory.EquipmentRepair
        ))
        {
            Debug.Log($"{name}: недостаточно денег на ремонт оборудования.");
            return false;
        }

        equipment.Repair();
        Debug.Log(
            $"{name}: отремонтирована " +
            GetEquipmentDisplayName(equipment.EquipmentType) + "."
        );
        EquipmentChanged?.Invoke();
        StateChanged?.Invoke(State);
        return true;
    }

    private PCEquipmentCondition GetMostDamagedEquipment()
    {
        PCEquipmentCondition mostDamaged = keyboard;

        if (mouse.Condition < mostDamaged.Condition)
        {
            mostDamaged = mouse;
        }

        if (chair.Condition < mostDamaged.Condition)
        {
            mostDamaged = chair;
        }

        return mostDamaged;
    }

    private static string GetEquipmentDisplayName(PCEquipmentType equipmentType)
    {
        return equipmentType switch
        {
            PCEquipmentType.Keyboard => "клавиатура",
            PCEquipmentType.Mouse => "мышь",
            PCEquipmentType.Chair => "кресло",
            _ => "оборудование"
        };
    }

    private void EnsureEquipmentConditions()
    {
        keyboard ??= new PCEquipmentCondition(PCEquipmentType.Keyboard, 120);
        mouse ??= new PCEquipmentCondition(PCEquipmentType.Mouse, 100);
        chair ??= new PCEquipmentCondition(PCEquipmentType.Chair, 180);
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        switch (state)
        {
            case PCState.Free:
                spriteRenderer.color = freeColor;
                break;
            case PCState.Occupied:
                spriteRenderer.color = occupiedColor;
                break;
            case PCState.Broken:
                spriteRenderer.color = brokenColor;
                break;
        }
    }
}
