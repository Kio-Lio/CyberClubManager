using System.Collections.Generic;
using UnityEngine;

public sealed class ClientSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 8f;
    [SerializeField] private int maxQueueSize = 5;

    [Header("Client Settings")]
    [SerializeField] private float clientMoveSpeed = 2f;
    [SerializeField] private float clientPatience = 15f;
    [SerializeField] private Color clientColor = Color.cyan;

    [Header("Positions")]
    [SerializeField] private Vector3 queueStartOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private Vector3 queueSpacing = new Vector3(0f, -0.8f, 0f);
    [SerializeField] private Vector3 exitPosition = new Vector3(-6f, 0f, 0f);

    private readonly List<Client> waitingClients = new();

    private float spawnTimer;
    private float currentSpawnInterval;
    private int clientNumber;
    private Sprite generatedClientSprite;

    private void Start()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged += RefreshSpawnInterval;
        }

        RefreshSpawnInterval();
    }

    private void OnDestroy()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged -= RefreshSpawnInterval;
        }
    }

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        minSpawnInterval = Mathf.Max(0.1f, minSpawnInterval);
        maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);
    }

    private void RefreshSpawnInterval()
    {
        if (ClubReputationManager.Instance == null)
        {
            currentSpawnInterval = spawnInterval;
            Debug.LogWarning(
                "ClubReputationManager is missing. " +
                $"Using the default spawn interval: {currentSpawnInterval:F1} sec."
            );
            return;
        }

        float reputation = ClubReputationManager.Instance.NormalizedReputation;
        currentSpawnInterval = Mathf.Lerp(
            maxSpawnInterval,
            minSpawnInterval,
            reputation
        );

        Debug.Log(
            "Client demand updated. " +
            $"Reputation: {ClubReputationManager.Instance.Reputation}/100. " +
            $"Spawn interval: {currentSpawnInterval:F1} sec."
        );
    }

    private void Update()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            TrySpawnClient();
        }

        AssignAvailablePCs();
    }

    public void RemoveFromQueue(Client client)
    {
        if (client == null)
        {
            return;
        }

        waitingClients.Remove(client);
        RepositionQueue();
    }

    public void ReturnToQueue(Client client)
    {
        if (client == null)
        {
            return;
        }

        if (!waitingClients.Contains(client))
        {
            waitingClients.Add(client);
        }

        client.ResumeWaiting();
        RepositionQueue();
    }

    private void TrySpawnClient()
    {
        RemoveMissingClients();

        if (waitingClients.Count >= maxQueueSize)
        {
            Debug.Log("Queue is full. The new client did not enter the club.");

            if (ClubReputationManager.Instance != null)
            {
                ClubReputationManager.Instance.RegisterLostClient();
            }

            return;
        }

        GameObject clientObject = new GameObject($"Client_{++clientNumber:00}");
        clientObject.transform.position = transform.position;
        clientObject.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        SpriteRenderer spriteRenderer = clientObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedClientSprite();
        spriteRenderer.color = clientColor;
        spriteRenderer.sortingOrder = 50;

        Client client = clientObject.AddComponent<Client>();
        waitingClients.Add(client);

        client.Initialize(
            this,
            clientMoveSpeed,
            clientPatience,
            exitPosition,
            GetQueuePosition(waitingClients.Count - 1)
        );

        Debug.Log($"{clientObject.name}: new client entered the club.");
        RepositionQueue();
    }

    private void AssignAvailablePCs()
    {
        RemoveMissingClients();

        while (waitingClients.Count > 0)
        {
            PC availablePc = FindAvailablePC();
            if (availablePc == null)
            {
                return;
            }

            if (!availablePc.TryReserve())
            {
                continue;
            }

            Client client = waitingClients[0];
            waitingClients.RemoveAt(0);

            if (client == null)
            {
                availablePc.CancelReservation();
                continue;
            }

            client.AssignPC(availablePc);
            RepositionQueue();
        }
    }

    private PC FindAvailablePC()
    {
        PC[] pcs = FindObjectsByType<PC>();

        foreach (PC pc in pcs)
        {
            if (pc != null && pc.IsAvailable)
            {
                return pc;
            }
        }

        return null;
    }

    private void RepositionQueue()
    {
        RemoveMissingClients();

        for (int i = 0; i < waitingClients.Count; i++)
        {
            waitingClients[i].SetWaitingPosition(GetQueuePosition(i));
        }
    }

    private Vector3 GetQueuePosition(int index)
    {
        return transform.position + queueStartOffset + queueSpacing * index;
    }

    private void RemoveMissingClients()
    {
        waitingClients.RemoveAll(client => client == null);
    }

    private Sprite GetGeneratedClientSprite()
    {
        if (generatedClientSprite != null)
        {
            return generatedClientSprite;
        }

        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[16 * 16];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedClientSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.5f),
            16f
        );

        return generatedClientSprite;
    }
}
