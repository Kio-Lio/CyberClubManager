using System.Collections.Generic;
using UnityEngine;

public sealed class ClientNavigationManager : MonoBehaviour
{
    public static ClientNavigationManager Instance { get; private set; }

    [Header("Main Nodes")]
    [SerializeField] private ClientNavigationNode entranceNode;
    [SerializeField] private ClientNavigationNode queueNode;
    [SerializeField] private ClientNavigationNode exitNode;

    public ClientNavigationNode EntranceNode => entranceNode;
    public ClientNavigationNode QueueNode => queueNode;
    public ClientNavigationNode ExitNode => exitNode;

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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeGraph()
    {
        EnsureRuntimeGraph();
    }

    public static ClientNavigationManager EnsureRuntimeGraph()
    {
        GameObject navigationObject =
            GameObject.Find("ClientNavigationManager");

        if (navigationObject == null)
        {
            navigationObject = new GameObject("ClientNavigationManager");
        }

        ClientNavigationManager navigationManager =
            navigationObject.GetComponent<ClientNavigationManager>() ??
            navigationObject.AddComponent<ClientNavigationManager>();

        navigationManager.EnsureDefaultGraph();
        return navigationManager;
    }

    public void EnsureDefaultGraph()
    {
        ClientNavigationNode entrance = EnsureNode(
            "EntranceNode",
            new Vector3(-6f, 0f, 0f)
        );
        ClientNavigationNode queue = EnsureNode(
            "QueueNode",
            new Vector3(-5f, 0f, 0f)
        );
        ClientNavigationNode mainAisle01 = EnsureNode(
            "MainAisle_01",
            new Vector3(-1f, 0f, 0f)
        );
        ClientNavigationNode mainAisle02 = EnsureNode(
            "MainAisle_02",
            new Vector3(3f, 0f, 0f)
        );
        ClientNavigationNode mainAisle03 = EnsureNode(
            "MainAisle_03",
            new Vector3(6f, 0f, 0f)
        );
        ClientNavigationNode exit = EnsureNode(
            "ExitNode",
            new Vector3(-5f, -1f, 0f)
        );

        entrance.AddNeighbour(queue);
        queue.AddNeighbour(mainAisle01);
        mainAisle01.AddNeighbour(mainAisle02);
        mainAisle02.AddNeighbour(mainAisle03);
        mainAisle01.AddNeighbour(exit);
        SetMainNodes(entrance, queue, exit);

        foreach (PC pc in FindObjectsByType<PC>())
        {
            EnsureApproachNode(pc);
        }
    }

    public void SetMainNodes(
        ClientNavigationNode entrance,
        ClientNavigationNode queue,
        ClientNavigationNode exit)
    {
        entranceNode = entrance;
        queueNode = queue;
        exitNode = exit;
    }

    public List<Vector3> BuildPath(
        ClientNavigationNode start,
        ClientNavigationNode destination)
    {
        List<Vector3> result = new();

        if (start == null || destination == null)
        {
            return result;
        }

        if (start == destination)
        {
            result.Add(destination.transform.position);
            return result;
        }

        Queue<ClientNavigationNode> openNodes = new();
        Dictionary<ClientNavigationNode, ClientNavigationNode> previousNodes =
            new();
        HashSet<ClientNavigationNode> visitedNodes = new();

        openNodes.Enqueue(start);
        visitedNodes.Add(start);

        bool pathFound = false;

        while (openNodes.Count > 0)
        {
            ClientNavigationNode current = openNodes.Dequeue();

            if (current == destination)
            {
                pathFound = true;
                break;
            }

            foreach (ClientNavigationNode neighbour in current.Neighbours)
            {
                if (neighbour == null || visitedNodes.Contains(neighbour))
                {
                    continue;
                }

                visitedNodes.Add(neighbour);
                previousNodes[neighbour] = current;
                openNodes.Enqueue(neighbour);
            }
        }

        if (!pathFound)
        {
            Debug.LogWarning(
                $"Маршрут не найден: {start.name} -> {destination.name}."
            );
            return result;
        }

        List<ClientNavigationNode> reversedPath = new();
        ClientNavigationNode pathNode = destination;

        while (pathNode != start)
        {
            reversedPath.Add(pathNode);
            pathNode = previousNodes[pathNode];
        }

        reversedPath.Reverse();

        foreach (ClientNavigationNode node in reversedPath)
        {
            result.Add(node.transform.position);
        }

        return result;
    }

    public ClientNavigationNode FindClosestNode(
        Vector3 position,
        ClientNavigationNode excludedNode = null)
    {
        ClientNavigationNode[] nodes =
            FindObjectsByType<ClientNavigationNode>();

        ClientNavigationNode closestNode = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (ClientNavigationNode node in nodes)
        {
            if (node == null || node == excludedNode)
            {
                continue;
            }

            float distanceSquared =
                (node.transform.position - position).sqrMagnitude;

            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestNode = node;
        }

        return closestNode;
    }

    private static ClientNavigationNode EnsureNode(
        string objectName,
        Vector3 position)
    {
        GameObject nodeObject = GameObject.Find(objectName);

        if (nodeObject == null)
        {
            nodeObject = new GameObject(objectName);
        }

        nodeObject.transform.position = position;

        return nodeObject.GetComponent<ClientNavigationNode>() ??
            nodeObject.AddComponent<ClientNavigationNode>();
    }

    private void EnsureApproachNode(PC pc)
    {
        if (pc == null)
        {
            return;
        }

        Vector3 approachPosition =
            pc.transform.position + new Vector3(0f, -0.8f, 0f);
        ClientNavigationNode approachNode = EnsureNode(
            $"{pc.name}_ApproachNode",
            approachPosition
        );
        pc.SetApproachNode(approachNode);

        ClientNavigationNode closestNode = FindClosestNode(
            approachPosition,
            approachNode
        );

        if (closestNode != null)
        {
            approachNode.AddNeighbour(closestNode);
        }
    }
}
