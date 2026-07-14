using System;
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

    [Header("Client Patience")]
    [SerializeField] private float regularPatience = 15f;
    [SerializeField] private float gamerPatience = 18f;
    [SerializeField] private float vipPatience = 22f;

    [Header("Client Visuals")]
    [SerializeField] private Color regularColor = Color.cyan;
    [SerializeField] private Color gamerColor =
        new Color(0.3f, 0.9f, 0.35f);
    [SerializeField] private Color vipColor =
        new Color(1f, 0.65f, 0.15f);

    [Header("Testing")]
    [SerializeField] private bool forceClientType;
    [SerializeField] private ClientType forcedClientType;
    [SerializeField, Range(0.05f, 1f)]
    private float forcedPatienceMultiplier = 1f;

    [Header("Positions")]
    [SerializeField] private Vector3 queueStartOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private Vector3 queueSpacing = new Vector3(0f, -0.8f, 0f);
    [SerializeField] private Vector3 exitPosition = new Vector3(-6f, 0f, 0f);

    private readonly List<Client> waitingClients = new();

    private float spawnTimer;
    private float currentSpawnInterval;
    private int clientNumber;
    private Sprite generatedClientSprite;

    public int WaitingClientCount => waitingClients.Count;

    public event Action QueueChanged;

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
        regularPatience = Mathf.Max(1f, regularPatience);
        gamerPatience = Mathf.Max(1f, gamerPatience);
        vipPatience = Mathf.Max(1f, vipPatience);
        forcedPatienceMultiplier = Mathf.Clamp(
            forcedPatienceMultiplier,
            0.05f,
            1f
        );
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

        if (!waitingClients.Remove(client))
        {
            return;
        }

        QueueChanged?.Invoke();
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
            QueueChanged?.Invoke();
        }

        client.ResumeWaiting();
        RepositionQueue();
    }

    private void TrySpawnClient()
    {
        RemoveMissingClients();

        ClientType clientType = GenerateClientType();
        float patience = GetPatience(clientType);

        if (forceClientType)
        {
            patience *= forcedPatienceMultiplier;
        }

        if (waitingClients.Count >= maxQueueSize)
        {
            Debug.Log(
                $"Очередь заполнена. Клиент типа " +
                $"{GetClientTypeDisplayName(clientType)} не вошел в клуб."
            );

            if (ClubReputationManager.Instance != null)
            {
                ClubReputationManager.Instance.RegisterLostClient(clientType);
            }

            return;
        }

        GameObject clientObject = new GameObject(
            $"Client_{clientType}_{++clientNumber:00}"
        );
        clientObject.transform.position = transform.position;
        clientObject.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        SpriteRenderer spriteRenderer = clientObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedClientSprite();
        spriteRenderer.color = GetClientColor(clientType);
        spriteRenderer.sortingOrder = 2;

        Client client = clientObject.AddComponent<Client>();
        client.Initialize(
            this,
            clientType,
            clientMoveSpeed,
            patience,
            exitPosition,
            GetQueuePosition(waitingClients.Count)
        );

        waitingClients.Add(client);
        QueueChanged?.Invoke();

        Debug.Log(
            $"{clientObject.name}: в клуб пришел клиент типа " +
            $"{client.GetTypeDisplayName()}."
        );
        RepositionQueue();
    }

    private void AssignAvailablePCs()
    {
        RemoveMissingClients();

        bool assignmentMade;

        do
        {
            assignmentMade = false;

            for (int clientIndex = 0;
                 clientIndex < waitingClients.Count;
                 clientIndex++)
            {
                Client client = waitingClients[clientIndex];

                if (client == null)
                {
                    continue;
                }

                PC availablePc = FindBestAvailablePC(client);

                if (availablePc == null || !availablePc.TryReserve())
                {
                    continue;
                }

                waitingClients.RemoveAt(clientIndex);
                QueueChanged?.Invoke();

                client.AssignPC(availablePc);
                RepositionQueue();

                assignmentMade = true;
                break;
            }
        }
        while (assignmentMade);
    }

    public int GetWaitingClientCount(ClientType type)
    {
        int count = 0;

        foreach (Client client in waitingClients)
        {
            if (client != null && client.Type == type)
            {
                count++;
            }
        }

        return count;
    }

    private PC FindBestAvailablePC(Client client)
    {
        if (client == null)
        {
            return null;
        }

        PC bestPc = null;

        foreach (PC pc in FindObjectsByType<PC>())
        {
            if (!client.CanUsePC(pc))
            {
                continue;
            }

            if (bestPc == null || pc.Tier < bestPc.Tier)
            {
                bestPc = pc;
            }
        }

        return bestPc;
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
        int previousCount = waitingClients.Count;
        waitingClients.RemoveAll(client => client == null);

        if (waitingClients.Count != previousCount)
        {
            QueueChanged?.Invoke();
        }
    }

    private ClientType GenerateClientType()
    {
        if (forceClientType)
        {
            return forcedClientType;
        }

        int clubLevel = ClubProgressionManager.Instance != null
            ? ClubProgressionManager.Instance.Level
            : 1;

        float roll = UnityEngine.Random.value;

        if (clubLevel >= 4)
        {
            if (roll < 0.15f)
            {
                return ClientType.VIP;
            }

            return roll < 0.45f
                ? ClientType.Gamer
                : ClientType.Regular;
        }

        if (clubLevel >= 2)
        {
            return roll < 0.25f
                ? ClientType.Gamer
                : ClientType.Regular;
        }

        return ClientType.Regular;
    }

    private static string GetClientTypeDisplayName(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.Regular => "Обычный",
            ClientType.Gamer => "Геймер",
            ClientType.VIP => "VIP",
            _ => clientType.ToString()
        };
    }

    private float GetPatience(ClientType type)
    {
        return type switch
        {
            ClientType.Regular => regularPatience,
            ClientType.Gamer => gamerPatience,
            ClientType.VIP => vipPatience,
            _ => regularPatience
        };
    }

    private Color GetClientColor(ClientType type)
    {
        return type switch
        {
            ClientType.Regular => regularColor,
            ClientType.Gamer => gamerColor,
            ClientType.VIP => vipColor,
            _ => regularColor
        };
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
