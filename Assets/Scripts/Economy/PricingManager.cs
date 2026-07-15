using System;
using UnityEngine;

public sealed class PricingManager : MonoBehaviour
{
    public static PricingManager Instance { get; private set; }

    [Header("Price Limits")]
    [SerializeField, Range(50, 200)] private int minimumPricePercent = 80;
    [SerializeField, Range(50, 200)] private int maximumPricePercent = 160;
    [SerializeField, Min(1)] private int priceStepPercent = 10;

    [Header("Current Prices")]
    [SerializeField] private int basicPricePercent = 100;
    [SerializeField] private int gamingPricePercent = 100;
    [SerializeField] private int premiumPricePercent = 100;

    public int MinimumPricePercent => minimumPricePercent;
    public int MaximumPricePercent => maximumPricePercent;
    public int PriceStepPercent => priceStepPercent;
    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NormalizeAllPrices();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public int GetPricePercent(PCTier tier)
    {
        return tier switch
        {
            PCTier.Basic => basicPricePercent,
            PCTier.Gaming => gamingPricePercent,
            PCTier.Premium => premiumPricePercent,
            _ => 100
        };
    }

    public int GetSessionPrice(PCTier tier, int basePrice)
    {
        return Mathf.Max(0, Mathf.RoundToInt(basePrice * GetPricePercent(tier) / 100f));
    }

    public bool CanClientAcceptPrice(PCTier tier, int clientTolerancePercent)
    {
        return GetPricePercent(tier) <= clientTolerancePercent;
    }

    public bool TryChangePrice(PCTier tier, int direction)
    {
        if (direction == 0)
        {
            return false;
        }

        int currentPercent = GetPricePercent(tier);
        int newPercent = NormalizePercent(currentPercent + Math.Sign(direction) * priceStepPercent);
        if (newPercent == currentPercent)
        {
            return false;
        }

        SetPricePercentInternal(tier, newPercent);
        Debug.Log($"Tariff {tier}: {newPercent}%.");
        StatusChanged?.Invoke();
        return true;
    }

    public void RestoreState(int savedBasicPercent, int savedGamingPercent, int savedPremiumPercent)
    {
        basicPricePercent = NormalizePercent(savedBasicPercent <= 0 ? 100 : savedBasicPercent);
        gamingPricePercent = NormalizePercent(savedGamingPercent <= 0 ? 100 : savedGamingPercent);
        premiumPricePercent = NormalizePercent(savedPremiumPercent <= 0 ? 100 : savedPremiumPercent);
        StatusChanged?.Invoke();
    }

    private void SetPricePercentInternal(PCTier tier, int percent)
    {
        switch (tier)
        {
            case PCTier.Basic: basicPricePercent = percent; break;
            case PCTier.Gaming: gamingPricePercent = percent; break;
            case PCTier.Premium: premiumPricePercent = percent; break;
        }
    }

    private int NormalizePercent(int value)
    {
        value = Mathf.Clamp(value, minimumPricePercent, maximumPricePercent);
        int steps = Mathf.RoundToInt((value - minimumPricePercent) / (float)priceStepPercent);
        return Mathf.Clamp(minimumPricePercent + steps * priceStepPercent, minimumPricePercent, maximumPricePercent);
    }

    private void NormalizeAllPrices()
    {
        basicPricePercent = NormalizePercent(basicPricePercent);
        gamingPricePercent = NormalizePercent(gamingPricePercent);
        premiumPricePercent = NormalizePercent(premiumPricePercent);
    }

    private void OnValidate()
    {
        minimumPricePercent = Mathf.Max(10, minimumPricePercent);
        maximumPricePercent = Mathf.Max(minimumPricePercent, maximumPricePercent);
        priceStepPercent = Mathf.Max(1, priceStepPercent);
        NormalizeAllPrices();
    }
}
