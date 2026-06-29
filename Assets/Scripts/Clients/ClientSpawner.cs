using UnityEngine;

public sealed class ClientSpawner : MonoBehaviour
{
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private float clientMoveSpeed = 2f;
    [SerializeField] private Color clientColor = Color.cyan;
    [SerializeField] private Vector3 exitPosition = new Vector3(-6f, 0f, 0f);

    private float timer;
    private Sprite generatedClientSprite;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0f;
            TrySpawnClient();
        }
    }

    private void TrySpawnClient()
    {
        PC freePc = FindFreePc();
        if (freePc == null)
        {
            Debug.Log("Нет свободных ПК для нового клиента.");
            return;
        }

        GameObject clientObject = new GameObject("Client");
        clientObject.transform.position = transform.position;
        clientObject.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        SpriteRenderer spriteRenderer = clientObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedClientSprite();
        spriteRenderer.color = clientColor;
        spriteRenderer.sortingOrder = 50;

        Client client = clientObject.AddComponent<Client>();
        client.Initialize(freePc, clientMoveSpeed, exitPosition);
        Debug.Log("Новый клиент пришел в клуб.");
    }

    private PC FindFreePc()
    {
        PC[] pcs = FindObjectsByType<PC>(FindObjectsSortMode.None);
        foreach (PC pc in pcs)
        {
            if (pc.IsFree)
            {
                return pc;
            }
        }

        return null;
    }

    private Sprite GetGeneratedClientSprite()
    {
        if (generatedClientSprite != null)
        {
            return generatedClientSprite;
        }

        Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        generatedClientSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return generatedClientSprite;
    }
}
