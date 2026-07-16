using UnityEngine;

public sealed class TutorialWorldMarker : MonoBehaviour
{
    private Transform target;
    private Vector3 baseScale;
    private float baseHeight = 1.15f;
    private Sprite markerSprite;

    public Transform Target => target;

    public void Initialize(Transform markerTarget)
    {
        target = markerTarget;
        baseScale = new Vector3(0.34f, 0.34f, 1f);
        transform.localScale = baseScale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        markerSprite = CreateMarkerSprite();
        renderer.sprite = markerSprite;
        renderer.color = new Color(0.55f, 1f, 0.45f, 0.95f);
        YSortRenderer.SetSortingLayer(renderer, "UI");
        renderer.sortingOrder = 1000;
    }

    private void OnDestroy()
    {
        if (markerSprite == null)
        {
            return;
        }

        Texture2D texture = markerSprite.texture;
        Destroy(markerSprite);
        if (texture != null)
        {
            Destroy(texture);
        }
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.18f;
        float bob = Mathf.Sin(Time.unscaledTime * 3f) * 0.12f;
        transform.position = target.position + Vector3.up * (baseHeight + bob);
        transform.localScale = baseScale * pulse;
    }

    private static Sprite CreateMarkerSprite()
    {
        Texture2D texture = new Texture2D(8, 8);
        Color[] pixels = new Color[64];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int distance = Mathf.Abs(x - 3) + Mathf.Abs(y - 3);
                pixels[y * 8 + x] = distance <= 3 ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f), 8f);
    }
}
