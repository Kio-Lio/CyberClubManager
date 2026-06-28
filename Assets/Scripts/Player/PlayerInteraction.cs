using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    private IInteractable currentInteractable;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            currentInteractable = interactable;
            Debug.Log("Рядом есть объект для взаимодействия");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (interactable == currentInteractable)
            {
                currentInteractable = null;
                Debug.Log("Объект взаимодействия потерян");
            }
        }
    }

    public void OnInteract(InputValue value)
    {
        Debug.Log("Нажата кнопка Interact");

        if (!value.isPressed)
            return;

        if (currentInteractable != null)
        {
            currentInteractable.Interact();
        }
        else
        {
            Debug.Log("Рядом нет объекта для взаимодействия");
        }
    }
}