using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LayoutFixSmokeTest
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CyberClubSceneSetup.Apply();

            ClubLayoutBuilder.EnsureRuntimeLayout();
            ClientNavigationManager navigation =
                ClientNavigationManager.EnsureRuntimeGraph();

            PCExpansionManager expansion =
                UnityEngine.Object.FindAnyObjectByType<PCExpansionManager>();

            if (expansion == null)
            {
                throw new InvalidOperationException("PCExpansionManager is missing.");
            }

            expansion.RestorePurchasedPCs(4);
            navigation.EnsureDefaultGraph();

            RunChecks(navigation);

            Debug.Log("LAYOUT_FIX_SMOKE_TEST: PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"LAYOUT_FIX_SMOKE_TEST: FAIL - {exception}");
            EditorApplication.Exit(1);
        }
    }

    private static void RunChecks(ClientNavigationManager navigation)
    {
        AssertExpectedPCPositions();
        AssertNoRoomPCOverlap();
        AssertPCCollidersAreTriggers();
        AssertTableGapsAreWideEnough();
        AssertRoomNavigation(navigation);
        AssertNoDuplicateRuntimeObjects();
    }

    private static void AssertExpectedPCPositions()
    {
        AssertPosition("PC_06", new Vector3(6.4f, -0.7f, 0f));
        AssertPosition("PC_07", new Vector3(1.2f, -3.3f, 0f));
        AssertPosition("PC_08", new Vector3(3.8f, -3.3f, 0f));
        AssertPosition("PC_09", new Vector3(6.4f, -3.3f, 0f));
        AssertPosition("PC_10", new Vector3(11.3f, 3.55f, 0f));
        AssertPosition("PC_11", new Vector3(12.7f, 3.55f, 0f));
        AssertPosition("PC_12", new Vector3(11.3f, -1.85f, 0f));
        AssertPosition("PC_13", new Vector3(12.7f, -1.85f, 0f));
    }

    private static void AssertNoRoomPCOverlap()
    {
        for (int oldIndex = 1; oldIndex <= 9; oldIndex++)
        {
            GameObject oldPC = RequireObject($"PC_{oldIndex:00}");

            for (int roomIndex = 10; roomIndex <= 13; roomIndex++)
            {
                GameObject roomPC = RequireObject($"PC_{roomIndex:00}");
                float distance = Vector2.Distance(
                    oldPC.transform.position,
                    roomPC.transform.position
                );

                if (distance < 2.0f)
                {
                    throw new InvalidOperationException(
                        $"{oldPC.name} crowds {roomPC.name}: {distance:F2}"
                    );
                }
            }
        }
    }

    private static void AssertPCCollidersAreTriggers()
    {
        for (int index = 1; index <= 13; index++)
        {
            GameObject pcObject = RequireObject($"PC_{index:00}");
            BoxCollider2D collider = pcObject.GetComponent<BoxCollider2D>();

            if (collider == null)
            {
                throw new InvalidOperationException($"{pcObject.name} has no BoxCollider2D.");
            }

            if (!collider.isTrigger)
            {
                throw new InvalidOperationException($"{pcObject.name} collider blocks the aisle.");
            }
        }
    }

    private static void AssertTableGapsAreWideEnough()
    {
        Bounds topTable = RequireObject("PCTable_Top").GetComponent<BoxCollider2D>().bounds;
        Bounds bottomTable = RequireObject("PCTable_Bottom").GetComponent<BoxCollider2D>().bounds;
        Bounds expansionTable = RequireObject("PCTable_Expansion").GetComponent<BoxCollider2D>().bounds;

        float mainGap = topTable.min.y - bottomTable.max.y;
        float lowerGap = bottomTable.min.y - expansionTable.max.y;

        if (mainGap < 2.0f)
        {
            throw new InvalidOperationException($"Main aisle is too narrow: {mainGap:F2}");
        }

        if (lowerGap < 1.4f)
        {
            throw new InvalidOperationException($"Lower aisle is too narrow: {lowerGap:F2}");
        }
    }

    private static void AssertRoomNavigation(ClientNavigationManager navigation)
    {
        RoomDoor privateDoor = RequireDoor("PrivateRoom01");
        RoomDoor vipDoor = RequireDoor("VIPRoom01");

        AssertNoPathWhenClosed(
            navigation,
            navigation.MainAisleRight,
            RequirePC("PC_10").ApproachNode
        );

        AssertNoPathWhenClosed(
            navigation,
            navigation.LowerAisleRight,
            RequirePC("PC_12").ApproachNode
        );

        privateDoor.RestoreState(true);
        vipDoor.RestoreState(true);

        AssertPathExists(
            navigation,
            navigation.MainAisleRight,
            RequirePC("PC_10").ApproachNode,
            "private room"
        );

        AssertPathExists(
            navigation,
            navigation.LowerAisleRight,
            RequirePC("PC_12").ApproachNode,
            "VIP room"
        );
    }

    private static void AssertNoDuplicateRuntimeObjects()
    {
        AssertSingleObject("ClubLayoutBuilder");
        AssertSingleObject("ClientNavigationManager");
        AssertSingleObject("PrivateRoom01_Door");
        AssertSingleObject("VIPRoom01_Door");
        AssertSingleObject("PC_10");
        AssertSingleObject("PC_13");
    }

    private static void AssertNoPathWhenClosed(
        ClientNavigationManager navigation,
        ClientNavigationNode start,
        ClientNavigationNode destination)
    {
        List<Vector3> path = navigation.BuildPath(start, destination);

        if (path.Count > 0)
        {
            throw new InvalidOperationException("Closed room unexpectedly has a client path.");
        }
    }

    private static void AssertPathExists(
        ClientNavigationManager navigation,
        ClientNavigationNode start,
        ClientNavigationNode destination,
        string label)
    {
        List<Vector3> path = navigation.BuildPath(start, destination);

        if (path.Count == 0)
        {
            throw new InvalidOperationException($"No path found for {label}.");
        }
    }

    private static void AssertPosition(string objectName, Vector3 expected)
    {
        Vector3 actual = RequireObject(objectName).transform.position;

        if (Vector3.Distance(actual, expected) > 0.05f)
        {
            throw new InvalidOperationException(
                $"{objectName} position {actual} does not match expected {expected}. " +
                DescribeObjectsByName(objectName)
            );
        }
    }

    private static string DescribeObjectsByName(string objectName)
    {
        List<string> descriptions = new();

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            CollectObjectDescriptions(
                rootObject.transform,
                objectName,
                descriptions
            );
        }

        return descriptions.Count == 0
            ? "No matching scene objects found."
            : string.Join(" | ", descriptions);
    }

    private static void CollectObjectDescriptions(
        Transform current,
        string objectName,
        List<string> descriptions)
    {
        if (current.name == objectName)
        {
            descriptions.Add(
                $"{GetTransformPath(current)} at {current.position}"
            );
        }

        for (int index = 0; index < current.childCount; index++)
        {
            CollectObjectDescriptions(
                current.GetChild(index),
                objectName,
                descriptions
            );
        }
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    private static PC RequirePC(string objectName)
    {
        PC pc = RequireObject(objectName).GetComponent<PC>();

        if (pc == null)
        {
            throw new InvalidOperationException($"{objectName} has no PC component.");
        }

        return pc;
    }

    private static RoomDoor RequireDoor(string doorId)
    {
        RoomDoor door = RoomUnlockManager.Instance != null
            ? RoomUnlockManager.Instance.FindDoor(doorId)
            : null;

        if (door == null)
        {
            List<RoomDoor> doors = FindSceneComponents<RoomDoor>();

            foreach (RoomDoor candidate in doors)
            {
                if (candidate != null && candidate.DoorId == doorId)
                {
                    door = candidate;
                    break;
                }
            }
        }

        if (door == null)
        {
            throw new InvalidOperationException($"{doorId} door is missing.");
        }

        return door;
    }

    private static GameObject RequireObject(string objectName)
    {
        GameObject target = GameObject.Find(objectName);

        if (target == null)
        {
            throw new InvalidOperationException($"{objectName} is missing.");
        }

        return target;
    }

    private static List<T> FindSceneComponents<T>() where T : Component
    {
        List<T> components = new();

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            components.AddRange(rootObject.GetComponentsInChildren<T>(false));
        }

        return components;
    }

    private static void AssertSingleObject(string objectName)
    {
        int count = 0;

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            count += CountByName(rootObject.transform, objectName);
        }

        if (count != 1)
        {
            throw new InvalidOperationException(
                $"{objectName} expected once, found {count}."
            );
        }
    }

    private static int CountByName(Transform root, string objectName)
    {
        int count = root.name == objectName ? 1 : 0;

        for (int index = 0; index < root.childCount; index++)
        {
            count += CountByName(root.GetChild(index), objectName);
        }

        return count;
    }
}
