using UnityEngine;

[DefaultExecutionOrder(210)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PCExpansionSlotVisualPresenter : MonoBehaviour
{
    private int slotIndex;
    private string pcName;
    private SpriteRenderer basePanel;
    private SpriteRenderer cablePoint;
    private SpriteRenderer[] mounts;

    public int SlotIndex => slotIndex;
    public string PCName => pcName;
    public bool IsOccupied { get; private set; }
    public bool IsUnlocked { get; private set; }

    private void Awake()
    {
        BuildVisual();
    }

    private void Start()
    {
        Subscribe();
        Refresh();
        Invoke(nameof(Refresh), 0f);
    }

    private void OnDestroy()
    {
        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= Refresh;
        }

        if (ManagerBuildController.Instance != null)
        {
            ManagerBuildController.Instance.StateChanged -= Refresh;
        }
    }

    public void Configure(int index, string targetPCName)
    {
        slotIndex = Mathf.Max(0, index);
        pcName = targetPCName;
        transform.localScale = new Vector3(0.95f, 0.78f, 1f);
        ApplySortingOrders();
        Refresh();
    }

    public void Refresh()
    {
        if (basePanel == null || mounts == null)
        {
            return;
        }

        PCExpansionManager expansion = PCExpansionManager.Instance;
        IsOccupied = !string.IsNullOrEmpty(pcName) &&
            GameObject.Find(pcName) != null;
        IsUnlocked = expansion == null ||
            slotIndex < expansion.UnlockedSlotCount;

        bool buildMode = ManagerBuildController.Instance != null &&
            ManagerBuildController.Instance.IsPlacing;
        bool visible = !IsOccupied;
        float alpha = buildMode
            ? (IsUnlocked ? 0.64f : 0.38f)
            : (IsUnlocked ? 0.34f : 0.22f);

        basePanel.enabled = visible;
        basePanel.color = IsUnlocked
            ? new Color(0.075f, 0.115f, 0.135f, alpha)
            : new Color(0.055f, 0.06f, 0.072f, alpha);

        Color detailColor = IsUnlocked
            ? new Color(0.18f, 0.34f, 0.39f, alpha + 0.08f)
            : new Color(0.12f, 0.13f, 0.15f, alpha);
        foreach (SpriteRenderer mount in mounts)
        {
            mount.enabled = visible;
            mount.color = detailColor;
        }

        cablePoint.enabled = visible;
        cablePoint.color = IsUnlocked
            ? new Color(0.12f, 0.42f, 0.50f, alpha + 0.08f)
            : detailColor;
    }

    private void Subscribe()
    {
        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= Refresh;
            PCExpansionManager.Instance.StatusChanged += Refresh;
        }

        if (ManagerBuildController.Instance != null)
        {
            ManagerBuildController.Instance.StateChanged -= Refresh;
            ManagerBuildController.Instance.StateChanged += Refresh;
        }
    }

    private void BuildVisual()
    {
        basePanel = GetComponent<SpriteRenderer>();
        basePanel.sprite = WorldVisualPrimitives.SquareSprite;
        YSortRenderer.SetSortingLayer(basePanel, "World");
        int baseOrder = Mathf.RoundToInt(-transform.position.y * 100f) + 60;
        basePanel.sortingOrder = baseOrder;
        transform.localScale = Vector3.one;

        Transform existing = transform.Find("SlotDetails");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject details = new("SlotDetails");
        details.transform.SetParent(transform, false);
        mounts = new SpriteRenderer[4];
        Vector2[] mountPositions =
        {
            new(-0.39f, -0.30f),
            new(0.39f, -0.30f),
            new(-0.39f, 0.30f),
            new(0.39f, 0.30f)
        };

        for (int index = 0; index < mountPositions.Length; index++)
        {
            mounts[index] = WorldVisualPrimitives.CreatePart(
                details.transform,
                $"Mount_{index + 1:00}",
                mountPositions[index],
                new Vector2(0.08f, 0.08f),
                Color.white,
                0
            );
            mounts[index].sprite = WorldVisualPrimitives.CircleSprite;
            YSortRenderer.SetSortingLayer(mounts[index], "World");
            mounts[index].sortingOrder = baseOrder + 1;
        }

        cablePoint = WorldVisualPrimitives.CreatePart(
            details.transform,
            "CablePoint",
            new Vector2(0f, 0.18f),
            new Vector2(0.12f, 0.055f),
            Color.white,
            0
        );
        YSortRenderer.SetSortingLayer(cablePoint, "World");
        cablePoint.sortingOrder = baseOrder + 2;

        transform.localScale = new Vector3(0.95f, 0.78f, 1f);
    }

    private void ApplySortingOrders()
    {
        if (basePanel == null || mounts == null || cablePoint == null)
        {
            return;
        }

        int baseOrder = Mathf.RoundToInt(-transform.position.y * 100f) + 60;
        basePanel.sortingOrder = baseOrder;
        foreach (SpriteRenderer mount in mounts)
        {
            mount.sortingOrder = baseOrder + 1;
        }
        cablePoint.sortingOrder = baseOrder + 2;
    }
}
