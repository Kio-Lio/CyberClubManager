using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(PC))]
public sealed class PCVisualPresenter : MonoBehaviour,
    IWorldInteractionVisual
{
    private const float WorkstationPixelsPerUnit = 800f;

    private static readonly Color SelectionColor =
        new(0.05f, 0.76f, 1f, 0.95f);

    private PC pc;
    private SpriteRenderer rootRenderer;
    private SpriteRenderer workstationRenderer;
    private SpriteRenderer tierAccent;
    private SpriteRenderer statusLight;
    private SpriteRenderer[] outlineParts;
    private WorldVisualPart[] visualParts;
    private Sprite workstationSprite;
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

        if (workstationSprite != null)
        {
            Destroy(workstationSprite);
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
            new Vector2(0f, -0.77f),
            new Vector2(1.18f, 0.045f),
            SelectionColor,
            7
        );
        statusLight = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "StatusLight",
            new Vector2(0.62f, 0.58f),
            new Vector2(0.10f, 0.10f),
            Color.cyan,
            8
        );

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

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
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

        if (workstationSprite != null)
        {
            Destroy(workstationSprite);
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;
        workstationSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            WorkstationPixelsPerUnit,
            0,
            SpriteMeshType.FullRect
        );
        workstationSprite.name = $"{pc.Tier}WorkstationSprite";
        workstationRenderer.sprite = workstationSprite;

        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }
    }

    private void RefreshVisual()
    {
        if (pc == null || workstationRenderer == null)
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
        workstationRenderer.color = pc.State switch
        {
            PCState.Free => Color.white,
            PCState.Occupied => new Color(1f, 0.88f, 0.64f, 1f),
            PCState.Broken => new Color(0.62f, 0.34f, 0.34f, 1f),
            _ => Color.white
        };
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
