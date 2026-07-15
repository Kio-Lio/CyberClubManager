using System.Collections.Generic;
using UnityEngine;

public sealed class UnlockableRoomRuntimeBuilder : MonoBehaviour
{
    private readonly Dictionary<string, RoomDoor> createdDoors = new();
    private IReadOnlyList<UnlockableRoomDefinition> configuredRooms;

    public void BuildRooms(
        IReadOnlyList<UnlockableRoomDefinition> rooms,
        ClientNavigationManager navigation,
        RoomUnlockManager roomManager,
        Sprite squareSprite)
    {
        if (rooms == null || navigation == null ||
            roomManager == null || squareSprite == null)
        {
            return;
        }

        configuredRooms = rooms;

        foreach (UnlockableRoomDefinition room in rooms)
        {
            if (room == null)
            {
                continue;
            }

            if (!room.IsValid(out string error))
            {
                Debug.LogWarning(error);
                continue;
            }

            BuildRoom(room, navigation, roomManager, squareSprite);
        }
    }

    public void ReconnectNavigation(ClientNavigationManager navigation)
    {
        if (configuredRooms == null || navigation == null)
        {
            return;
        }

        foreach (UnlockableRoomDefinition room in configuredRooms)
        {
            if (room == null || !room.IsValid(out _))
            {
                continue;
            }

            ClientNavigationNode doorNode = navigation.EnsureRoomNode(
                $"{room.roomId}_DoorNode",
                room.doorPosition
            );
            ClientNavigationNode centerNode = navigation.EnsureRoomNode(
                $"{room.roomId}_CenterNode",
                room.center
            );

            doorNode.AddNeighbour(centerNode);
            ConnectDoorToMainGraph(room.roomId, doorNode, navigation);

            for (int index = 0; index < room.pcNames.Length; index++)
            {
                ClientNavigationNode approachNode = navigation.EnsureRoomNode(
                    $"{room.pcNames[index]}_ApproachNode",
                    room.approachPositions[index]
                );
                centerNode.AddNeighbour(approachNode);
            }
        }
    }

    private void BuildRoom(
        UnlockableRoomDefinition room,
        ClientNavigationManager navigation,
        RoomUnlockManager roomManager,
        Sprite squareSprite)
    {
        ClientNavigationNode doorNode = navigation.EnsureRoomNode(
            $"{room.roomId}_DoorNode",
            room.doorPosition
        );
        ClientNavigationNode centerNode = navigation.EnsureRoomNode(
            $"{room.roomId}_CenterNode",
            room.center
        );

        RoomDoor door = FindExistingDoor(room.roomId, roomManager);
        if (door == null)
        {
            door = CreateDoor(room, doorNode, squareSprite);
        }

        door.Configure(
            room.roomId,
            room.displayName,
            room.requiredClubLevel,
            room.unlockCost,
            doorNode
        );
        roomManager.RegisterDoor(door);
        createdDoors[room.roomId] = door;

        doorNode.AddNeighbour(centerNode);
        ConnectDoorToMainGraph(room.roomId, doorNode, navigation);

        for (int index = 0; index < room.pcNames.Length; index++)
        {
            CreateOrConfigurePC(
                room,
                index,
                door,
                centerNode,
                navigation,
                squareSprite
            );
        }
    }

    private RoomDoor FindExistingDoor(
        string roomId,
        RoomUnlockManager roomManager)
    {
        if (createdDoors.TryGetValue(roomId, out RoomDoor createdDoor) &&
            createdDoor != null)
        {
            return createdDoor;
        }

        RoomDoor registeredDoor = roomManager.FindDoor(roomId);
        if (registeredDoor != null)
        {
            return registeredDoor;
        }

        foreach (RoomDoor door in FindObjectsByType<RoomDoor>())
        {
            if (door != null && door.DoorId == roomId)
            {
                return door;
            }
        }

        return null;
    }

    private static RoomDoor CreateDoor(
        UnlockableRoomDefinition room,
        ClientNavigationNode doorNode,
        Sprite squareSprite)
    {
        GameObject doorObject = new GameObject($"{room.roomId}_Door");
        doorObject.transform.position = room.doorPosition;
        doorObject.transform.localScale = GetDoorScale(room);

        SpriteRenderer renderer = doorObject.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        YSortRenderer.SetSortingLayer(renderer, "World");

        doorObject.AddComponent<BoxCollider2D>();
        RoomDoor door = doorObject.AddComponent<RoomDoor>();
        door.Configure(
            room.roomId,
            room.displayName,
            room.requiredClubLevel,
            room.unlockCost,
            doorNode
        );

        return door;
    }

    private static Vector3 GetDoorScale(UnlockableRoomDefinition room)
    {
        Vector2 offset = room.doorPosition - room.center;
        bool verticalDoor = Mathf.Abs(offset.x) > Mathf.Abs(offset.y);

        return verticalDoor
            ? new Vector3(0.35f, 1.2f, 1f)
            : new Vector3(1.2f, 0.35f, 1f);
    }

    private static void CreateOrConfigurePC(
        UnlockableRoomDefinition room,
        int index,
        RoomDoor door,
        ClientNavigationNode centerNode,
        ClientNavigationManager navigation,
        Sprite squareSprite)
    {
        string pcName = room.pcNames[index];
        GameObject pcObject = GameObject.Find(pcName);
        bool newlyCreated = pcObject == null;

        if (newlyCreated)
        {
            pcObject = new GameObject(pcName);
            pcObject.transform.localScale = Vector3.one;
        }

        pcObject.transform.position = room.pcPositions[index];
        EnsurePCVisuals(pcObject, squareSprite);

        PC pc = pcObject.GetComponent<PC>() ?? pcObject.AddComponent<PC>();
        ClientNavigationNode approachNode = navigation.EnsureRoomNode(
            $"{pcName}_ApproachNode",
            room.approachPositions[index]
        );

        centerNode.AddNeighbour(approachNode);
        pc.SetApproachNode(approachNode);
        pc.SetRequiredRoomDoor(door);
        pc.ConfigureYSorting();

        if (newlyCreated)
        {
            pc.RestoreTier(room.startingTier);
        }
    }

    private static void ConnectDoorToMainGraph(
        string roomId,
        ClientNavigationNode doorNode,
        ClientNavigationManager navigation)
    {
        ClientNavigationNode mainConnection = roomId switch
        {
            "PrivateRoom01" => navigation.MainAisleRight,
            "VIPRoom01" => navigation.LowerAisleRight,
            _ => null
        };

        if (mainConnection != null)
        {
            doorNode.AddNeighbour(mainConnection);
        }
    }

    private static void EnsurePCVisuals(
        GameObject pcObject,
        Sprite squareSprite)
    {
        SpriteRenderer renderer = pcObject.GetComponent<SpriteRenderer>();

        // Unity components can be managed references after their native object is gone.
        // The Unity null comparison catches that case before accessing the renderer.
        if (renderer == null)
        {
            renderer = pcObject.AddComponent<SpriteRenderer>();
        }

        if (renderer.sprite == null)
        {
            renderer.sprite = squareSprite;
        }

        BoxCollider2D collider = pcObject.GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            collider = pcObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = false;
    }
}
