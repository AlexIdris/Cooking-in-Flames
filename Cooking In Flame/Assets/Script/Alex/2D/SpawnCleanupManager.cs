using UnityEngine;
using System.Collections.Generic;

public class SpawnCleanupManager : MonoBehaviour
{
    private static List<GameObject> looseSpawnedObjects = new List<GameObject>();

    // Called by Spawnable2D when spawning
    public static void RegisterSpawnedObject(GameObject spawned)
    {
        if (spawned != null && !looseSpawnedObjects.Contains(spawned))
        {
            looseSpawnedObjects.Add(spawned);
        }
    }

    // Called when picked up
    public static void MarkAsHeld(GameObject obj)
    {
        if (obj != null)
        {
            looseSpawnedObjects.Remove(obj);
        }
    }

    // Called when dropped
    public static void MarkAsDropped(GameObject obj)
    {
        if (obj != null && !looseSpawnedObjects.Contains(obj))
        {
            looseSpawnedObjects.Add(obj);
        }
    }

    // Call this to delete everything not currently held
    public static void DeleteAllLooseSpawns()
    {
        var toDelete = new List<GameObject>(looseSpawnedObjects);

        foreach (var obj in toDelete)
        {
            if (obj != null)
            {
                Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
                if (pickup == null || !pickup.isHeld)
                {
                    Destroy(obj);
                }
            }
        }

        looseSpawnedObjects.Clear();
        Debug.Log("All loose (not held) spawned objects deleted.");
    }

    // Optional: auto-clean when scene unloads or game ends
    void OnDestroy()
    {
        DeleteAllLooseSpawns();
    }
}