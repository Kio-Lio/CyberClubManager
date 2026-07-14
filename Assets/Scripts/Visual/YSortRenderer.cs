using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class YSortRenderer : MonoBehaviour
{
    [Header("Sorting")]
    [SerializeField] private int sortingOffset;
    [SerializeField, Min(1f)] private float sortingPrecision = 100f;
    [SerializeField] private Transform sortingPoint;
    [SerializeField] private bool updateEveryFrame = true;

    private SpriteRenderer spriteRenderer;
    private int lastSortingOrder = int.MinValue;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        RefreshSortingOrder();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
        {
            RefreshSortingOrder();
        }
    }

    public static YSortRenderer Ensure(
        GameObject target,
        int offset,
        float sortingPointLocalY)
    {
        if (target == null)
        {
            return null;
        }

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return null;
        }

        SetSortingLayer(renderer, "World");

        YSortRenderer ySort = target.GetComponent<YSortRenderer>() ??
            target.AddComponent<YSortRenderer>();

        Transform sortingPoint = target.transform.Find("SortingPoint");
        if (sortingPoint == null)
        {
            GameObject pointObject = new GameObject("SortingPoint");
            sortingPoint = pointObject.transform;
            sortingPoint.SetParent(target.transform, false);
        }

        sortingPoint.localPosition = new Vector3(
            0f,
            sortingPointLocalY,
            0f
        );

        ySort.SetSortingPoint(sortingPoint);
        ySort.SetSortingOffset(offset);
        return ySort;
    }

    public static void SetSortingLayer(
        SpriteRenderer renderer,
        string layerName)
    {
        if (renderer == null)
        {
            return;
        }

        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == layerName)
            {
                renderer.sortingLayerName = layerName;
                return;
            }
        }
    }

    public void RefreshSortingOrder()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        float sortingY = sortingPoint != null
            ? sortingPoint.position.y
            : transform.position.y;
        int calculatedOrder =
            Mathf.RoundToInt(-sortingY * sortingPrecision) +
            sortingOffset;

        if (calculatedOrder == lastSortingOrder)
        {
            return;
        }

        lastSortingOrder = calculatedOrder;
        spriteRenderer.sortingOrder = calculatedOrder;
    }

    public void SetSortingOffset(int offset)
    {
        sortingOffset = offset;
        RefreshSortingOrder();
    }

    public void SetSortingPoint(Transform point)
    {
        sortingPoint = point;
        RefreshSortingOrder();
    }

    private void OnValidate()
    {
        sortingPrecision = Mathf.Max(1f, sortingPrecision);

        if (!Application.isPlaying)
        {
            RefreshSortingOrder();
        }
    }
}
