using System.Collections;
using UnityEngine;

public sealed class Client : MonoBehaviour
{
    private PC targetPc;
    private float moveSpeed;
    private Vector3 exitPosition;

    public void Initialize(PC pc, float speed, Vector3 exit)
    {
        targetPc = pc;
        moveSpeed = speed;
        exitPosition = exit;
        StartCoroutine(ClientRoutine());
    }

    private IEnumerator ClientRoutine()
    {
        if (targetPc == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 seatPosition = targetPc.transform.position + new Vector3(0f, -0.8f, 0f);
        yield return MoveTo(seatPosition);

        if (!targetPc.TryOccupy())
        {
            Debug.Log("Клиент не смог занять ПК и уходит.");
            yield return MoveTo(exitPosition);
            Destroy(gameObject);
            yield break;
        }

        Debug.Log("Клиент начал играть.");
        yield return new WaitUntil(() => targetPc == null || !targetPc.IsOccupied);
        Debug.Log("Клиент уходит из клуба.");
        yield return MoveTo(exitPosition);
        Destroy(gameObject);
    }

    private IEnumerator MoveTo(Vector3 targetPosition)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
    }
}
