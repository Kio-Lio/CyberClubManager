using UnityEngine;

public sealed class ClubStatusUI : MonoBehaviour
{
    [SerializeField] private int fontSize = 24;
    [SerializeField] private Vector2 screenPosition = new Vector2(20f, 55f);

    private PC[] pcs;

    private int freeCount;
    private int occupiedCount;
    private int brokenCount;

    private GUIStyle labelStyle;

    private void Start()
    {
        pcs = FindObjectsByType<PC>();

        foreach (PC pc in pcs)
        {
            pc.StateChanged += OnPCStateChanged;
        }

        RecalculateCounts();
    }

    private void OnDestroy()
    {
        if (pcs == null)
        {
            return;
        }

        foreach (PC pc in pcs)
        {
            if (pc != null)
            {
                pc.StateChanged -= OnPCStateChanged;
            }
        }
    }

    private void OnPCStateChanged(PCState newState)
    {
        RecalculateCounts();
    }

    private void RecalculateCounts()
    {
        freeCount = 0;
        occupiedCount = 0;
        brokenCount = 0;

        if (pcs == null)
        {
            return;
        }

        foreach (PC pc in pcs)
        {
            if (pc == null)
            {
                continue;
            }

            switch (pc.State)
            {
                case PCState.Free:
                    freeCount++;
                    break;

                case PCState.Occupied:
                    occupiedCount++;
                    break;

                case PCState.Broken:
                    brokenCount++;
                    break;
            }
        }
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
            $"Свободно: {freeCount} | " +
            $"Занято: {occupiedCount} | " +
            $"Сломано: {brokenCount}";

        GUI.Label(
            new Rect(screenPosition.x, screenPosition.y, 600f, 40f),
            statusText,
            labelStyle
        );
    }
}
