using System;
using UnityEngine;

public sealed class ClubProgressionManager : MonoBehaviour
{
    public static ClubProgressionManager Instance { get; private set; }

    [Header("Progression Settings")]
    [SerializeField, Min(1)] private int maxLevel = 5;
    [SerializeField, Min(1)] private int experiencePerClient = 10;
    [SerializeField, Min(1)] private int experiencePerDailyGoal = 50;

    private int level = 1;
    private int experience;

    public int Level => level;
    public int Experience => experience;
    public int MaxLevel => maxLevel;
    public int ExperienceToNextLevel =>
        level >= maxLevel
            ? 0
            : GetRequiredExperience(level);
    public int UnlockedExpansionSlots => Mathf.Clamp(level, 1, 4);
    public bool IsMaxLevel => level >= maxLevel;

    public event Action StatusChanged;
    public event Action<int> LevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        SubscribeToEvents();
        StatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeToEvents()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.ClientServed += OnClientServed;
        }

        if (DailyGoalManager.Instance != null)
        {
            DailyGoalManager.Instance.GoalCompletedEvent +=
                OnDailyGoalCompleted;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.ClientServed -= OnClientServed;
        }

        if (DailyGoalManager.Instance != null)
        {
            DailyGoalManager.Instance.GoalCompletedEvent -=
                OnDailyGoalCompleted;
        }
    }

    private void OnClientServed()
    {
        AddExperience(experiencePerClient);
    }

    private void OnDailyGoalCompleted()
    {
        AddExperience(experiencePerDailyGoal);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || IsMaxLevel)
        {
            return;
        }

        experience += amount;

        while (!IsMaxLevel)
        {
            int requiredExperience = GetRequiredExperience(level);

            if (experience < requiredExperience)
            {
                break;
            }

            experience -= requiredExperience;
            level++;

            Debug.Log(
                $"Уровень клуба повышен до {level}. " +
                $"Доступно мест расширения: {UnlockedExpansionSlots}."
            );

            LevelChanged?.Invoke(level);
        }

        if (IsMaxLevel)
        {
            experience = 0;
        }

        StatusChanged?.Invoke();
    }

    private static int GetRequiredExperience(int currentLevel)
    {
        return currentLevel switch
        {
            1 => 100,
            2 => 200,
            3 => 350,
            4 => 500,
            _ => int.MaxValue
        };
    }

    public void RestoreState(int savedLevel, int savedExperience)
    {
        level = Mathf.Clamp(savedLevel, 1, maxLevel);

        if (IsMaxLevel)
        {
            experience = 0;
        }
        else
        {
            experience = Mathf.Clamp(
                savedExperience,
                0,
                GetRequiredExperience(level) - 1
            );
        }

        StatusChanged?.Invoke();
        LevelChanged?.Invoke(level);
    }

    private void OnValidate()
    {
        maxLevel = Mathf.Max(1, maxLevel);
        experiencePerClient = Mathf.Max(1, experiencePerClient);
        experiencePerDailyGoal = Mathf.Max(1, experiencePerDailyGoal);
    }
}
