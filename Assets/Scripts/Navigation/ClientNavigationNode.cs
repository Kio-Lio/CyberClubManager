using System.Collections.Generic;
using UnityEngine;

public sealed class ClientNavigationNode : MonoBehaviour
{
    [SerializeField] private List<ClientNavigationNode> neighbours = new();

    public IReadOnlyList<ClientNavigationNode> Neighbours => neighbours;

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
