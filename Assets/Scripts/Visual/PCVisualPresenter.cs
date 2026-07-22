using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(PC))]
public sealed class PCVisualPresenter : MonoBehaviour,
    IWorldInteractionVisual
{
    private static readonly Color FrameColor =
        new(0.035f, 0.055f, 0.075f, 1f);
    private static readonly Color DeskColor =
        new(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color DeskEdgeColor =
        new(0.055f, 0.075f, 0.095f, 1f);
    private static readonly Color ChairColor =
        new(0.055f, 0.075f, 0.105f, 1f);
    private static readonly Color SelectionColor =
        new(0.05f, 0.76f, 1f, 0.95f);

    private PC pc;
    private SpriteRenderer rootRenderer;
    private SpriteRenderer screen;
    private SpriteRenderer screenGlow;
    private SpriteRenderer tierAccent;
    private SpriteRenderer statusLight;
    private SpriteRenderer[] outlineParts;
    private WorldVisualPart[] visualParts;
    private bool isHovered;
    private bool isSelected;

    private void Awake()
    {
        pc = GetComponent<PC>();
        rootRenderer = GetComponent<SpriteRenderer>();
        BuildVisual();
    }

    private void Start()
    {
        pc.StateChanged += OnStateChanged;
        pc.TierChanged += OnTierChanged;
        pc.EquipmentChanged += RefreshVisual;
        RefreshVisual();
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

    private void OnDestroy()
    {
        if (pc == null)
        {
            return;
        }

        pc.StateChanged -= OnStateChanged;
        pc.TierChanged -= OnTierChanged;
        pc.EquipmentChanged -= RefreshVisual;
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

    private void OnStateChanged(PCState state)
    {
        RefreshVisual();
    }

    private void OnTierChanged(PCTier tier)
    {
        RefreshVisual();
    }

    private void BuildVisual()
    {
        Transform existing = transform.Find("PCVisual");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject visualRoot = new("PCVisual");
        visualRoot.transform.SetParent(transform, false);

        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shadow",
            new Vector2(0.04f, -0.08f),
            new Vector2(1.46f, 1.16f),
            new Color(0f, 0f, 0f, 0.42f),
            0
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Desk",
            new Vector2(0f, 0.08f),
            new Vector2(1.34f, 0.58f),
            DeskColor,
            2
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "DeskEdge",
            new Vector2(0f, -0.20f),
            new Vector2(1.34f, 0.09f),
            DeskEdgeColor,
            3
        );

        screenGlow = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ScreenGlow",
            new Vector2(-0.08f, 0.25f),
            new Vector2(0.68f, 0.46f),
            new Color(0.04f, 0.65f, 1f, 0.20f),
            4
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "MonitorFrame",
            new Vector2(-0.08f, 0.25f),
            new Vector2(0.62f, 0.40f),
            FrameColor,
            5
        );
        screen = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Screen",
            new Vector2(-0.08f, 0.27f),
            new Vector2(0.50f, 0.28f),
            Color.cyan,
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "MonitorStand",
            new Vector2(-0.08f, 0.02f),
            new Vector2(0.12f, 0.16f),
            FrameColor,
            5
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Tower",
            new Vector2(0.48f, 0.17f),
            new Vector2(0.24f, 0.46f),
            FrameColor,
            5
        );
        statusLight = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "StatusLight",
            new Vector2(0.48f, 0.31f),
            new Vector2(0.07f, 0.07f),
            Color.cyan,
            7
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Keyboard",
            new Vector2(-0.10f, -0.09f),
            new Vector2(0.54f, 0.12f),
            new Color(0.08f, 0.10f, 0.13f, 1f),
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Mouse",
            new Vector2(0.29f, -0.09f),
            new Vector2(0.09f, 0.13f),
            new Color(0.10f, 0.13f, 0.17f, 1f),
            6
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ChairSeat",
            new Vector2(0f, -0.43f),
            new Vector2(0.56f, 0.34f),
            ChairColor,
            8
        );
        WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "ChairBack",
            new Vector2(0f, -0.66f),
            new Vector2(0.64f, 0.17f),
            new Color(0.045f, 0.06f, 0.09f, 1f),
            9
        );
        tierAccent = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "TierAccent",
            new Vector2(0f, -0.245f),
            new Vector2(1.18f, 0.045f),
            SelectionColor,
            7
        );

        outlineParts = new[]
        {
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineTop",
                new Vector2(0f, 0.60f), new Vector2(1.48f, 0.045f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineBottom",
                new Vector2(0f, -0.78f), new Vector2(1.48f, 0.045f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineLeft",
                new Vector2(-0.72f, -0.09f), new Vector2(0.045f, 1.34f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineRight",
                new Vector2(0.72f, -0.09f), new Vector2(0.045f, 1.34f),
                SelectionColor, 12)
        };

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        RefreshOutline();
    }

    private void RefreshVisual()
    {
        if (pc == null || screen == null)
        {
            return;
        }

        Color stateColor = pc.State switch
        {
            PCState.Free => new Color(0.08f, 0.72f, 1f, 1f),
            PCState.Occupied => new Color(1f, 0.68f, 0.10f, 1f),
            PCState.Broken => new Color(1f, 0.20f, 0.18f, 1f),
            _ => Color.white
        };
        screen.color = stateColor;
        screenGlow.color = new Color(
            stateColor.r,
            stateColor.g,
            stateColor.b,
            0.23f
        );
        statusLight.color = pc.LowestEquipmentCondition < 35f && !pc.IsBroken
            ? new Color(1f, 0.35f, 0.08f, 1f)
            : stateColor;
        tierAccent.color = pc.Tier switch
        {
            PCTier.Basic => new Color(0.34f, 0.58f, 0.76f, 1f),
            PCTier.Gaming => new Color(0.08f, 0.88f, 0.88f, 1f),
            PCTier.Premium => new Color(0.76f, 0.20f, 1f, 1f),
            _ => SelectionColor
        };
    }

    private void RefreshOutline()
    {
        if (outlineParts == null)
        {
            return;
        }

        bool visible = isHovered || isSelected;
        Color color = isSelected
            ? SelectionColor
            : new Color(0.35f, 0.88f, 1f, 0.62f);

        foreach (SpriteRenderer outline in outlineParts)
        {
            if (outline != null)
            {
                outline.enabled = visible;
                outline.color = color;
            }
        }
    }
}
