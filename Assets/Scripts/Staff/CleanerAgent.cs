using System.Collections.Generic;
using UnityEngine;

public sealed class CleanerAgent : MonoBehaviour
{
    private enum CleanerState
    {
        Idle,
        MovingToTrash,
        Cleaning,
        ReturningHome
    }

    private readonly List<Vector3> navigationPath = new();

    private CleanerManager manager;
    private TrashItem targetTrash;
    private CleanerState state = CleanerState.Idle;
    private int pathIndex;
    private float cleaningTimer;
    private float searchTimer;
    private Vector3 homePosition;

    public TrashItem TargetTrash => targetTrash;
    public bool IsWorking => state == CleanerState.MovingToTrash ||
                             state == CleanerState.Cleaning;

    public void Initialize(CleanerManager owner, Vector3 cleanerHomePosition)
    {
        manager = owner;
        homePosition = cleanerHomePosition;
        transform.position = homePosition;
        state = CleanerState.Idle;
        searchTimer = 0f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        switch (state)
        {
            case CleanerState.Idle:
                UpdateIdle();
                break;
            case CleanerState.MovingToTrash:
                UpdateMovingToTrash();
                break;
            case CleanerState.Cleaning:
                UpdateCleaning();
                break;
            case CleanerState.ReturningHome:
                UpdateReturningHome();
                break;
        }
    }

    private void OnDestroy()
    {
        ReleaseCurrentTarget();
    }

    private void UpdateIdle()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer > 0f)
        {
            return;
        }

        searchTimer = manager != null ? manager.SearchInterval : 1f;
        TryFindWork();
    }

    private void TryFindWork()
    {
        ClubCleanlinessManager cleanliness = ClubCleanlinessManager.Instance;
        if (cleanliness == null)
        {
            return;
        }

        TrashItem trash = cleanliness.FindClosestUnreservedTrash(transform.position);
        if (trash == null || !trash.TryReserveForCleaner())
        {
            return;
        }

        targetTrash = trash;
        if (!BuildRoute(targetTrash.transform.position))
        {
            ReleaseCurrentTarget();
            manager?.ReportNavigationFailure();
            return;
        }

        state = CleanerState.MovingToTrash;
        manager?.ReportMovingToTrash(targetTrash);
    }

    private void UpdateMovingToTrash()
    {
        if (targetTrash == null)
        {
            BeginReturnHome();
            return;
        }

        if (!UpdateNavigation())
        {
            return;
        }

        cleaningTimer = manager != null ? manager.CleaningDuration : 1.5f;
        state = CleanerState.Cleaning;
    }

    private void UpdateCleaning()
    {
        if (targetTrash == null)
        {
            BeginReturnHome();
            return;
        }

        cleaningTimer -= Time.deltaTime;
        if (cleaningTimer > 0f)
        {
            return;
        }

        TrashItem cleanedTrash = targetTrash;
        targetTrash = null;
        bool cleaned = ClubCleanlinessManager.Instance != null &&
            ClubCleanlinessManager.Instance.CleanTrash(cleanedTrash);

        if (cleaned)
        {
            manager?.ReportTrashCleaned(cleanedTrash.SourcePCName);
        }

        searchTimer = 0f;
        state = CleanerState.Idle;
    }

    private void BeginReturnHome()
    {
        ReleaseCurrentTarget();

        if (Vector3.Distance(transform.position, homePosition) <= 0.1f)
        {
            state = CleanerState.Idle;
            searchTimer = 0f;
            return;
        }

        if (!BuildRoute(homePosition))
        {
            transform.position = homePosition;
            state = CleanerState.Idle;
            searchTimer = 0f;
            return;
        }

        state = CleanerState.ReturningHome;
    }

    private void UpdateReturningHome()
    {
        if (!UpdateNavigation())
        {
            return;
        }

        state = CleanerState.Idle;
        searchTimer = 0f;
    }

    private bool BuildRoute(Vector3 destination)
    {
        navigationPath.Clear();
        pathIndex = 0;

        ClientNavigationManager navigation = ClientNavigationManager.Instance;
        if (navigation == null)
        {
            return false;
        }

        ClientNavigationNode startNode = navigation.FindClosestNode(transform.position);
        ClientNavigationNode destinationNode = navigation.FindClosestNode(destination);
        if (startNode == null || destinationNode == null ||
            !startNode.IsWalkable || !destinationNode.IsWalkable)
        {
            return false;
        }

        navigationPath.AddRange(navigation.BuildPath(startNode, destinationNode));
        if (navigationPath.Count == 0 && startNode != destinationNode)
        {
            return false;
        }

        if (navigationPath.Count == 0 ||
            Vector3.Distance(navigationPath[^1], destination) > 0.05f)
        {
            navigationPath.Add(destination);
        }

        return navigationPath.Count > 0;
    }

    private bool UpdateNavigation()
    {
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        if (pathIndex >= navigationPath.Count)
        {
            return true;
        }

        Vector3 targetPosition = navigationPath[pathIndex];
        float baseMovementSpeed = manager != null ? manager.MoveSpeed : 2.5f;
        float researchMultiplier = ClubResearchManager.Instance != null
            ? ClubResearchManager.Instance.GetCleanerSpeedMultiplier()
            : 1f;
        float movementSpeed = baseMovementSpeed * researchMultiplier;
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            movementSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            return false;
        }

        pathIndex++;
        return pathIndex >= navigationPath.Count;
    }

    private void ReleaseCurrentTarget()
    {
        if (targetTrash == null)
        {
            return;
        }

        targetTrash.ReleaseCleanerReservation();
        targetTrash = null;
    }
}
