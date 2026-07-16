using System;
using UnityEngine;

public sealed class MarketingManager : MonoBehaviour
{
    public static MarketingManager Instance { get; private set; }

    [SerializeField] private MarketingCampaignDefinition[] campaignDefinitions;
    private MarketingCampaignType activeCampaign = MarketingCampaignType.None;
    private int remainingDays;
    private string lastStatusMessage = "No marketing campaign is active.";

    public MarketingCampaignType ActiveCampaign => activeCampaign;
    public int RemainingDays => remainingDays;
    public bool HasActiveCampaign => activeCampaign != MarketingCampaignType.None && remainingDays > 0;
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
        EnsureDefaultDefinitions();
    }

    private void Start()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded += OnDayEnded;
        }
    }

    private void OnDestroy()
    {
        if (GameDayManager.Instance != null)
        {
            GameDayManager.Instance.DayEnded -= OnDayEnded;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public MarketingCampaignDefinition GetDefinition(MarketingCampaignType campaignType)
    {
        if (campaignDefinitions == null)
        {
            return null;
        }

        foreach (MarketingCampaignDefinition definition in campaignDefinitions)
        {
            if (definition != null && definition.CampaignType == campaignType)
            {
                return definition;
            }
        }

        return null;
    }

    public bool TryStartCampaign(MarketingCampaignType campaignType)
    {
        if (HasActiveCampaign)
        {
            lastStatusMessage = "Wait for the current campaign to finish.";
            StatusChanged?.Invoke();
            return false;
        }

        MarketingCampaignDefinition definition = GetDefinition(campaignType);
        if (definition == null || campaignType == MarketingCampaignType.None)
        {
            return false;
        }

        int activationCost = GetEffectiveActivationCost(campaignType);
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(activationCost, EconomyTransactionCategory.MarketingExpense))
        {
            lastStatusMessage = $"Campaign needs {activationCost} RUB.";
            StatusChanged?.Invoke();
            return false;
        }

        activeCampaign = campaignType;
        remainingDays = definition.DurationDays;
        lastStatusMessage = $"{definition.DisplayName} started for {remainingDays} day(s).";
        Debug.Log(lastStatusMessage);
        StatusChanged?.Invoke();
        return true;
    }

    public int GetEffectiveActivationCost(MarketingCampaignType campaignType)
    {
        MarketingCampaignDefinition definition = GetDefinition(campaignType);
        if (definition == null)
        {
            return 0;
        }

        float multiplier = ClubResearchManager.Instance != null
            ? ClubResearchManager.Instance.GetMarketingCostMultiplier()
            : 1f;
        return Mathf.RoundToInt(definition.ActivationCost * multiplier);
    }

    public float GetDemandMultiplier()
    {
        MarketingCampaignDefinition definition = HasActiveCampaign ? GetDefinition(activeCampaign) : null;
        return definition != null ? Mathf.Max(0.1f, definition.DemandMultiplier) : 1f;
    }

    public void GetClientWeights(float defaultRegularWeight, float defaultGamerWeight, float defaultVIPWeight, out float regularWeight, out float gamerWeight, out float vipWeight)
    {
        MarketingCampaignDefinition definition = HasActiveCampaign ? GetDefinition(activeCampaign) : null;
        if (definition == null)
        {
            regularWeight = defaultRegularWeight;
            gamerWeight = defaultGamerWeight;
            vipWeight = defaultVIPWeight;
            return;
        }

        regularWeight = defaultRegularWeight * definition.RegularWeight;
        gamerWeight = defaultGamerWeight * definition.GamerWeight;
        vipWeight = defaultVIPWeight * definition.VIPWeight;
    }

    public void RestoreState(MarketingCampaignType savedCampaign, int savedRemainingDays)
    {
        if (savedCampaign == MarketingCampaignType.None || savedRemainingDays <= 0 || GetDefinition(savedCampaign) == null)
        {
            activeCampaign = MarketingCampaignType.None;
            remainingDays = 0;
            lastStatusMessage = "No marketing campaign is active.";
        }
        else
        {
            activeCampaign = savedCampaign;
            remainingDays = Mathf.Max(1, savedRemainingDays);
            lastStatusMessage = $"{GetDefinition(activeCampaign).DisplayName}: {remainingDays} day(s) remaining.";
        }

        StatusChanged?.Invoke();
    }

    private void OnDayEnded(int _, int __, int ___, int ____)
    {
        if (!HasActiveCampaign)
        {
            return;
        }

        remainingDays--;
        MarketingCampaignDefinition definition = GetDefinition(activeCampaign);
        if (remainingDays > 0)
        {
            lastStatusMessage = $"{definition?.DisplayName}: {remainingDays} day(s) remaining.";
        }
        else
        {
            activeCampaign = MarketingCampaignType.None;
            remainingDays = 0;
            lastStatusMessage = $"{definition?.DisplayName}: campaign completed.";
        }

        StatusChanged?.Invoke();
    }

    private void EnsureDefaultDefinitions()
    {
        if (campaignDefinitions != null && campaignDefinitions.Length > 0)
        {
            return;
        }

        campaignDefinitions = new[]
        {
            new MarketingCampaignDefinition(MarketingCampaignType.SocialMedia, "Social media", 500, 2, 1.20f, 1f, 1f, 1f),
            new MarketingCampaignDefinition(MarketingCampaignType.GamerAdvertising, "Gamer advertising", 900, 2, 1.15f, 0.65f, 1.8f, 0.8f),
            new MarketingCampaignDefinition(MarketingCampaignType.VIPPromotion, "VIP promotion", 1500, 3, 1.10f, 0.55f, 1f, 2.4f),
            new MarketingCampaignDefinition(MarketingCampaignType.Tournament, "Weekend tournament", 2500, 1, 1.60f, 0.35f, 2.6f, 0.7f)
        };
    }
}
