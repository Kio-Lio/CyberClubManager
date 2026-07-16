using System;
using UnityEngine;

[Serializable]
public sealed class TutorialStepDefinition
{
    [SerializeField] private TutorialStepType stepType;
    [SerializeField] private string title;
    [SerializeField, TextArea] private string description;
    [SerializeField] private string objectiveText;
    [SerializeField, Min(1)] private int requiredProgress = 1;

    public TutorialStepType StepType => stepType;
    public string Title => title;
    public string Description => description;
    public string ObjectiveText => objectiveText;
    public int RequiredProgress => requiredProgress;

    public TutorialStepDefinition(TutorialStepType stepType, string title,
        string description, string objectiveText, int requiredProgress = 1)
    {
        this.stepType = stepType;
        this.title = title;
        this.description = description;
        this.objectiveText = objectiveText;
        this.requiredProgress = Mathf.Max(1, requiredProgress);
    }
}
