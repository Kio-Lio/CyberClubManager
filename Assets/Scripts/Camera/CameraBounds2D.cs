using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CameraBounds2D : MonoBehaviour
{
    private BoxCollider2D boundsCollider;

    public Bounds WorldBounds
    {
        get
        {
            EnsureCollider();
            return boundsCollider.bounds;
        }
    }

    private void Awake()
    {
        EnsureCollider();
        boundsCollider.isTrigger = true;
    }

    private void EnsureCollider()
    {
        if (boundsCollider == null)
        {
            boundsCollider = GetComponent<BoxCollider2D>();
        }
    }

    public void Configure(Vector2 size)
    {
        EnsureCollider();

        boundsCollider.size = new Vector2(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y)
        );
        boundsCollider.offset = Vector2.zero;
        boundsCollider.isTrigger = true;
    }
}
