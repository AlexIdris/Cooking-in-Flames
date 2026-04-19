using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks runtime-spawned GameObjects that are currently loose (not held by the player).
/// Call <see cref="DeleteAllLooseSpawns"/> on scene transition or round reset.
/// </summary>
public class SpawnCleanupManager : MonoBehaviour
{
    private static readonly List<GameObject> looseObjects = new List<GameObject>();

    /// <summary>Registers a freshly spawned object as loose. Call immediately after Instantiate.</summary>
    public static void RegisterSpawnedObject(GameObject obj)
    {
        if (obj != null && !looseObjects.Contains(obj))
            looseObjects.Add(obj);
    }

    /// <summary>Removes an object from the loose list while it is being held.</summary>
    public static void MarkAsHeld(GameObject obj)
    {
        if (obj != null) looseObjects.Remove(obj);
    }

    /// <summary>Returns an object to the loose list after it has been dropped.</summary>
    public static void MarkAsDropped(GameObject obj)
    {
        if (obj != null && !looseObjects.Contains(obj))
            looseObjects.Add(obj);
    }

    /// <summary>
    /// Returns true if <paramref name="obj"/> is currently registered as a loose
    /// spawned object. Used by IngredientAnomalyCleanup to distinguish runtime-
    /// spawned ingredients (eligible for cleanup) from scene-placed static objects.
    /// </summary>
    public static bool IsRegistered(GameObject obj) =>
        obj != null && looseObjects.Contains(obj);

    /// <summary>Destroys every loose object that is not currently held, then clears the list.</summary>
    public static void DeleteAllLooseSpawns()
    {
        foreach (GameObject obj in new List<GameObject>(looseObjects))
        {
            if (obj == null) continue;
            Pickupable2D pickup = obj.GetComponent<Pickupable2D>();
            if (pickup != null && pickup.IsHeld) continue;
            Destroy(obj);
        }
        looseObjects.Clear();
    }

    void OnDestroy() => DeleteAllLooseSpawns();
}