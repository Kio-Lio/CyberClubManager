using UnityEngine;
using UnityEngine.UI;

public sealed class ManagerSelectionPanel : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.012f, 0.038f, 0.075f, 0.97f);
    private static readonly Color CyanColor =
        new(0.05f, 0.70f, 1f, 1f);

    private ManagerModeController managerMode;
    private PC selectedPC;
    private GameObject panelRoot;
    private Text titleText;
    private Text detailsText;
    private Text actionText;
    private Button actionButton;
    private GameObject managementRow;
    private Text moveText;
    private Text sellText;
    private Button moveButton;
    private Button sellButton;
    private PC saleConfirmationPC;

    private void Awake()
    {
        BuildPanel();
        panelRoot.SetActive(false);
    }

    private void Start()
    {
        managerMode = FindAnyObjectByType<ManagerModeController>();
        if (managerMode == null)
        {
            return;
        }

        managerMode.SelectionChanged += OnSelectionChanged;
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged += OnMoneyChanged;
        }
        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged += Refresh;
        }
        OnSelectionChanged(managerMode.SelectedBehaviour);
    }

    private void Update()
    {
        if (panelRoot == null || managerMode == null)
        {
            return;
        }

        bool buildModeActive = ManagerBuildController.Instance != null &&
            ManagerBuildController.Instance.IsPlacing;
        bool hudVisible = ClubHUDCanvas.Instance == null ||
            ClubHUDCanvas.Instance.CurrentMode != ClubHUDMode.Hidden;
        bool visible = managerMode.SelectedBehaviour != null &&
            hudVisible && !GameplayInputState.IsBlocked && !buildModeActive;
        panelRoot.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (managerMode != null)
        {
            managerMode.SelectionChanged -= OnSelectionChanged;
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.MoneyChanged -= OnMoneyChanged;
        }
        if (PCExpansionManager.Instance != null)
        {
            PCExpansionManager.Instance.StatusChanged -= Refresh;
        }

        UnsubscribeFromPC();
    }

    private void OnSelectionChanged(MonoBehaviour selected)
    {
        saleConfirmationPC = null;
        UnsubscribeFromPC();
        selectedPC = selected as PC;

        if (selectedPC != null)
        {
            selectedPC.StateChanged += OnPCStateChanged;
            selectedPC.TierChanged += OnPCTierChanged;
            selectedPC.EquipmentChanged += Refresh;
        }

        Refresh();
    }

    private void UnsubscribeFromPC()
    {
        if (selectedPC == null)
        {
            return;
        }

        selectedPC.StateChanged -= OnPCStateChanged;
        selectedPC.TierChanged -= OnPCTierChanged;
        selectedPC.EquipmentChanged -= Refresh;
        selectedPC = null;
    }

    private void OnPCStateChanged(PCState state)
    {
        Refresh();
    }

    private void OnPCTierChanged(PCTier tier)
    {
        Refresh();
    }

    private void OnMoneyChanged(int money)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (managerMode == null || panelRoot == null)
        {
            return;
        }

        MonoBehaviour selected = managerMode.SelectedBehaviour;
        if (selected == null)
        {
            panelRoot.SetActive(false);
            return;
        }

        panelRoot.SetActive(true);
        titleText.text = GetDisplayName(selected);
        managementRow.SetActive(false);

        if (selected is PC pc)
        {
            RefreshPC(pc);
            return;
        }

        detailsText.text = selected is IInteractable interactable
            ? CleanPrompt(interactable.GetInteractionPrompt())
            : string.Empty;

        if (selected is PCExpansionTerminal)
        {
            PCExpansionManager expansion = PCExpansionManager.Instance;
            actionText.text = "РАЗМЕСТИТЬ ПК";
            actionButton.interactable = expansion != null &&
                expansion.HasAvailableSlot && EconomyManager.Instance != null &&
                EconomyManager.Instance.Money >= expansion.PurchaseCost;
            return;
        }

        actionText.text = "ОТКРЫТЬ";
        actionButton.interactable = selected is IInteractable;
    }

    private void RefreshPC(PC pc)
    {
        RefreshPCManagement(pc);

        string state = pc.State switch
        {
            PCState.Free => pc.IsReserved ? "Зарезервирован" : "Свободен",
            PCState.Occupied => "Занят",
            PCState.Broken => "Сломан",
            _ => pc.State.ToString()
        };

        detailsText.text =
            $"{state}  ·  {pc.GetTierDisplayName()}\n" +
            $"Сессия: {pc.CurrentSessionPrice:N0} ₽  ·  " +
            $"Периферия: {pc.LowestEquipmentCondition:F0}%";

        if (pc.IsBroken)
        {
            actionText.text = "ОТРЕМОНТИРОВАТЬ";
            actionButton.interactable = pc.CanServiceEquipment;
            return;
        }

        if (pc.LowestEquipmentCondition < 100f)
        {
            actionText.text = "ОБСЛУЖИТЬ";
            actionButton.interactable = pc.CanServiceEquipment;
            return;
        }

        if (!pc.IsFree || pc.IsReserved)
        {
            actionText.text = "НЕДОСТУПНО";
            actionButton.interactable = false;
            return;
        }

        if (pc.CanUpgrade)
        {
            actionText.text = $"УЛУЧШИТЬ · {pc.NextUpgradeCost:N0} ₽";
            actionButton.interactable = EconomyManager.Instance != null &&
                EconomyManager.Instance.Money >= pc.NextUpgradeCost;
            return;
        }

        actionText.text = "МАКСИМАЛЬНЫЙ КЛАСС";
        actionButton.interactable = false;
    }

    private void RefreshPCManagement(PC pc)
    {
        PCExpansionManager expansion = PCExpansionManager.Instance;
        bool isExpansionPC = expansion != null && expansion.IsExpansionPC(pc);
        managementRow.SetActive(isExpansionPC);
        if (!isExpansionPC)
        {
            saleConfirmationPC = null;
            return;
        }

        bool canMove = expansion.CanMovePC(pc);
        moveText.text = "ПЕРЕМЕСТИТЬ";
        moveButton.interactable = canMove &&
            ManagerBuildController.Instance != null;

        bool awaitingConfirmation = saleConfirmationPC == pc;
        sellText.text = awaitingConfirmation
            ? "ПОДТВЕРДИТЬ ПРОДАЖУ"
            : $"ПРОДАТЬ · {expansion.SaleRefund:N0} ₽";
        sellButton.interactable = expansion.CanSellPC(pc);
    }

    private void BuildPanel()
    {
        panelRoot = new GameObject(
            "ManagerSelectionPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(Outline)
        );
        panelRoot.transform.SetParent(transform, false);

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 92f);
        panelRect.sizeDelta = new Vector2(410f, 286f);

        panelRoot.GetComponent<Image>().color = PanelColor;
        Outline outline = panelRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.05f, 0.70f, 1f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout =
            panelRoot.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject header = CreateHorizontalRow(panelRoot.transform, 42f);
        titleText = CreateText(header.transform, 25, FontStyle.Bold);
        titleText.color = Color.white;
        titleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Button closeButton = CreateButton(header.transform, "×", 42f);
        closeButton.onClick.AddListener(() => managerMode?.ClearSelection());

        detailsText = CreateText(panelRoot.transform, 18, FontStyle.Normal);
        detailsText.color = new Color(0.72f, 0.84f, 0.94f, 1f);
        LayoutElement detailsLayout =
            detailsText.gameObject.AddComponent<LayoutElement>();
        detailsLayout.preferredHeight = 82f;

        actionButton = CreateButton(panelRoot.transform, string.Empty, 58f);
        actionText = actionButton.GetComponentInChildren<Text>();
        actionText.fontSize = 19;
        actionText.fontStyle = FontStyle.Bold;
        actionButton.onClick.AddListener(() => managerMode?.InteractSelected());

        managementRow = CreateHorizontalRow(panelRoot.transform, 52f);
        moveButton = CreateButton(managementRow.transform, string.Empty, 52f);
        moveText = moveButton.GetComponentInChildren<Text>();
        moveText.fontSize = 16;
        moveText.fontStyle = FontStyle.Bold;
        moveButton.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        moveButton.onClick.AddListener(MoveSelectedPC);

        sellButton = CreateButton(managementRow.transform, string.Empty, 52f);
        sellText = sellButton.GetComponentInChildren<Text>();
        sellText.fontSize = 15;
        sellText.fontStyle = FontStyle.Bold;
        sellButton.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
        sellButton.onClick.AddListener(SellSelectedPC);
    }

    private void MoveSelectedPC()
    {
        PC pc = selectedPC;
        saleConfirmationPC = null;
        if (pc == null || ManagerBuildController.Instance == null ||
            !ManagerBuildController.Instance.BeginPCMove(pc))
        {
            Refresh();
        }
    }

    private void SellSelectedPC()
    {
        PC pc = selectedPC;
        PCExpansionManager expansion = PCExpansionManager.Instance;
        if (pc == null || expansion == null || !expansion.CanSellPC(pc))
        {
            saleConfirmationPC = null;
            Refresh();
            return;
        }

        if (saleConfirmationPC != pc)
        {
            saleConfirmationPC = pc;
            Refresh();
            return;
        }

        saleConfirmationPC = null;
        managerMode?.ClearSelection();
        if (!expansion.TrySellPC(pc))
        {
            managerMode?.SelectBehaviour(pc);
        }
    }

    private static GameObject CreateHorizontalRow(
        Transform parent,
        float height)
    {
        GameObject row = new GameObject(
            "Header",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = height;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return row;
    }

    private static Text CreateText(
        Transform parent,
        int fontSize,
        FontStyle fontStyle)
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
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string label,
        float height)
    {
        GameObject buttonObject = new GameObject(
            "Button",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );
        buttonObject.transform.SetParent(parent, false);
        LayoutElement buttonLayout = buttonObject.GetComponent<LayoutElement>();
        buttonLayout.preferredHeight = height;
        buttonLayout.minWidth = height;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.025f, 0.20f, 0.34f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.025f, 0.20f, 0.34f, 1f);
        colors.highlightedColor = new Color(0.025f, 0.42f, 0.66f, 1f);
        colors.pressedColor = CyanColor;
        colors.disabledColor = new Color(0.10f, 0.14f, 0.19f, 0.85f);
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, 22, FontStyle.Normal);
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        return button;
    }

    private static string GetDisplayName(MonoBehaviour selected)
    {
        return selected switch
        {
            PC => selected.name,
            PCExpansionTerminal => "СТРОИТЕЛЬСТВО",
            PCMaintenanceTerminal => "ОБСЛУЖИВАНИЕ",
            PricingTerminal => "ТАРИФЫ",
            ConsumableStockTerminal => "СКЛАД",
            MarketingTerminal => "МАРКЕТИНГ",
            InternetProviderTerminal => "ИНТЕРНЕТ",
            ClubResearchTerminal => "ИССЛЕДОВАНИЯ",
            RoomDoor => "ПОМЕЩЕНИЕ",
            TrashItem => "МУСОР",
            _ => selected.name
        };
    }

    private static string CleanPrompt(string prompt)
    {
        return (prompt ?? string.Empty)
            .Replace("E - ", string.Empty)
            .Replace("E — ", string.Empty);
    }
}
