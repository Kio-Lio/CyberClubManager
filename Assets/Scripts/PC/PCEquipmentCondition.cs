using System;
using UnityEngine;

[Serializable]
public sealed class PCEquipmentCondition
{
    [SerializeField] private PCEquipmentType equipmentType;
    [SerializeField, Range(0f, 100f)] private float condition = 100f;
    [SerializeField, Min(0)] private int repairCost;

    public PCEquipmentType EquipmentType => equipmentType;
    public float Condition => condition;
    public int RepairCost => repairCost;
    public bool IsBroken => condition <= 0f;

    public PCEquipmentCondition(PCEquipmentType type, int cost)
    {
        equipmentType = type;
        repairCost = Mathf.Max(0, cost);
        condition = 100f;
    }

    public void ApplyWear(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        condition = Mathf.Clamp(condition - amount, 0f, 100f);
    }

    public void Repair()
    {
        condition = 100f;
    }

    public void RestoreCondition(float savedCondition)
    {
        condition = Mathf.Clamp(savedCondition, 0f, 100f);
    }
}
