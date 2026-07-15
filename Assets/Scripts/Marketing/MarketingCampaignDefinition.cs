using System;
using UnityEngine;

[Serializable]
public sealed class MarketingCampaignDefinition
{
    [SerializeField] private MarketingCampaignType campaignType;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int activationCost;
    [SerializeField, Min(1)] private int durationDays = 1;
    [SerializeField, Min(0.1f)] private float demandMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float regularWeight = 1f;
    [SerializeField, Range(0f, 3f)] private float gamerWeight;
    [SerializeField, Range(0f, 3f)] private float vipWeight;

    public MarketingCampaignType CampaignType => campaignType;
    public string DisplayName => displayName;
    public int ActivationCost => activationCost;
    public int DurationDays => durationDays;
    public float DemandMultiplier => demandMultiplier;
    public float RegularWeight => regularWeight;
    public float GamerWeight => gamerWeight;
    public float VIPWeight => vipWeight;

    public MarketingCampaignDefinition(MarketingCampaignType campaignType, string displayName, int activationCost, int durationDays, float demandMultiplier, float regularWeight, float gamerWeight, float vipWeight)
    {
        this.campaignType = campaignType;
        this.displayName = displayName;
        this.activationCost = activationCost;
        this.durationDays = durationDays;
        this.demandMultiplier = demandMultiplier;
        this.regularWeight = regularWeight;
        this.gamerWeight = gamerWeight;
        this.vipWeight = vipWeight;
    }
}
