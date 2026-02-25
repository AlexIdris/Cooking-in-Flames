using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TrashCan2D : MonoBehaviour
{
    [Header("Trash Can Settings")]
    [Tooltip("Should the trash can destroy EVERYTHING that enters? (including player, UI, etc.)")]
    public bool destroyEverything = false;

    [Tooltip("If not destroying everything, only destroy objects on these layers")]
    public LayerMask layersToDelete = ~0;   // Default = all layers

    private Collider2D myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider2D>();

        if (myCollider == null)
        {
            Debug.LogError($"{name}: Missing Collider2D component!", this);
            enabled = false;
            return;
        }

        if (!myCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: Collider should be set to Is Trigger = true for trash detection.");
            myCollider.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        // Skip if the object is this trash can itself (edge case)
        if (other.gameObject == gameObject) return;

        // Option A: Destroy everything
        if (destroyEverything)
        {
            Destroy(other.gameObject);
            Debug.Log($"TrashCan {name} destroyed: {other.name}");
            return;
        }

        // Option B: Destroy only objects on allowed layers
        if (((1 << other.gameObject.layer) & layersToDelete) != 0)
        {
            Destroy(other.gameObject);
            Debug.Log($"TrashCan {name} destroyed: {other.name} (layer match)");
        }
    }

    // Optional: also support OnTriggerStay2D if objects can stay inside
    void OnTriggerStay2D(Collider2D other)
    {
        OnTriggerEnter2D(other); // Just in case something enters without triggering enter
    }
}