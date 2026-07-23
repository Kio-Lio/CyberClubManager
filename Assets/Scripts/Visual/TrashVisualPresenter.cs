using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TrashVisualPresenter : MonoBehaviour
{
    private SpriteRenderer rootRenderer;
    private WorldVisualPart[] visualParts;

    private void Awake()
    {
        rootRenderer = GetComponent<SpriteRenderer>();
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

    private void BuildVisual()
    {
        Transform existing = transform.Find("TrashVisual");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject visualRoot = new("TrashVisual");
        visualRoot.transform.SetParent(transform, false);

        SpriteRenderer shadow = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "Shadow",
            new Vector2(0.01f, -0.025f),
            new Vector2(0.30f, 0.14f),
            new Color(0f, 0f, 0f, 0.25f),
            0
        );
        shadow.sprite = WorldVisualPrimitives.CircleSprite;

        SpriteRenderer firstPiece = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "LitterA",
            new Vector2(-0.045f, 0.01f),
            new Vector2(0.16f, 0.055f),
            new Color(0.28f, 0.25f, 0.21f, 1f),
            2
        );
        firstPiece.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);

        SpriteRenderer secondPiece = WorldVisualPrimitives.CreatePart(
            visualRoot.transform,
            "LitterB",
            new Vector2(0.075f, -0.015f),
            new Vector2(0.12f, 0.045f),
            new Color(0.20f, 0.22f, 0.24f, 1f),
            3
        );
        secondPiece.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);

        visualParts = visualRoot.GetComponentsInChildren<WorldVisualPart>(true);
        rootRenderer.enabled = false;
    }
}
