using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoomUnlockManager : MonoBehaviour
{
    public static RoomUnlockManager Instance { get; private set; }

    private readonly List<RoomDoor> roomDoors = new();

    public IReadOnlyList<RoomDoor> RoomDoors => roomDoors;

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
        foreach (RoomDoor door in FindObjectsByType<RoomDoor>())
        {
            RegisterDoor(door);
        }
    }

    private void OnDestroy()
    {
        foreach (RoomDoor door in roomDoors)
        {
            if (door != null)
            {
                door.StatusChanged -= OnDoorStatusChanged;
            }
        }

        roomDoors.Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterDoor(RoomDoor door)
    {
        if (door == null || roomDoors.Contains(door))
        {
            return;
        }

        roomDoors.Add(door);
        door.StatusChanged += OnDoorStatusChanged;
        StatusChanged?.Invoke();
    }

    public RoomDoor FindDoor(string doorId)
    {
        foreach (RoomDoor door in roomDoors)
        {
            if (door != null && door.DoorId == doorId)
            {
                return door;
            }
        }

        return null;
    }

    private void OnDoorStatusChanged()
    {
        StatusChanged?.Invoke();
    }
}
