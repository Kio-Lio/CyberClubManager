using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class ReceptionVisualPresenter : MonoBehaviour
{
    private const string ResourcePath = "World/Reception";
    private const float PixelsPerUnit = 360f;

    private SpriteRenderer spriteRenderer;
    private Sprite receptionSprite;

    private void Awake()
    {
        ApplyVisual();
    }

    private void OnDestroy()
    {
        if (receptionSprite != null)
        {
            Destroy(receptionSprite);
        }
    }

    public void ApplyVisual()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();

        if (receptionSprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                Debug.LogWarning(
                    $"Reception sprite was not found: {ResourcePath}."
                );
                return;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            receptionSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect
            );
            receptionSprite.name = "ReceptionSprite";
        }

        spriteRenderer.sprite = receptionSprite;
        spriteRenderer.color = Color.white;
        YSortRenderer.SetSortingLayer(spriteRenderer, "World");
    }
}
