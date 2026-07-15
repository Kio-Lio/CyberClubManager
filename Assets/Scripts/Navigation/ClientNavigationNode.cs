using System.Collections.Generic;
using UnityEngine;

public sealed class ClientNavigationNode : MonoBehaviour
{
    [SerializeField] private List<ClientNavigationNode> neighbours = new();
    [SerializeField] private bool isWalkable = true;

    public IReadOnlyList<ClientNavigationNode> Neighbours => neighbours;
    public bool IsWalkable => isWalkable;

    public void SetWalkable(bool walkable)
    {
        isWalkable = walkable;
    }

    public void ClearNeighbours()
    {
        neighbours.Clear();
    }

    public void AddNeighbour(
        ClientNavigationNode neighbour,
        bool addReverseConnection = true)
    {
        if (neighbour == null ||
            neighbour == this ||
            neighbours.Contains(neighbour))
        {
            return;
        }

        neighbours.Add(neighbour);

        if (addReverseConnection)
        {
            neighbour.AddNeighbour(this, false);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isWalkable ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position, 0.12f);

        foreach (ClientNavigationNode neighbour in neighbours)
        {
            if (neighbour == null)
            {
                continue;
            }

            Gizmos.DrawLine(
                transform.position,
                neighbour.transform.position
            );
        }
    }
}
