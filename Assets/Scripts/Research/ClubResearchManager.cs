using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ClubResearchManager : MonoBehaviour
{
    public static ClubResearchManager Instance { get; private set; }

    [SerializeField] private ClubResearchDefinition[] definitions;
    private readonly Dictionary<ClubResearchType, int> levels = new();
    private string lastStatusMessage = "Исследования еще не проводились.";

    public string LastStatusMessage => lastStatusMessage;
    public int TotalPurchasedLevels
    {
        get
        {
            int total = 0;
            foreach (int level in levels.Values) total += level;
            return total;
        }
    }
    public int ResearchedCategoryCount
    {
        get
        {
            int count = 0;
            foreach (int level in levels.Values)
            {
                if (level > 0) count++;
            }
            return count;
        }
    }

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureDefaultDefinitions();
        InitializeLevels();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public ClubResearchDefinition GetDefinition(ClubResearchType researchType)
    {
        if (definitions == null) return null;
        foreach (ClubResearchDefinition definition in definitions)
        {
            if (definition != null && definition.ResearchType == researchType)
                return definition;
        }
        return null;
    }

    public int GetLevel(ClubResearchType researchType) =>
        levels.TryGetValue(researchType, out int level) ? level : 0;

    public int GetRequiredClubLevel(int researchLevel) => researchLevel switch
    {
        1 => 2,
        2 => 3,
        3 => 5,
        _ => int.MaxValue
    };

    public int GetNextLevelCost(ClubResearchType researchType)
    {
        ClubResearchDefinition definition = GetDefinition(researchType);
        if (definition == null) return 0;
        float multiplier = (GetLevel(researchType) + 1) switch
        {
            1 => 1f,
            2 => 1.75f,
            3 => 2.75f,
            _ => 0f
        };
        return Mathf.RoundToInt(definition.BaseCost * multiplier);
    }

    public bool TryPurchaseResearch(ClubResearchType researchType)
    {
        ClubResearchDefinition definition = GetDefinition(researchType);
        if (definition == null) return false;
        int currentLevel = GetLevel(researchType);
        if (currentLevel >= definition.MaximumLevel)
        {
            SetStatus("Исследование уже завершено.");
            return false;
        }

        int nextLevel = currentLevel + 1;
        int requiredClubLevel = GetRequiredClubLevel(nextLevel);
        int clubLevel = ClubProgressionManager.Instance != null
            ? ClubProgressionManager.Instance.Level : 1;
        if (clubLevel < requiredClubLevel)
        {
            SetStatus($"Для уровня {nextLevel} нужен уровень клуба {requiredClubLevel}.");
            return false;
        }

        int cost = GetNextLevelCost(researchType);
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(
            cost, EconomyTransactionCategory.ResearchInvestment))
        {
            SetStatus($"Для исследования нужно {cost} ₽.");
            return false;
        }

        levels[researchType] = nextLevel;
        SetStatus($"{definition.DisplayName}: уровень {nextLevel} изучен.", true);
        return true;
    }

    public float GetPCBreakdownMultiplier() =>
        Mathf.Max(0.1f, 1f - GetLevel(ClubResearchType.ReliableComponents) * 0.10f);
    public float GetEquipmentWearMultiplier() =>
        Mathf.Max(0.1f, 1f - GetLevel(ClubResearchType.DurableEquipment) * 0.15f);
    public float GetCleanerSpeedMultiplier() =>
        1f + GetLevel(ClubResearchType.EfficientCleaning) * 0.20f;
    public float GetPurchasePriceMultiplier() =>
        Mathf.Max(0.1f, 1f - GetLevel(ClubResearchType.WholesalePurchasing) * 0.10f);
    public float GetMarketingCostMultiplier() =>
        Mathf.Max(0.1f, 1f - GetLevel(ClubResearchType.BrandPromotion) * 0.10f);
    public float GetInternetSpeedMultiplier() =>
        1f + GetLevel(ClubResearchType.NetworkOptimization) * 0.05f;
    public float GetElectricityCostMultiplier() =>
        Mathf.Max(0.1f, 1f - GetLevel(ClubResearchType.EnergyEfficiency) * 0.08f);

    public ClubResearchSaveData[] CreateSaveData()
    {
        ClubResearchSaveData[] result = new ClubResearchSaveData[levels.Count];
        int index = 0;
        foreach (KeyValuePair<ClubResearchType, int> pair in levels)
        {
            result[index++] = new ClubResearchSaveData
            {
                researchType = pair.Key,
                level = pair.Value
            };
        }
        return result;
    }

    public void RestoreState(ClubResearchSaveData[] savedResearch)
    {
        InitializeLevels();
        if (savedResearch != null)
        {
            foreach (ClubResearchSaveData item in savedResearch)
            {
                ClubResearchDefinition definition = item != null
                    ? GetDefinition(item.researchType) : null;
                if (definition != null)
                {
                    levels[item.researchType] = Mathf.Clamp(
                        item.level, 0, definition.MaximumLevel);
                }
            }
        }
        SetStatus("Исследования восстановлены.");
    }

    private void InitializeLevels()
    {
        levels.Clear();
        if (definitions == null) return;
        foreach (ClubResearchDefinition definition in definitions)
        {
            if (definition != null) levels[definition.ResearchType] = 0;
        }
    }

    private void SetStatus(string message, bool log = false)
    {
        lastStatusMessage = message;
        if (log) Debug.Log(message);
        StatusChanged?.Invoke();
    }

    private void EnsureDefaultDefinitions()
    {
        if (definitions != null && definitions.Length > 0) return;
        definitions = new[]
        {
            new ClubResearchDefinition(ClubResearchType.ReliableComponents, "Надежные комплектующие", "Снижает вероятность поломки ПК.", 3, 1000),
            new ClubResearchDefinition(ClubResearchType.DurableEquipment, "Качественная периферия", "Снижает износ клавиатур, мышей и кресел.", 3, 900),
            new ClubResearchDefinition(ClubResearchType.EfficientCleaning, "Эффективная уборка", "Повышает скорость передвижения уборщика.", 3, 700),
            new ClubResearchDefinition(ClubResearchType.WholesalePurchasing, "Оптовые закупки", "Снижает закупочную стоимость товаров.", 3, 800),
            new ClubResearchDefinition(ClubResearchType.BrandPromotion, "Продвижение бренда", "Снижает стоимость маркетинговых кампаний.", 3, 1200),
            new ClubResearchDefinition(ClubResearchType.NetworkOptimization, "Сетевая оптимизация", "Дополнительно ускоряет игровые сессии.", 3, 1500),
            new ClubResearchDefinition(ClubResearchType.EnergyEfficiency, "Энергоэффективность", "Снижает затраты на электричество.", 3, 1300)
        };
    }
}
