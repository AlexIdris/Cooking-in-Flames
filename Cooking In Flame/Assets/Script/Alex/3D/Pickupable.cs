using UnityEngine;

public class Pickupable : MonoBehaviour
{
    [Header("Pickup Settings")]
    public bool canPickup = true;

    [Header("Inventory Icon (Drag Sprite!)")]
    public Sprite itemIcon;

    [Header("Place Settings")]
    public float placeHeightOffset = 0.15f;
    public float settleVelocity = -2f;
    public float placeDrag = 5f;
    public float placeAngularDrag = 10f;

    [Header("Glow Settings")]
    public bool useGlow = true;

    private Rigidbody rb;
    private Transform originalParent;
    private MeshRenderer meshRend;
    private Material originalMat;
    private Material glowMat;
    private Vector3 originalScale;
    private Color originalBaseColor;

    private float originalDrag;
    private float originalAngularDrag;
    private bool originalKinematic;
    private bool originalFreezeRotation;
    private int originalLayer;
    private CollisionDetectionMode originalCollisionMode;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = placeDrag;
        rb.angularDamping = placeAngularDrag;

        originalParent = transform.parent;

        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;
        originalKinematic = rb.isKinematic;
        originalFreezeRotation = rb.freezeRotation;
        originalLayer = gameObject.layer;
        originalCollisionMode = rb.collisionDetectionMode;

        if (useGlow)
        {
            meshRend = GetComponent<MeshRenderer>();
            if (meshRend != null)
            {
                originalMat = meshRend.material;
                originalScale = transform.localScale;
                originalBaseColor = originalMat.GetColor("_BaseColor");

                glowMat = new Material(originalMat);
                glowMat.SetColor("_BaseColor", originalBaseColor * 1.2f);
                glowMat.SetColor("_EmissionColor", originalBaseColor * 2f);
                glowMat.EnableKeyword("_EMISSION");
            }
        }

        if (itemIcon == null) Debug.LogWarning("Pickupable " + name + ": Assign Item Icon Sprite!");
    }

    public void Pickup()
    {
        canPickup = false;
    }

    public void Place(Vector3 worldPosition, Vector3 surfaceNormal)
    {
        transform.SetParent(originalParent);
        transform.position = worldPosition + (surfaceNormal * placeHeightOffset);
        transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-surfaceNormal, Vector3.up), surfaceNormal);
        rb.isKinematic = false;
        rb.linearVelocity = surfaceNormal * settleVelocity;
        rb.angularVelocity = Vector3.zero;
        ResetPhysics();
        Debug.Log("Placed: " + name + " (active in world!)");
    }

    public void Drop(Vector3 throwVelocity)
    {
        transform.SetParent(originalParent);
        rb.isKinematic = false;
        rb.linearVelocity = throwVelocity;
        ResetPhysics();
        Debug.Log("Dropped: " + name + " (active in world!)");
    }

    private void ResetPhysics()
    {
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        rb.isKinematic = originalKinematic;
        rb.freezeRotation = originalFreezeRotation;
        rb.collisionDetectionMode = originalCollisionMode;
        gameObject.layer = originalLayer;
        canPickup = true;
    }

    public void SetHovered(bool hovered)
    {
        if (!useGlow || meshRend == null || !canPickup) return;
        meshRend.material = hovered ? glowMat : originalMat;
        transform.localScale = hovered ? originalScale * 1.1f : originalScale;
    }
}