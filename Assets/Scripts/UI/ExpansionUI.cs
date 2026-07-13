using UnityEngine;

public sealed class ExpansionUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 230f);

    private GUIStyle labelStyle;
    private int purchaseCost;
    private int remainingSlots;

    private void Start()
    {
        if (PCExpansionManager.Instance == null)
        {
            Debug.LogWarning(
                "PCExpansionManager не найден. Интерфейс расширения не будет работать."
            );
            return;
        }

        PCExpansionManager.Instance.StatusChanged += RefreshData;
        RefreshData();
    }

    private void OnDestroy()
    {
        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= RefreshData;
        }
    }

    private void RefreshData()
    {
        PCExpansionManager manager = PCExpansionManager.Instance;
        if (manager == null)
        {
            return;
        }

        purchaseCost = manager.PurchaseCost;
        remainingSlots = manager.RemainingSlots;
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize
            };
        }

        string text = remainingSlots > 0
            ? $"Новый ПК: {purchaseCost} ₽ | Мест для расширения: {remainingSlots}"
            : "Все места для ПК приобретены";

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y, 700f, 40f),
            text,
            labelStyle
        );
    }
}
