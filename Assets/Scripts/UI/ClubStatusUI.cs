using System.Collections.Generic;
using UnityEngine;

public sealed class ClubStatusUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 55f);

    private readonly List<PC> pcs = new();

    private int freeCount;
    private int occupiedCount;
    private int brokenCount;

    private GUIStyle labelStyle;

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;

        PC[] existingPCs = FindObjectsByType<PC>();
        foreach (PC pc in existingPCs)
        {
            RegisterPC(pc);
        }

        RecalculateCounts();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;

        foreach (PC pc in pcs)
        {
            if (pc != null)
            {
                pc.StateChanged -= OnPCStateChanged;
            }
        }
    }

    private void RegisterPC(PC pc)
    {
        if (pc == null || pcs.Contains(pc))
        {
            return;
        }

        pcs.Add(pc);
        pc.StateChanged += OnPCStateChanged;
        RecalculateCounts();
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.StateChanged -= OnPCStateChanged;
        pcs.Remove(pc);
        RecalculateCounts();
    }

    private void OnPCStateChanged(PCState newState)
    {
        RecalculateCounts();
    }

    private void RecalculateCounts()
    {
        freeCount = 0;
        occupiedCount = 0;
        brokenCount = 0;

        foreach (PC pc in pcs)
        {
            if (pc == null)
            {
                continue;
            }

            switch (pc.State)
            {
                case PCState.Free:
                    freeCount++;
                    break;

                case PCState.Occupied:
                    occupiedCount++;
                    break;

                case PCState.Broken:
                    brokenCount++;
                    break;
            }
        }
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize
            };
        }

        string statusText =
            $"Свободно: {freeCount} | " +
            $"Занято: {occupiedCount} | " +
            $"Сломано: {brokenCount}";

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y, 600f, 40f),
            statusText,
            labelStyle
        );
    }
}
