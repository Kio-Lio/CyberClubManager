using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(190)]
public sealed class FloorTileGridPresenter : MonoBehaviour
{
    public const string ResourcePath =
        "Environment/Architecture/CyberClub_FloorTile";
    public const float TileSize = 2f;

    private readonly List<SpriteRenderer> tileRenderers = new();

    public IReadOnlyList<SpriteRenderer> TileRenderers => tileRenderers;
    public int TileCount => tileRenderers.Count;

    public void Configure(Vector2 floorSize)
    {
        Sprite tileSprite = Resources.Load<Sprite>(ResourcePath);
        if (tileSprite == null)
        {
            Debug.LogWarning($"Floor tile sprite was not found: {ResourcePath}.");
            return;
        }

        int columns = Mathf.Max(1, Mathf.CeilToInt(floorSize.x / TileSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(floorSize.y / TileSize));
        float tileWidth = floorSize.x / columns;
        float tileHeight = floorSize.y / rows;
        int requiredCount = columns * rows;

        tileRenderers.Clear();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                SpriteRenderer renderer = EnsureTile(index);
                renderer.sprite = tileSprite;
                renderer.color = Color.white;
                renderer.enabled = true;
                renderer.sortingOrder = -9997;
                YSortRenderer.SetSortingLayer(renderer, "Background");

                renderer.transform.localPosition = new Vector3(
                    -floorSize.x * 0.5f +
                    tileWidth * (column + 0.5f),
                    -floorSize.y * 0.5f +
                    tileHeight * (row + 0.5f),
                    0f
                );
                renderer.transform.localRotation = Quaternion.identity;
                renderer.transform.localScale = new Vector3(
                    tileWidth / Mathf.Max(
                        0.01f,
                        tileSprite.bounds.size.x
                    ),
                    tileHeight / Mathf.Max(
                        0.01f,
                        tileSprite.bounds.size.y
                    ),
                    1f
                );
                tileRenderers.Add(renderer);
            }
        }

        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("FloorTile_") ||
                !int.TryParse(
                    child.name.Substring("FloorTile_".Length),
                    out int index) ||
                index < requiredCount)
            {
                continue;
            }

            if (child.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.enabled = false;
            }
        }
    }

    private SpriteRenderer EnsureTile(int index)
    {
        string objectName = $"FloorTile_{index:00}";
        Transform existing = transform.Find(objectName);
        GameObject tileObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName);
        tileObject.transform.SetParent(transform, false);

        SpriteRenderer renderer =
            tileObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = tileObject.AddComponent<SpriteRenderer>();
        }

        return renderer;
    }
}
