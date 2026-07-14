using System.Collections.Generic;
using UnityEngine;

public sealed class ClientNavigationManager : MonoBehaviour
{
    public static ClientNavigationManager Instance { get; private set; }

    [Header("Main Nodes")]
    [SerializeField] private ClientNavigationNode entranceNode;
    [SerializeField] private ClientNavigationNode queueNode;
    [SerializeField] private ClientNavigationNode exitNode;
    [SerializeField] private ClientNavigationNode mainAisleLeft;
    [SerializeField] private ClientNavigationNode mainAisleCenter;
    [SerializeField] private ClientNavigationNode mainAisleRight;
    [SerializeField] private ClientNavigationNode lowerAisleLeft;
    [SerializeField] private ClientNavigationNode lowerAisleCenter;
    [SerializeField] private ClientNavigationNode lowerAisleRight;

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
        RemoveLegacyNode("MainAisle_01");
        RemoveLegacyNode("MainAisle_02");
        RemoveLegacyNode("MainAisle_03");

        entranceNode = EnsureNode(
            "EntranceNode",
            new Vector3(-3.5f, -4.2f, 0f)
        );
        queueNode = EnsureNode(
            "QueueNode",
            new Vector3(-3.5f, 2f, 0f)
        );
        mainAisleLeft = EnsureNode(
            "MainAisle_Left",
            new Vector3(-0.5f, 1f, 0f)
        );
        mainAisleCenter = EnsureNode(
            "MainAisle_Center",
            new Vector3(3.8f, 1f, 0f)
        );
        mainAisleRight = EnsureNode(
            "MainAisle_Right",
            new Vector3(6.8f, 1f, 0f)
        );
        lowerAisleLeft = EnsureNode(
            "LowerAisle_Left",
            new Vector3(-0.5f, -2.5f, 0f)
        );
        lowerAisleCenter = EnsureNode(
            "LowerAisle_Center",
            new Vector3(3.8f, -2.5f, 0f)
        );
        lowerAisleRight = EnsureNode(
            "LowerAisle_Right",
            new Vector3(6.8f, -2.5f, 0f)
        );
        exitNode = EnsureNode(
            "ExitNode",
            new Vector3(-1.5f, -4.2f, 0f)
        );

        foreach (ClientNavigationNode node in
                 FindObjectsByType<ClientNavigationNode>())
        {
            node.ClearNeighbours();
        }

        entranceNode.AddNeighbour(queueNode);
        queueNode.AddNeighbour(mainAisleLeft);
        mainAisleLeft.AddNeighbour(mainAisleCenter);
        mainAisleCenter.AddNeighbour(mainAisleRight);
        mainAisleLeft.AddNeighbour(lowerAisleLeft);
        lowerAisleLeft.AddNeighbour(lowerAisleCenter);
        lowerAisleCenter.AddNeighbour(lowerAisleRight);
        lowerAisleLeft.AddNeighbour(exitNode);

        foreach (PC pc in FindObjectsByType<PC>())
        {
            EnsureApproachNode(pc);
        }
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
                $"Navigation path was not found: {start.name} -> {destination.name}."
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
        ClientNavigationNode closestNode = null;
        float closestDistanceSquared = float.MaxValue;

        foreach (ClientNavigationNode node in
                 FindObjectsByType<ClientNavigationNode>())
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

    public ClientNavigationNode EnsureApproachNode(PC pc)
    {
        if (pc == null)
        {
            return null;
        }

        ClientNavigationNode approachNode = EnsureNode(
            $"{pc.name}_ApproachNode",
            GetApproachPosition(pc)
        );
        pc.SetApproachNode(approachNode);

        ClientNavigationNode anchor = GetApproachAnchor(pc);
        if (anchor != null)
        {
            approachNode.AddNeighbour(anchor);
        }

        return approachNode;
    }

    private Vector3 GetApproachPosition(PC pc)
    {
        int pcNumber = GetPcNumber(pc);

        if (pcNumber >= 1 && pcNumber <= 3)
        {
            return pc.transform.position + Vector3.down;
        }

        return pc.transform.position + Vector3.up;
    }

    private ClientNavigationNode GetApproachAnchor(PC pc)
    {
        int pcNumber = GetPcNumber(pc);

        return pcNumber switch
        {
            1 => mainAisleLeft,
            2 => mainAisleCenter,
            3 => mainAisleRight,
            4 => mainAisleLeft,
            5 => mainAisleCenter,
            6 => mainAisleRight,
            7 => lowerAisleLeft,
            8 => lowerAisleCenter,
            9 => lowerAisleRight,
            _ => FindClosestNode(pc.transform.position)
        };
    }

    private static int GetPcNumber(PC pc)
    {
        if (pc == null)
        {
            return 0;
        }

        string numberText = pc.name.Replace("PC_", string.Empty);
        return int.TryParse(numberText, out int pcNumber) ? pcNumber : 0;
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

    private static void RemoveLegacyNode(string objectName)
    {
        GameObject legacyNode = GameObject.Find(objectName);

        if (legacyNode == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(legacyNode);
            return;
        }

        DestroyImmediate(legacyNode);
    }
}
