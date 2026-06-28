using UnityEngine;

public class PC : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        Debug.Log("Игрок взаимодействует с ПК");
    }
}