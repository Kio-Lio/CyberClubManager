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

    [Header("Visual Settings")]
    [SerializeField] private Color freeColor = Color.white;
    [SerializeField] private Color occupiedColor = Color.yellow;
    [SerializeField] private Color brokenColor = Color.red;

    private SpriteRenderer spriteRenderer;
    private Coroutine sessionCoroutine;

    public PCState State => state;
    public bool IsFree => state == PCState.Free;
    public bool IsOccupied => state == PCState.Occupied;
    public bool IsBroken => state == PCState.Broken;
    public bool IsAvailable => IsFree && !isReserved;
    public PCTier Tier => tier;
    public int DailyElectricityCost => dailyElectricityCost;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        PCRegistered = null;
        PCUnregistered = null;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        dailyElectricityCost = Mathf.Max(0, dailyElectricityCost);
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    public void Interact()
    {
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
            case PCState.Broken:
                TryRepair();
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

        Occupy();
        return true;
    }

    public bool TryReserve()
    {
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

    public bool TryOccupyReserved()
    {
        if (!IsFree || !isReserved)
        {
            return false;
        }

        isReserved = false;
        Occupy();
        return true;
    }

    private void Occupy()
    {
        SetState(PCState.Occupied);
        Debug.Log("Клиент посажен за ПК. ПК теперь занят.");

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddMoney(sessionPrice);
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

    private IEnumerator SessionTimer()
    {
        Debug.Log($"Игровая сессия началась. Длительность: {sessionDuration} секунд.");
        yield return new WaitForSeconds(sessionDuration);
        CompleteSession();
    }

    private void CompleteSession()
    {
        if (!IsOccupied)
        {
            return;
        }

        sessionCoroutine = null;

        if (UnityEngine.Random.value < breakdownChance)
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
        if (!IsFree || isReserved)
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
        if (!EconomyManager.Instance.SpendMoney(upgradeCost))
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

        if (!EconomyManager.Instance.SpendMoney(repairCost))
        {
            Debug.Log($"{name}: недостаточно денег для ремонта. Нужно {repairCost}.");
            return;
        }

        SetState(PCState.Free);
        Debug.Log($"{name}: ПК отремонтирован и снова доступен.");
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
