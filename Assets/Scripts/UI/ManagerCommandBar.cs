using UnityEngine;
using UnityEngine.UI;

public sealed class ManagerCommandBar : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.012f, 0.038f, 0.075f, 0.97f);
    private static readonly Color CyanColor =
        new(0.05f, 0.70f, 1f, 1f);
    private static readonly Color MutedColor =
        new(0.48f, 0.60f, 0.70f, 1f);

    private GameObject barRoot;
    private Text statusText;
    private Text buildButtonText;
    private Button buildButton;
    private ManagerBuildController buildController;

    public bool CanPurchasePC
    {
        get
        {
            PCExpansionManager expansion = PCExpansionManager.Instance;
            EconomyManager economy = EconomyManager.Instance;
            return expansion != null && economy != null &&
                expansion.HasAvailableSlot &&
                economy.Money >= expansion.PurchaseCost &&
                (BankruptcyManager.Instance == null ||
                 !BankruptcyManager.Instance.IsGameOver);
        }
    }

    private void Awake()
    {
        BuildBar();
        barRoot.SetActive(false);
    }

    private void Start()
    {
        buildController = ManagerBuildController.Instance ??
            FindAnyObjectByType<ManagerBuildController>();

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }

        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged += Refresh;
        }

        if (buildController != null)
        {
            buildController.StateChanged += Refresh;
        }

        Refresh();
    }

    private void Update()
    {
        bool hudVisible = ClubHUDCanvas.Instance == null ||
            ClubHUDCanvas.Instance.CurrentMode != ClubHUDMode.Hidden;
        bool placementActive = buildController != null &&
            buildController.IsPlacing;
        bool visible = hudVisible && !GameplayInputState.IsBlocked &&
            !placementActive;

        if (barRoot.activeSelf != visible)
        {
            barRoot.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        }

        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= Refresh;
        }

        if (buildController != null)
        {
            buildController.StateChanged -= Refresh;
        }
    }

    public bool TryBeginPCPlacement()
    {
        if (!CanPurchasePC || buildController == null)
        {
            Refresh();
            return false;
        }

        return buildController.BeginPCPlacement();
    }

    private void OnMoneyChanged(int money)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (statusText == null || buildButton == null)
        {
            return;
        }

        PCExpansionManager expansion = PCExpansionManager.Instance;
        EconomyManager economy = EconomyManager.Instance;
        if (expansion == null || economy == null)
        {
            statusText.text = "СТРОИТЕЛЬСТВО НЕДОСТУПНО";
            statusText.color = MutedColor;
            buildButtonText.text = "НЕТ ДАННЫХ";
            buildButton.interactable = false;
            return;
        }

        statusText.text =
            $"СТРОИТЕЛЬСТВО  ·  СЛОТЫ {expansion.RemainingSlots}/{expansion.UnlockedSlotCount}  ·  " +
            $"БАЛАНС {economy.Money:N0} ₽";
        statusText.color = Color.white;

        if (expansion.PurchasedPCCount >= expansion.TotalExpansionSlots)
        {
            buildButtonText.text = "ВСЕ ПК КУПЛЕНЫ";
        }
        else if (!expansion.HasAvailableSlot)
        {
            buildButtonText.text = "СЛЕДУЮЩИЙ СЛОТ ЗАКРЫТ";
        }
        else if (economy.Money < expansion.PurchaseCost)
        {
            buildButtonText.text = $"НУЖНО {expansion.PurchaseCost:N0} ₽";
        }
        else
        {
            buildButtonText.text = $"НОВЫЙ ПК  ·  {expansion.PurchaseCost:N0} ₽";
        }

        buildButton.interactable = CanPurchasePC;
    }

    private void BuildBar()
    {
        barRoot = new GameObject(
            "ManagerCommandBar",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(Outline)
        );
        barRoot.transform.SetParent(transform, false);

        RectTransform barRect = barRoot.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = new Vector2(0f, 22f);
        barRect.sizeDelta = new Vector2(760f, 64f);

        barRoot.GetComponent<Image>().color = PanelColor;
        Outline outline = barRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.70f, 1f, 0.66f);
        outline.effectDistance = new Vector2(2f, -2f);

        HorizontalLayoutGroup layout =
            barRoot.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 8, 8, 8);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        statusText = CreateText(barRoot.transform, 17, FontStyle.Bold);
        LayoutElement statusLayout =
            statusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.flexibleWidth = 1f;
        statusLayout.minWidth = 410f;

        buildButton = CreateButton(barRoot.transform);
        buildButtonText = buildButton.GetComponentInChildren<Text>();
        buildButton.onClick.AddListener(() => TryBeginPCPlacement());
        LayoutElement buttonLayout =
            buildButton.gameObject.GetComponent<LayoutElement>();
        buttonLayout.preferredWidth = 275f;
        buttonLayout.minWidth = 250f;
    }

    private static Text CreateText(
        Transform parent,
        int fontSize,
        FontStyle style)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(Text)
        );
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "BuildPCButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );
        buttonObject.transform.SetParent(parent, false);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.025f, 0.20f, 0.34f, 1f);
        colors.highlightedColor = new Color(0.025f, 0.42f, 0.66f, 1f);
        colors.pressedColor = CyanColor;
        colors.disabledColor = new Color(0.10f, 0.14f, 0.19f, 0.9f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, 17, FontStyle.Bold);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);
        return button;
    }
}
