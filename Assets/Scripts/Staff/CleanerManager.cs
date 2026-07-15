using System;
using UnityEngine;

public sealed class CleanerManager : MonoBehaviour
{
    public static CleanerManager Instance { get; private set; }

    [Header("Hiring")]
    [SerializeField, Min(0)] private int hireCost = 1500;
    [SerializeField, Min(0)] private int dailySalary = 180;

    [Header("Work")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float cleaningDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float searchInterval = 0.75f;
    [SerializeField] private Vector3 cleanerHomePosition =
        new(-3.8f, 2.5f, 0f);

    private bool cleanerHired;
    private CleanerAgent cleanerAgent;
    private Sprite runtimeSprite;
    private string lastWorkMessage = "Уборщик не нанят.";

    public bool CleanerHired => cleanerHired;
    public int HireCost => hireCost;
    public int DailySalary => dailySalary;
    public float MoveSpeed => moveSpeed;
    public float CleaningDuration => cleaningDuration;
    public float SearchInterval => searchInterval;
    public string LastWorkMessage => lastWorkMessage;
    public CleanerAgent CleanerAgent => cleanerAgent;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null)
        {
            Destroy(runtimeSprite);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryHireCleaner()
    {
        if (cleanerHired)
        {
            return false;
        }

        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(
            hireCost,
            EconomyTransactionCategory.StaffHire
        ))
        {
            lastWorkMessage = $"Для найма уборщика нужно {hireCost} ₽.";
            StatusChanged?.Invoke();
            return false;
        }

        cleanerHired = true;
        SpawnCleanerAgent();
        lastWorkMessage =
            $"Уборщик нанят. Зарплата: {dailySalary} ₽ в день.";
        Debug.Log(lastWorkMessage);
        StatusChanged?.Invoke();
        return true;
    }

    public int GetDailyOperatingCost()
    {
        return cleanerHired ? dailySalary : 0;
    }

    public void RestoreState(bool savedCleanerHired)
    {
        cleanerHired = savedCleanerHired;

        if (cleanerHired)
        {
            SpawnCleanerAgent();
            lastWorkMessage =
                $"Уборщик работает. Зарплата: {dailySalary} ₽ в день.";
        }
        else
        {
            DestroyCleanerAgent();
            lastWorkMessage = "Уборщик не нанят.";
        }

        StatusChanged?.Invoke();
    }

    private void SpawnCleanerAgent()
    {
        if (!cleanerHired || cleanerAgent != null)
        {
            return;
        }

        GameObject cleanerObject = GameObject.Find("Cleaner");
        if (cleanerObject == null)
        {
            cleanerObject = new GameObject("Cleaner");
        }

        cleanerObject.transform.position = cleanerHomePosition;
        cleanerObject.transform.localScale = new Vector3(0.55f, 0.75f, 1f);

        SpriteRenderer renderer = cleanerObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = cleanerObject.AddComponent<SpriteRenderer>();
        }
        renderer.sprite = GetRuntimeSprite();
        renderer.color = new Color(0.25f, 0.85f, 0.75f);
        YSortRenderer.SetSortingLayer(renderer, "World");
        YSortRenderer.Ensure(cleanerObject, 12, -0.35f);

        cleanerAgent = cleanerObject.GetComponent<CleanerAgent>();
        if (cleanerAgent == null)
        {
            cleanerAgent = cleanerObject.AddComponent<CleanerAgent>();
        }
        cleanerAgent.Initialize(this, cleanerHomePosition);
    }

    private void DestroyCleanerAgent()
    {
        if (cleanerAgent == null)
        {
            return;
        }

        cleanerAgent.gameObject.SetActive(false);
        Destroy(cleanerAgent.gameObject);
        cleanerAgent = null;
    }

    public void ReportMovingToTrash(TrashItem trash)
    {
        if (trash == null)
        {
            return;
        }

        lastWorkMessage = $"Уборщик идёт к мусору возле {trash.SourcePCName}.";
        StatusChanged?.Invoke();
    }

    public void ReportTrashCleaned(string sourcePCName)
    {
        lastWorkMessage = $"Уборщик убрал мусор возле {sourcePCName}.";
        Debug.Log(lastWorkMessage);
        StatusChanged?.Invoke();
    }

    public void ReportNavigationFailure()
    {
        lastWorkMessage = "Уборщик не смог построить маршрут.";
        Debug.LogWarning(lastWorkMessage);
        StatusChanged?.Invoke();
    }

    private Sprite GetRuntimeSprite()
    {
        if (runtimeSprite != null)
        {
            return runtimeSprite;
        }

        Texture2D texture = new Texture2D(1, 1)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        runtimeSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        return runtimeSprite;
    }

    private void OnValidate()
    {
        hireCost = Mathf.Max(0, hireCost);
        dailySalary = Mathf.Max(0, dailySalary);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
        cleaningDuration = Mathf.Max(0.1f, cleaningDuration);
        searchInterval = Mathf.Max(0.1f, searchInterval);
    }
}
