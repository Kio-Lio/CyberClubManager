using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class TerminalVisualPresenter : MonoBehaviour,
    IWorldInteractionVisual
{
    private SpriteRenderer rootRenderer;
    private SpriteRenderer screen;
    private SpriteRenderer accentStrip;
    private SpriteRenderer[] outlineParts;
    private WorldVisualPart[] visualParts;
    private Color accentColor;
    private bool isHovered;
    private bool isSelected;

    private void Awake()
    {
        rootRenderer = GetComponent<SpriteRenderer>();
        accentColor = ResolveAccentColor();
        BuildVisual();
    }

    private void LateUpdate()
    {
        if (rootRenderer == null || visualParts == null)
        {
            return;
        }

        int baseOrder = rootRenderer.sortingOrder;
        foreach (WorldVisualPart part in visualParts)
        {
            if (part != null &&
                part.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.sortingOrder = baseOrder + part.OrderOffset;
            }
        }
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        RefreshOutline();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshOutline();
    }

    private void BuildVisual()
    {
        Transform existing = transform.Find("TerminalVisual");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject visualRoot = new("TerminalVisual");
        visualRoot.transform.SetParent(transform, false);

        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shadow",
            new Vector2(0.04f, -0.06f),
            new Vector2(0.94f, 1.18f),
            new Color(0f, 0f, 0f, 0.45f),
            0
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Body",
            new Vector2(0f, -0.02f),
            new Vector2(0.76f, 1.02f),
            new Color(0.035f, 0.055f, 0.08f, 1f),
            2
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ScreenFrame",
            new Vector2(0f, 0.20f),
            new Vector2(0.64f, 0.48f),
            new Color(0.015f, 0.025f, 0.04f, 1f),
            4
        );
        screen = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Screen",
            new Vector2(0f, 0.22f),
            new Vector2(0.52f, 0.34f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.70f),
            5
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ScreenLineA",
            new Vector2(-0.08f, 0.27f),
            new Vector2(0.26f, 0.045f),
            Color.white,
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ScreenLineB",
            new Vector2(0.05f, 0.17f),
            new Vector2(0.30f, 0.045f),
            new Color(1f, 1f, 1f, 0.72f),
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ControlPanel",
            new Vector2(0f, -0.18f),
            new Vector2(0.58f, 0.16f),
            new Color(0.08f, 0.11f, 0.15f, 1f),
            4
        );
        accentStrip = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "AccentStrip",
            new Vector2(0f, -0.42f),
            new Vector2(0.66f, 0.08f),
            accentColor,
            5
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Base",
            new Vector2(0f, -0.56f),
            new Vector2(0.92f, 0.18f),
            new Color(0.025f, 0.035f, 0.055f, 1f),
            3
        );

        outlineParts = new[]
        {
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineTop",
                new Vector2(0f, 0.56f), new Vector2(0.90f, 0.045f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineBottom",
                new Vector2(0f, -0.68f), new Vector2(0.90f, 0.045f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineLeft",
                new Vector2(-0.43f, -0.06f), new Vector2(0.045f, 1.20f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineRight",
                new Vector2(0.43f, -0.06f), new Vector2(0.045f, 1.20f),
                accentColor, 10)
        };

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        RefreshOutline();
    }

    private Color ResolveAccentColor()
    {
        if (GetComponent<PCExpansionTerminal>() != null)
        {
            return new Color(0.05f, 0.72f, 1f, 1f);
        }
        if (GetComponent<PCMaintenanceTerminal>() != null)
        {
            return new Color(0.12f, 0.92f, 0.52f, 1f);
        }
        if (GetComponent<PricingTerminal>() != null)
        {
            return new Color(0.68f, 0.28f, 1f, 1f);
        }
        if (GetComponent<ConsumableStockTerminal>() != null)
        {
            return new Color(1f, 0.48f, 0.10f, 1f);
        }
        if (GetComponent<MarketingTerminal>() != null)
        {
            return new Color(1f, 0.80f, 0.10f, 1f);
        }
        if (GetComponent<InternetProviderTerminal>() != null)
        {
            return new Color(0.08f, 0.84f, 0.86f, 1f);
        }
        if (GetComponent<ClubResearchTerminal>() != null)
        {
            return new Color(0.92f, 0.22f, 0.78f, 1f);
        }

        return new Color(0.30f, 0.68f, 1f, 1f);
    }

    private void RefreshOutline()
    {
        if (outlineParts == null)
        {
            return;
        }

        bool visible = isHovered || isSelected;
        Color color = isSelected
            ? Color.white
            : new Color(accentColor.r, accentColor.g, accentColor.b, 0.68f);

        foreach (SpriteRenderer outline in outlineParts)
        {
            if (outline != null)
            {
                outline.enabled = visible;
                outline.color = color;
            }
        }

        if (screen != null)
        {
            float alpha = isSelected ? 1f : isHovered ? 0.88f : 0.70f;
            screen.color = new Color(
                accentColor.r,
                accentColor.g,
                accentColor.b,
                alpha
            );
        }
        if (accentStrip != null)
        {
            accentStrip.color = isSelected ? Color.white : accentColor;
        }
    }
}
