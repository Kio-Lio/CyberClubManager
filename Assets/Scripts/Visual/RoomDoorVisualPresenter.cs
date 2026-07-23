using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(RoomDoor))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RoomDoorVisualPresenter : MonoBehaviour
{
    private RoomDoor door;
    private SpriteRenderer rootRenderer;
    private SpriteRenderer frame;
    private SpriteRenderer panel;
    private SpriteRenderer lockIndicator;
    private SpriteRenderer threshold;
    private WorldVisualPart[] visualParts;

    private void Awake()
    {
        door = GetComponent<RoomDoor>();
        rootRenderer = GetComponent<SpriteRenderer>();
        BuildVisual();
        RefreshState();
    }

    private void Start()
    {
        door.StatusChanged += RefreshState;
    }

    private void OnDestroy()
    {
        if (door != null)
        {
            door.StatusChanged -= RefreshState;
        }
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

    public void RefreshState()
    {
        if (door == null || frame == null || panel == null ||
            lockIndicator == null ||
            threshold == null)
        {
            return;
        }

        frame.enabled = !door.IsUnlocked;
        panel.enabled = !door.IsUnlocked;
        lockIndicator.enabled = !door.IsUnlocked;
        threshold.color = door.IsUnlocked
            ? new Color(0.20f, 0.34f, 0.32f, 0.82f)
            : new Color(0.30f, 0.27f, 0.23f, 0.90f);
        rootRenderer.enabled = false;
    }

    private void BuildVisual()
    {
        Transform existing = transform.Find("DoorVisual");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject visualRoot = new("DoorVisual");
        visualRoot.transform.SetParent(transform, false);

        frame = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Frame",
            Vector2.zero,
            new Vector2(1f, 1f),
            new Color(0.08f, 0.09f, 0.11f, 1f),
            1
        );
        panel = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "DoorPanel",
            Vector2.zero,
            new Vector2(0.72f, 0.86f),
            new Color(0.14f, 0.15f, 0.18f, 1f),
            2
        );
        threshold = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Threshold",
            new Vector2(0f, -0.38f),
            new Vector2(0.84f, 0.12f),
            new Color(0.30f, 0.27f, 0.23f, 0.90f),
            3
        );
        lockIndicator = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "LockIndicator",
            new Vector2(0f, 0.08f),
            new Vector2(0.24f, 0.10f),
            new Color(0.60f, 0.42f, 0.18f, 0.88f),
            4
        );

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        rootRenderer.enabled = false;
    }
}
