using System;
using UnityEngine;

[Serializable]
public sealed class InternetPlanDefinition
{
    [SerializeField] private InternetPlanType planType;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int connectionCost;
    [SerializeField, Min(0)] private int dailyCost;
    [SerializeField, Min(0.1f)] private float sessionSpeedMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float reliability = 1f;

    public InternetPlanType PlanType => planType;
    public string DisplayName => displayName;
    public int ConnectionCost => connectionCost;
    public int DailyCost => dailyCost;
    public float SessionSpeedMultiplier => sessionSpeedMultiplier;
    public float Reliability => reliability;

    public InternetPlanDefinition(
        InternetPlanType planType,
        string displayName,
        int connectionCost,
        int dailyCost,
        float sessionSpeedMultiplier,
        float reliability)
    {
        this.planType = planType;
        this.displayName = displayName;
        this.connectionCost = connectionCost;
        this.dailyCost = dailyCost;
        this.sessionSpeedMultiplier = sessionSpeedMultiplier;
        this.reliability = reliability;
    }
}
