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
    private Transform visualRoot;
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
        if (visualRoot != null)
        {
            visualRoot.localScale = GetNormalizedVisualScale();
        }

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

        GameObject visualObject = new("TerminalVisual");
        visualObject.transform.SetParent(transform, false);
        visualRoot = visualObject.transform;
        visualRoot.localScale = GetNormalizedVisualScale();

        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "Shadow",
            new Vector2(0.035f, -0.035f),
            new Vector2(0.72f, 0.90f),
            new Color(0f, 0f, 0f, 0.38f),
            0
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "Body",
            new Vector2(0f, -0.01f),
            new Vector2(0.58f, 0.76f),
            new Color(0.045f, 0.055f, 0.07f, 1f),
            2
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "ScreenFrame",
            new Vector2(0f, 0.14f),
            new Vector2(0.50f, 0.34f),
            new Color(0.015f, 0.025f, 0.04f, 1f),
            4
        );
        screen = WorldVisualPrimitives.CreatePart(
            visualRoot,
            "Screen",
            new Vector2(0f, 0.15f),
            new Vector2(0.42f, 0.23f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.48f),
            5
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "ScreenLineA",
            new Vector2(-0.055f, 0.18f),
            new Vector2(0.20f, 0.025f),
            new Color(1f, 1f, 1f, 0.48f),
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "ScreenLineB",
            new Vector2(0.04f, 0.11f),
            new Vector2(0.23f, 0.025f),
            new Color(1f, 1f, 1f, 0.34f),
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "ControlPanel",
            new Vector2(0f, -0.13f),
            new Vector2(0.42f, 0.11f),
            new Color(0.08f, 0.11f, 0.15f, 1f),
            4
        );
        accentStrip = WorldVisualPrimitives.CreatePart(
            visualRoot,
            "AccentStrip",
            new Vector2(0f, -0.29f),
            new Vector2(0.40f, 0.04f),
            accentColor,
            5
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot,
            "Base",
            new Vector2(0f, -0.38f),
            new Vector2(0.66f, 0.12f),
            new Color(0.025f, 0.035f, 0.055f, 1f),
            3
        );

        outlineParts = new[]
        {
            WorldVisualPrimitives.CreatePart(
                visualRoot, "OutlineTop",
                new Vector2(0f, 0.40f), new Vector2(0.68f, 0.035f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot, "OutlineBottom",
                new Vector2(0f, -0.46f), new Vector2(0.68f, 0.035f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot, "OutlineLeft",
                new Vector2(-0.33f, -0.03f), new Vector2(0.035f, 0.82f),
                accentColor, 10),
            WorldVisualPrimitives.CreatePart(
                visualRoot, "OutlineRight",
                new Vector2(0.33f, -0.03f), new Vector2(0.035f, 0.82f),
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
            return new Color(0.20f, 0.55f, 0.75f, 0.88f);
        }
        if (GetComponent<PCMaintenanceTerminal>() != null)
        {
            return new Color(0.22f, 0.60f, 0.43f, 0.88f);
        }
        if (GetComponent<PricingTerminal>() != null)
        {
            return new Color(0.48f, 0.34f, 0.68f, 0.88f);
        }
        if (GetComponent<ConsumableStockTerminal>() != null)
        {
            return new Color(0.64f, 0.42f, 0.22f, 0.88f);
        }
        if (GetComponent<MarketingTerminal>() != null)
        {
            return new Color(0.67f, 0.57f, 0.22f, 0.88f);
        }
        if (GetComponent<InternetProviderTerminal>() != null)
        {
            return new Color(0.24f, 0.58f, 0.60f, 0.88f);
        }
        if (GetComponent<ClubResearchTerminal>() != null)
        {
            return new Color(0.62f, 0.32f, 0.58f, 0.88f);
        }

        return new Color(0.32f, 0.52f, 0.66f, 0.88f);
    }

    private Vector3 GetNormalizedVisualScale()
    {
        Vector3 worldScale = transform.lossyScale;
        return new Vector3(
            0.78f / Mathf.Max(0.01f, Mathf.Abs(worldScale.x)),
            0.82f / Mathf.Max(0.01f, Mathf.Abs(worldScale.y)),
            1f
        );
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
            float alpha = isSelected ? 0.90f : isHovered ? 0.70f : 0.48f;
            screen.color = new Color(
                accentColor.r,
                accentColor.g,
                accentColor.b,
                alpha
            );
        }
        if (accentStrip != null)
        {
            accentStrip.color = isSelected
                ? new Color(0.82f, 0.90f, 0.96f, 0.92f)
                : accentColor;
        }
    }
}
