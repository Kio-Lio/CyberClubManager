using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    private readonly List<MonoBehaviour> candidates = new();

    private MonoBehaviour currentBehaviour;
    private string currentPrompt = string.Empty;

    public string CurrentPrompt => currentPrompt;

    public event Action<string> PromptChanged;

    private void Update()
    {
        RefreshCurrentTarget();
    }

    public void OnInteract(InputValue inputValue)
    {
        if (!inputValue.isPressed)
        {
            return;
        }

        if (GameplayInputState.IsBlocked)
        {
            return;
        }

        if (currentBehaviour == null)
        {
            return;
        }

        if (currentBehaviour is not IInteractable interactable)
        {
            return;
        }

        interactable.Interact();
        RefreshPrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        MonoBehaviour interactableBehaviour = FindInteractableBehaviour(other);

        if (interactableBehaviour == null)
        {
            return;
        }

        if (!candidates.Contains(interactableBehaviour))
        {
            candidates.Add(interactableBehaviour);
        }

        RefreshCurrentTarget();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        MonoBehaviour interactableBehaviour = FindInteractableBehaviour(other);

        if (interactableBehaviour == null)
        {
            return;
        }

        candidates.Remove(interactableBehaviour);
        RefreshCurrentTarget();
    }

    private void OnDisable()
    {
        candidates.Clear();
        currentBehaviour = null;
        SetPrompt(string.Empty);
    }

    private void RefreshCurrentTarget()
    {
        candidates.RemoveAll(
            candidate =>
                candidate == null ||
                !candidate.isActiveAndEnabled
        );

        MonoBehaviour nearestCandidate = null;
        float nearestDistanceSquared = float.MaxValue;

        foreach (MonoBehaviour candidate in candidates)
        {
            float distanceSquared =
                (candidate.transform.position - transform.position).sqrMagnitude;

            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestCandidate = candidate;
        }

        bool targetChanged = currentBehaviour != nearestCandidate;
        currentBehaviour = nearestCandidate;
        if (targetChanged && currentBehaviour is PC)
        {
            FirstDayTutorialManager.Instance?.ReportAction(
                TutorialStepType.ApproachPC
            );
        }
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        if (currentBehaviour is IInteractable interactable)
        {
            SetPrompt(interactable.GetInteractionPrompt());
            return;
        }

        SetPrompt(string.Empty);
    }

    private void SetPrompt(string newPrompt)
    {
        newPrompt ??= string.Empty;

        if (currentPrompt == newPrompt)
        {
            return;
        }

        currentPrompt = newPrompt;
        PromptChanged?.Invoke(currentPrompt);
    }

    private static MonoBehaviour FindInteractableBehaviour(Collider2D other)
    {
        MonoBehaviour[] behaviours =
            other.GetComponentsInParent<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable)
            {
                return behaviour;
            }
        }

        return null;
    }
}
