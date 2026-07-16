using System;
using UnityEngine;

[Serializable]
public sealed class ClubResearchDefinition
{
    [SerializeField] private ClubResearchType researchType;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField, Min(1)] private int maximumLevel = 3;
    [SerializeField, Min(0)] private int baseCost;

    public ClubResearchType ResearchType => researchType;
    public string DisplayName => displayName;
    public string Description => description;
    public int MaximumLevel => maximumLevel;
    public int BaseCost => baseCost;

    public ClubResearchDefinition(ClubResearchType researchType, string displayName,
        string description, int maximumLevel, int baseCost)
    {
        this.researchType = researchType;
        this.displayName = displayName;
        this.description = description;
        this.maximumLevel = maximumLevel;
        this.baseCost = baseCost;
    }
}
