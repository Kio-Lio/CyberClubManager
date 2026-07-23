using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(PC))]
public sealed class PCVisualPresenter : MonoBehaviour,
    IWorldInteractionVisual
{
    private const float TargetWorkstationWidth = 1.50f;
    private const float OutlineMargin = 0.045f;

    private static readonly Color SelectionColor =
        new(0.05f, 0.76f, 1f, 0.95f);

    private PC pc;
    private SpriteRenderer rootRenderer;
    private SpriteRenderer workstationRenderer;
    private SpriteRenderer tierAccent;
    private SpriteRenderer statusLight;
    private SpriteRenderer[] outlineParts;
    private WorldVisualPart[] visualParts;
    private bool isHovered;
    private bool isSelected;

    public SpriteRenderer WorkstationRenderer => workstationRenderer;

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
        RefreshTierSprite();
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
        if (pc != null)
        {
            pc.StateChanged -= OnStateChanged;
            pc.TierChanged -= OnTierChanged;
            pc.EquipmentChanged -= RefreshVisual;
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

    private void OnStateChanged(PCState state)
    {
        RefreshVisual();
    }

    private void OnTierChanged(PCTier tier)
    {
        RefreshTierSprite();
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

        GameObject workstationObject = new("WorkstationSprite");
        workstationObject.transform.SetParent(visualRoot.transform, false);
        workstationObject.transform.localPosition =
            new Vector3(0f, -0.08f, 0f);

        workstationRenderer =
            workstationObject.AddComponent<SpriteRenderer>();
        YSortRenderer.SetSortingLayer(workstationRenderer, "World");
        WorldVisualPart workstationPart =
            workstationObject.AddComponent<WorldVisualPart>();
        workstationPart.OrderOffset = 4;

        tierAccent = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "TierAccent",
            new Vector2(0f, -0.765f),
            new Vector2(0.72f, 0.026f),
            SelectionColor,
            7
        );
        statusLight = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "StatusLight",
            new Vector2(0.62f, 0.56f),
            new Vector2(0.07f, 0.07f),
            Color.cyan,
            8
        );
        statusLight.sprite = WorldVisualPrimitives.CircleSprite;

        outlineParts = new[]
        {
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineTop",
                new Vector2(0f, 0.69f), new Vector2(1.52f, 0.045f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineBottom",
                new Vector2(0f, -0.85f), new Vector2(1.52f, 0.045f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineLeft",
                new Vector2(-0.75f, -0.08f), new Vector2(0.045f, 1.50f),
                SelectionColor, 12),
            WorldVisualPrimitives.CreatePart(
                visualRoot.transform, "OutlineRight",
                new Vector2(0.75f, -0.08f), new Vector2(0.045f, 1.50f),
                SelectionColor, 12)
        };

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        RefreshTierSprite();
        RefreshOutline();
    }

    private void RefreshTierSprite()
    {
        if (workstationRenderer == null || pc == null)
        {
            return;
        }

        string resourcePath = pc.Tier switch
        {
            PCTier.Basic => "PC/Workstations/Basic",
            PCTier.Gaming => "PC/Workstations/Gaming",
            PCTier.Premium => "PC/Workstations/Premium",
            _ => "PC/Workstations/Basic"
        };

        Sprite workstation = Resources.Load<Sprite>(resourcePath);
        if (workstation == null)
        {
            Debug.LogWarning(
                $"Workstation sprite was not found: {resourcePath}."
            );
            workstationRenderer.sprite = null;
            if (rootRenderer != null)
            {
                rootRenderer.enabled = true;
            }
            return;
        }

        workstationRenderer.sprite = workstation;
        float scale = TargetWorkstationWidth /
            Mathf.Max(0.01f, workstation.bounds.size.x);
        workstationRenderer.transform.localScale =
            new Vector3(scale, scale, 1f);
        RefreshVisualGeometry(
            TargetWorkstationWidth,
            workstation.bounds.size.y * scale
        );

        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }
    }

    private void RefreshVisualGeometry(float width, float height)
    {
        if (outlineParts == null || outlineParts.Length != 4)
        {
            return;
        }

        float halfWidth = width * 0.5f + OutlineMargin;
        float halfHeight = height * 0.5f + OutlineMargin;
        float lineThickness = 0.045f;

        ConfigureOutline(
            outlineParts[0],
            new Vector2(0f, halfHeight),
            new Vector2(halfWidth * 2f, lineThickness)
        );
        ConfigureOutline(
            outlineParts[1],
            new Vector2(0f, -halfHeight),
            new Vector2(halfWidth * 2f, lineThickness)
        );
        ConfigureOutline(
            outlineParts[2],
            new Vector2(-halfWidth, 0f),
            new Vector2(lineThickness, halfHeight * 2f)
        );
        ConfigureOutline(
            outlineParts[3],
            new Vector2(halfWidth, 0f),
            new Vector2(lineThickness, halfHeight * 2f)
        );

        tierAccent.transform.localPosition =
            new Vector3(0f, -halfHeight - 0.03f, 0f);
        tierAccent.transform.localScale =
            new Vector3(width * 0.48f, 0.026f, 1f);
        statusLight.transform.localPosition = new Vector3(
            width * 0.5f - 0.07f,
            height * 0.5f - 0.07f,
            0f
        );
    }

    private static void ConfigureOutline(
        SpriteRenderer renderer,
        Vector2 localPosition,
        Vector2 localScale)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = localPosition;
        renderer.transform.localScale =
            new Vector3(localScale.x, localScale.y, 1f);
    }

    private void RefreshVisual()
    {
        if (pc == null || workstationRenderer == null)
        {
            return;
        }

        Color stateColor = pc.State switch
        {
            PCState.Free => new Color(0.18f, 0.58f, 0.72f, 0.86f),
            PCState.Occupied => new Color(0.76f, 0.55f, 0.20f, 0.90f),
            PCState.Broken => new Color(0.72f, 0.24f, 0.22f, 0.92f),
            _ => Color.white
        };
        workstationRenderer.color = pc.State switch
        {
            PCState.Free => Color.white,
            PCState.Occupied => new Color(1f, 0.88f, 0.64f, 1f),
            PCState.Broken => new Color(0.62f, 0.34f, 0.34f, 1f),
            _ => Color.white
        };
        statusLight.color = pc.LowestEquipmentCondition < 35f && !pc.IsBroken
            ? new Color(0.78f, 0.34f, 0.14f, 0.92f)
            : stateColor;
        tierAccent.color = pc.Tier switch
        {
            PCTier.Basic => new Color(0.30f, 0.46f, 0.60f, 0.72f),
            PCTier.Gaming => new Color(0.16f, 0.60f, 0.62f, 0.76f),
            PCTier.Premium => new Color(0.55f, 0.30f, 0.68f, 0.78f),
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
