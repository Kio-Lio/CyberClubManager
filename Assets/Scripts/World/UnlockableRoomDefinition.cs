using System;
using UnityEngine;

[Serializable]
public sealed class UnlockableRoomDefinition
{
    [Header("Identity")]
    public string roomId;
    public string displayName;

    [Header("Unlock")]
    [Min(1)] public int requiredClubLevel = 1;
    [Min(0)] public int unlockCost;

    [Header("Geometry")]
    public Vector2 center;
    public Vector2 size = new Vector2(4f, 3f);
    public Vector2 doorPosition;

    [Header("Computers")]
    public string[] pcNames;
    public Vector2[] pcPositions;
    public Vector2[] approachPositions;
    public PCTier startingTier = PCTier.Basic;

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            error = "Room ID не задан.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = $"{roomId}: Display Name не задан.";
            return false;
        }

        if (pcNames == null ||
            pcPositions == null ||
            approachPositions == null)
        {
            error = $"{roomId}: массивы ПК не заданы.";
            return false;
        }

        if (pcNames.Length == 0 ||
            pcNames.Length != pcPositions.Length ||
            pcNames.Length != approachPositions.Length)
        {
            error =
                $"{roomId}: количество имен, позиций ПК " +
                "и точек подхода не совпадает.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
