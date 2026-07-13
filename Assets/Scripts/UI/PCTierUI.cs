using System.Collections.Generic;
using UnityEngine;

public sealed class PCTierUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 265f);

    private readonly List<PC> pcs = new();

    private int basicCount;
    private int gamingCount;
    private int premiumCount;

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
                pc.TierChanged -= OnTierChanged;
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
        pc.TierChanged += OnTierChanged;
        RecalculateCounts();
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.TierChanged -= OnTierChanged;
        pcs.Remove(pc);
        RecalculateCounts();
    }

    private void OnTierChanged(PCTier newTier)
    {
        RecalculateCounts();
    }

    private void RecalculateCounts()
    {
        basicCount = 0;
        gamingCount = 0;
        premiumCount = 0;

        foreach (PC pc in pcs)
        {
            if (pc == null)
            {
                continue;
            }

            switch (pc.Tier)
            {
                case PCTier.Basic:
                    basicCount++;
                    break;
                case PCTier.Gaming:
                    gamingCount++;
                    break;
                case PCTier.Premium:
                    premiumCount++;
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

        string tierText =
            $"ПК: базовые {basicCount} | " +
            $"игровые {gamingCount} | " +
            $"премиальные {premiumCount}";

        string upgradeText =
            $"Улучшение через E: {PC.BasicToGamingUpgradeCost} ₽ / " +
            $"{PC.GamingToPremiumUpgradeCost} ₽";

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y, 800f, 35f),
            tierText,
            labelStyle
        );

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y + 30f, 800f, 35f),
            upgradeText,
            labelStyle
        );
    }
}
