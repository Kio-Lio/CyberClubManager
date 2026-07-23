using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ClubCleanlinessManager : MonoBehaviour
{
    public static ClubCleanlinessManager Instance { get; private set; }

    [Header("Trash Generation")]
    [SerializeField, Range(0f, 1f)] private float trashSpawnChance = 0.35f;
    [SerializeField, Min(1)] private int maximumTrashItems = 10;
    [SerializeField, Min(0.1f)] private float cleanlinessLossPerTrash = 10f;
    [SerializeField, Min(1)] private int maximumTrashPerPC = 2;

    [Header("Visuals")]
    [SerializeField] private Color trashColor = new(0.24f, 0.23f, 0.22f);
    [SerializeField] private Vector2 trashSize = new(0.28f, 0.18f);

    private readonly List<TrashItem> activeTrashItems = new();
    private readonly List<PC> registeredPCs = new();

    private Transform trashRoot;
    private Sprite trashSprite;

    public int TrashCount => activeTrashItems.Count;
    public float Cleanliness => Mathf.Clamp(
        100f - TrashCount * cleanlinessLossPerTrash,
        0f,
        100f
    );
    public IReadOnlyList<TrashItem> ActiveTrashItems => activeTrashItems;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateTrashRoot();
        trashSprite = CreateSquareSprite();
    }

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;

        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }

        StatusChanged?.Invoke();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;

        foreach (PC pc in registeredPCs)
        {
            if (pc != null)
            {
                pc.SessionCompleted -= OnPCSessionCompleted;
            }
        }

        registeredPCs.Clear();

        if (trashSprite != null)
        {
            Destroy(trashSprite);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RegisterPC(PC pc)
    {
        if (pc == null || registeredPCs.Contains(pc))
        {
            return;
        }

        registeredPCs.Add(pc);
        pc.SessionCompleted += OnPCSessionCompleted;
    }

    private void UnregisterPC(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        pc.SessionCompleted -= OnPCSessionCompleted;
        registeredPCs.Remove(pc);
    }

    private void OnPCSessionCompleted(PC pc)
    {
        bool forceTutorialTrash = FirstDayTutorialManager.Instance != null &&
            FirstDayTutorialManager.Instance.ShouldForceTutorialTrash;
        if (pc == null || activeTrashItems.Count >= maximumTrashItems ||
            CountTrashForPC(pc.name) >= maximumTrashPerPC ||
            (!forceTutorialTrash && UnityEngine.Random.value > trashSpawnChance))
        {
            return;
        }

        SpawnTrashForPC(pc);
    }

    public void EnsureTutorialTrash(PC pc)
    {
        if (pc == null || activeTrashItems.Count >= maximumTrashItems ||
            CountTrashForPC(pc.name) > 0)
        {
            return;
        }

        SpawnTrashForPC(pc);
    }

    private int CountTrashForPC(string pcName)
    {
        int count = 0;

        foreach (TrashItem trash in activeTrashItems)
        {
            if (trash != null && trash.SourcePCName == pcName)
            {
                count++;
            }
        }

        return count;
    }

    private void SpawnTrashForPC(PC pc)
    {
        int existingCount = CountTrashForPC(pc.name);
        Vector3 basePosition = pc.ApproachNode != null
            ? pc.ApproachNode.transform.position
            : pc.transform.position + Vector3.down * 0.7f;

        SpawnTrash(
            Guid.NewGuid().ToString("N"),
            pc.name,
            basePosition + GetTrashOffset(existingCount)
        );
    }

    private static Vector3 GetTrashOffset(int existingTrashCount)
    {
        return existingTrashCount switch
        {
            0 => new Vector3(-0.32f, -0.18f, 0f),
            1 => new Vector3(0.32f, -0.12f, 0f),
            _ => Vector3.zero
        };
    }

    private TrashItem SpawnTrash(
        string trashId,
        string sourcePCName,
        Vector3 position)
    {
        GameObject trashObject = new GameObject($"Trash_{trashId}");
        trashObject.transform.SetParent(trashRoot, false);
        trashObject.transform.position = position;
        trashObject.transform.localScale = Vector3.one;

        SpriteRenderer renderer = trashObject.AddComponent<SpriteRenderer>();
        renderer.sprite = trashSprite;
        renderer.color = trashColor;
        YSortRenderer.SetSortingLayer(renderer, "World");

        BoxCollider2D collider = trashObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = trashSize;
        YSortRenderer.Ensure(trashObject, 5, 0f);

        TrashItem trash = trashObject.AddComponent<TrashItem>();
        trash.Initialize(this, trashId, sourcePCName);
        trashObject.AddComponent<TrashVisualPresenter>();
        activeTrashItems.Add(trash);

        StatusChanged?.Invoke();
        Debug.Log(
            $"Появился мусор возле {sourcePCName}. " +
            $"Чистота клуба: {Cleanliness:F0}%."
        );
        return trash;
    }

    public TrashItem FindClosestUnreservedTrash(Vector3 position)
    {
        activeTrashItems.RemoveAll(trash => trash == null);

        TrashItem closestTrash = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (TrashItem trash in activeTrashItems)
        {
            if (trash == null || trash.IsReservedByCleaner)
            {
                continue;
            }

            float distanceSquared =
                (trash.transform.position - position).sqrMagnitude;

            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestTrash = trash;
        }

        return closestTrash;
    }

    public bool CleanTrash(TrashItem trash)
    {
        if (trash == null || !activeTrashItems.Remove(trash))
        {
            return false;
        }

        trash.ReleaseCleanerReservation();
        Destroy(trash.gameObject);
        StatusChanged?.Invoke();
        Debug.Log($"Мусор убран. Чистота клуба: {Cleanliness:F0}%.");
        FirstDayTutorialManager.Instance?.ReportAction(
            TutorialStepType.CleanTrash
        );
        return true;
    }

    public TrashSaveData[] CreateSaveData()
    {
        activeTrashItems.RemoveAll(trash => trash == null);
        TrashSaveData[] result = new TrashSaveData[activeTrashItems.Count];

        for (int index = 0; index < activeTrashItems.Count; index++)
        {
            TrashItem trash = activeTrashItems[index];
            Vector3 position = trash.transform.position;
            result[index] = new TrashSaveData
            {
                trashId = trash.TrashId,
                sourcePCName = trash.SourcePCName,
                positionX = position.x,
                positionY = position.y
            };
        }

        return result;
    }

    public void RestoreState(TrashSaveData[] savedTrashItems)
    {
        ClearAllTrash();

        if (savedTrashItems != null)
        {
            foreach (TrashSaveData savedTrash in savedTrashItems)
            {
                if (savedTrash == null ||
                    string.IsNullOrWhiteSpace(savedTrash.trashId) ||
                    activeTrashItems.Count >= maximumTrashItems)
                {
                    continue;
                }

                SpawnTrash(
                    savedTrash.trashId,
                    savedTrash.sourcePCName,
                    new Vector3(savedTrash.positionX, savedTrash.positionY, 0f)
                );
            }
        }

        StatusChanged?.Invoke();
    }

    private void ClearAllTrash()
    {
        foreach (TrashItem trash in activeTrashItems)
        {
            if (trash != null)
            {
                Destroy(trash.gameObject);
            }
        }

        activeTrashItems.Clear();
    }

    private void CreateTrashRoot()
    {
        Transform existingRoot = transform.Find("ActiveTrash");
        if (existingRoot != null)
        {
            trashRoot = existingRoot;
            return;
        }

        GameObject rootObject = new GameObject("ActiveTrash");
        rootObject.transform.SetParent(transform, false);
        trashRoot = rootObject.transform;
    }

    private static Sprite CreateSquareSprite()
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }

    private void OnValidate()
    {
        maximumTrashItems = Mathf.Max(1, maximumTrashItems);
        maximumTrashPerPC = Mathf.Max(1, maximumTrashPerPC);
        cleanlinessLossPerTrash = Mathf.Max(0.1f, cleanlinessLossPerTrash);
        trashSize.x = Mathf.Max(0.05f, trashSize.x);
        trashSize.y = Mathf.Max(0.05f, trashSize.y);
    }
}
