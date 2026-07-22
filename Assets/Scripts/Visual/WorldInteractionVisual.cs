using UnityEngine;

public interface IWorldInteractionVisual
{
    void SetHovered(bool hovered);
    void SetSelected(bool selected);
}

public static class WorldVisualPrimitives
{
    private static Sprite squareSprite;

    public static Sprite SquareSprite
    {
        get
        {
            if (squareSprite == null)
            {
                Texture2D texture = new(8, 8, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                Color32[] pixels = new Color32[64];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = Color.white;
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                squareSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    texture.width
                );
                squareSprite.name = "RuntimeWorldSquare";
                squareSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return squareSprite;
        }
    }

    public static SpriteRenderer CreatePart(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color,
        int orderOffset)
    {
        GameObject part = new(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = new Vector3(
            position.x,
            position.y,
            0f
        );
        part.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite;
        renderer.color = color;
        YSortRenderer.SetSortingLayer(renderer, "World");

        WorldVisualPart visualPart = part.AddComponent<WorldVisualPart>();
        visualPart.OrderOffset = orderOffset;
        return renderer;
    }
}

public sealed class WorldVisualPart : MonoBehaviour
{
    public int OrderOffset { get; set; }
}
