using UnityEngine;

public sealed class ReputationUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 90f);

    private GUIStyle labelStyle;
    private int reputation;
    private int servedClients;
    private int lostClients;

    private void Start()
    {
        if (ClubReputationManager.Instance == null)
        {
            Debug.LogWarning("ClubReputationManager is missing. Reputation UI is disabled.");
            return;
        }

        ClubReputationManager.Instance.StatusChanged += RefreshData;
        RefreshData();
    }

    private void OnDestroy()
    {
        if (ClubReputationManager.Instance != null)
        {
            ClubReputationManager.Instance.StatusChanged -= RefreshData;
        }
    }

    private void RefreshData()
    {
        ClubReputationManager manager = ClubReputationManager.Instance;
        if (manager == null)
        {
            return;
        }

        reputation = manager.Reputation;
        servedClients = manager.ServedClients;
        lostClients = manager.LostClients;
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

        string statusText =
            $"Репутация: {reputation}/100 | " +
            $"Обслужено: {servedClients} | " +
            $"Потеряно: {lostClients}";

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y, 700f, 40f),
            statusText,
            labelStyle
        );
    }
}
