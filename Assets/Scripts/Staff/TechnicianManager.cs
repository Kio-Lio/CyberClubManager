using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TechnicianManager : MonoBehaviour
{
    public static TechnicianManager Instance { get; private set; }

    [Header("Hiring")]
    [SerializeField, Min(0)] private int hireCost = 2000;
    [SerializeField, Min(0)] private int dailySalary = 250;

    [Header("Automatic Service")]
    [SerializeField, Min(0.5f)] private float serviceInterval = 5f;
    [SerializeField, Range(0f, 100f)] private float serviceThreshold = 20f;

    private readonly List<PC> pcs = new();
    private bool technicianHired;
    private float serviceTimer;
    private string lastServiceMessage = "Техник не нанят.";

    public bool TechnicianHired => technicianHired;
    public int HireCost => hireCost;
    public int DailySalary => dailySalary;
    public float ServiceThreshold => serviceThreshold;
    public string LastServiceMessage => lastServiceMessage;

    public event Action StatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        PC.PCRegistered += RegisterPC;
        PC.PCUnregistered += UnregisterPC;

        foreach (PC pc in FindObjectsByType<PC>())
        {
            RegisterPC(pc);
        }

        serviceTimer = serviceInterval;
    }

    private void Update()
    {
        if (!technicianHired)
        {
            return;
        }

        serviceTimer -= Time.deltaTime;
        if (serviceTimer > 0f)
        {
            return;
        }

        serviceTimer = serviceInterval;
        TryServiceCriticalEquipment();
    }

    private void OnDestroy()
    {
        PC.PCRegistered -= RegisterPC;
        PC.PCUnregistered -= UnregisterPC;
        pcs.Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryHireTechnician()
    {
        if (technicianHired)
        {
            return false;
        }

        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.SpendMoney(
            hireCost,
            EconomyTransactionCategory.StaffHire
        ))
        {
            lastServiceMessage = $"Для найма техника нужно {hireCost} ₽.";
            StatusChanged?.Invoke();
            return false;
        }

        technicianHired = true;
        serviceTimer = 0f;
        lastServiceMessage = $"Техник нанят. Зарплата: {dailySalary} ₽ в день.";
        Debug.Log(lastServiceMessage);
        StatusChanged?.Invoke();
        return true;
    }

    public int GetDailyOperatingCost()
    {
        return technicianHired ? dailySalary : 0;
    }

    public void RestoreState(bool savedTechnicianHired)
    {
        technicianHired = savedTechnicianHired;
        serviceTimer = serviceInterval;
        lastServiceMessage = technicianHired
            ? $"Техник работает. Зарплата: {dailySalary} ₽ в день."
            : "Техник не нанят.";
        StatusChanged?.Invoke();
    }

    [ContextMenu("Service Critical Equipment")]
    public void TryServiceCriticalEquipment()
    {
        if (!technicianHired)
        {
            return;
        }

        pcs.RemoveAll(pc => pc == null);
        PC targetPC = null;
        float lowestCondition = float.MaxValue;

        foreach (PC pc in pcs)
        {
            if (pc == null || !pc.CanServiceEquipment ||
                pc.MostDamagedEquipmentCondition > serviceThreshold ||
                pc.MostDamagedEquipmentCondition >= lowestCondition)
            {
                continue;
            }

            targetPC = pc;
            lowestCondition = pc.MostDamagedEquipmentCondition;
        }

        if (targetPC == null)
        {
            return;
        }

        PCEquipmentType equipmentType = targetPC.MostDamagedEquipmentType;
        PCEquipmentCondition equipment = targetPC.GetEquipment(equipmentType);
        if (equipment == null)
        {
            return;
        }

        int repairCost = equipment.RepairCost;
        if (!targetPC.TryRepairEquipment(equipmentType))
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy != null && economy.Money < repairCost)
            {
                lastServiceMessage =
                    $"Техник не смог отремонтировать {targetPC.name}: " +
                    $"нужно {repairCost} ₽.";
                StatusChanged?.Invoke();
            }

            return;
        }

        lastServiceMessage =
            $"Техник обслужил {targetPC.name}: " +
            $"{GetEquipmentDisplayName(equipmentType)} - {repairCost} ₽.";
        Debug.Log(lastServiceMessage);
        StatusChanged?.Invoke();
    }

    private void RegisterPC(PC pc)
    {
        if (pc != null && !pcs.Contains(pc))
        {
            pcs.Add(pc);
        }
    }

    private void UnregisterPC(PC pc)
    {
        if (pc != null)
        {
            pcs.Remove(pc);
        }
    }

    private static string GetEquipmentDisplayName(PCEquipmentType equipmentType)
    {
        return equipmentType switch
        {
            PCEquipmentType.Keyboard => "клавиатура",
            PCEquipmentType.Mouse => "мышь",
            PCEquipmentType.Chair => "кресло",
            _ => "оборудование"
        };
    }

    private void OnValidate()
    {
        hireCost = Mathf.Max(0, hireCost);
        dailySalary = Mathf.Max(0, dailySalary);
        serviceInterval = Mathf.Max(0.5f, serviceInterval);
        serviceThreshold = Mathf.Clamp(serviceThreshold, 0f, 100f);
    }
}
