using System;
using UnityEngine;

public sealed class ClubReputationManager : MonoBehaviour
{
    public static ClubReputationManager Instance { get; private set; }

    [Header("Reputation Settings")]
    [SerializeField, Range(0, 100)] private int reputation = 50;
    [SerializeField] private int rewardForServedClient = 1;
    [SerializeField] private int penaltyForLostClient = 5;

    private int servedClients;
    private int lostClients;

    public int Reputation => reputation;
    public int ServedClients => servedClients;
    public int LostClients => lostClients;
    public float NormalizedReputation => reputation / 100f;

    public event Action StatusChanged;

    public void RestoreState(
        int savedReputation,
        int savedServedClients,
        int savedLostClients)
    {
        reputation = Mathf.Clamp(savedReputation, 0, 100);
        servedClients = Mathf.Max(0, savedServedClients);
        lostClients = Mathf.Max(0, savedLostClients);
        StatusChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterServedClient()
    {
        servedClients++;
        reputation = Mathf.Clamp(reputation + rewardForServedClient, 0, 100);
        Debug.Log($"Client served. Reputation: {reputation}/100. Served: {servedClients}.");
        StatusChanged?.Invoke();
    }

    public void RegisterLostClient()
    {
        lostClients++;
        reputation = Mathf.Clamp(reputation - penaltyForLostClient, 0, 100);
        Debug.Log($"Client lost. Reputation: {reputation}/100. Lost: {lostClients}.");
        StatusChanged?.Invoke();
    }
}
