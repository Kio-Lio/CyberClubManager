using System;
using UnityEngine;

public sealed class InternetProviderManager : MonoBehaviour
{
    public static InternetProviderManager Instance { get; private set; }

    [SerializeField] private InternetPlanDefinition[] plans;
    [SerializeField] private InternetPlanType activePlan = InternetPlanType.Basic;

    [Header("Outage generation")]
    [SerializeField, Min(1f)] private float reliabilityCheckInterval = 30f;
    [SerializeField, Min(1f)] private float generatedOutageDuration = 15f;

    private float reliabilityTimer;
    private string lastStatusMessage = "Подключен базовый интернет.";

    public InternetPlanType ActivePlan => activePlan;
    public string LastStatusMessage => lastStatusMessage;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDefaultPlans();
        reliabilityTimer = reliabilityCheckInterval;
    }

    private void Update()
    {
        if (FirstDayTutorialManager.Instance != null &&
            FirstDayTutorialManager.Instance.SuppressProviderFailures)
        {
            return;
        }

        reliabilityTimer -= Time.deltaTime;
        if (reliabilityTimer > 0f)
        {
            return;
        }

        reliabilityTimer = reliabilityCheckInterval;
        TryGenerateProviderOutage();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        reliabilityCheckInterval = Mathf.Max(1f, reliabilityCheckInterval);
        generatedOutageDuration = Mathf.Max(1f, generatedOutageDuration);
    }

    public InternetPlanDefinition GetPlan(InternetPlanType planType)
    {
        EnsureDefaultPlans();
        foreach (InternetPlanDefinition plan in plans)
        {
            if (plan != null && plan.PlanType == planType)
            {
                return plan;
            }
        }

        return null;
    }

    public InternetPlanDefinition GetActivePlan()
    {
        return GetPlan(activePlan);
    }

    public float GetSessionSpeedMultiplier()
    {
        InternetPlanDefinition plan = GetActivePlan();
        float planMultiplier = plan != null
            ? Mathf.Max(0.1f, plan.SessionSpeedMultiplier)
            : 1f;
        float researchMultiplier = ClubResearchManager.Instance != null
            ? ClubResearchManager.Instance.GetInternetSpeedMultiplier()
            : 1f;
        return planMultiplier * researchMultiplier;
    }

    public int GetDailyCost()
    {
        return GetActivePlan()?.DailyCost ?? 0;
    }

    public bool TrySwitchPlan(InternetPlanType newPlanType)
    {
        if (newPlanType == activePlan)
        {
            lastStatusMessage = "Этот тариф уже подключен.";
            StatusChanged?.Invoke();
            return false;
        }

        InternetPlanDefinition plan = GetPlan(newPlanType);
        if (plan == null)
        {
            lastStatusMessage = "Выбранный тариф недоступен.";
            StatusChanged?.Invoke();
            return false;
        }

        EconomyManager economy = EconomyManager.Instance;
        if (economy == null)
        {
            lastStatusMessage = "Экономика клуба недоступна.";
            StatusChanged?.Invoke();
            return false;
        }

        if (plan.ConnectionCost > 0 &&
            !economy.SpendMoney(
                plan.ConnectionCost,
                EconomyTransactionCategory.InternetConnection
            ))
        {
            lastStatusMessage = $"Для подключения нужно {plan.ConnectionCost} ₽.";
            StatusChanged?.Invoke();
            return false;
        }

        activePlan = newPlanType;
        lastStatusMessage = $"Подключен тариф «{plan.DisplayName}».";
        Debug.Log(lastStatusMessage);
        StatusChanged?.Invoke();
        return true;
    }

    private void TryGenerateProviderOutage()
    {
        ClubRandomEventManager eventManager = ClubRandomEventManager.Instance;
        if (eventManager == null || eventManager.HasActiveEvent)
        {
            return;
        }

        InternetPlanDefinition plan = GetActivePlan();
        if (plan == null)
        {
            return;
        }

        float failureChance = 1f - Mathf.Clamp01(plan.Reliability);
        if (UnityEngine.Random.value >= failureChance)
        {
            return;
        }

        if (!eventManager.TriggerInternetOutage(
            generatedOutageDuration,
            $"Сбой у провайдера «{plan.DisplayName}»."
        ))
        {
            return;
        }

        lastStatusMessage = "У провайдера произошел сбой.";
        StatusChanged?.Invoke();
    }

    public void RestoreState(InternetPlanType savedPlan)
    {
        activePlan = GetPlan(savedPlan) != null
            ? savedPlan
            : InternetPlanType.Basic;
        InternetPlanDefinition plan = GetActivePlan();
        lastStatusMessage =
            $"Подключен тариф «{plan?.DisplayName ?? "Базовый"}».";
        reliabilityTimer = reliabilityCheckInterval;
        StatusChanged?.Invoke();
    }

    private void EnsureDefaultPlans()
    {
        if (plans != null && plans.Length > 0)
        {
            return;
        }

        plans = new[]
        {
            new InternetPlanDefinition(
                InternetPlanType.Basic,
                "Базовый",
                0,
                120,
                1f,
                0.96f
            ),
            new InternetPlanDefinition(
                InternetPlanType.Gaming,
                "Игровой",
                1000,
                250,
                1.15f,
                0.98f
            ),
            new InternetPlanDefinition(
                InternetPlanType.Professional,
                "Профессиональный",
                3000,
                450,
                1.30f,
                0.995f
            )
        };
    }
}
