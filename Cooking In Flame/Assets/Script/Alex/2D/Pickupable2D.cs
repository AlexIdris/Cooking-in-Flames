using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Pickupable2D : MonoBehaviour
{
    [Header("Hover Feedback")]
    public float hoverScale = 1.15f;

    [Header("Glow Child (must be child named 'Glow')")]
    public GameObject glowObject;   // Drag the Glow child here

    private Vector3 originalScale;
    private bool isHeld = false;

    void Awake()
    {
        originalScale = transform.localScale;

        // Hide glow by default
        if (glowObject != null)
            glowObject.SetActive(false);
    }

    void Start()
    {
        // Make sure glow starts off
        SetHovered(false);
    }

    public bool CanBePickedUp()
    {
        return !isHeld;
    }

    public void OnPickup()
    {
        isHeld = true;
        SetHovered(false); // No glow while held
    }

    public void OnDrop()
    {
        isHeld = false;
    }

    public void SetHovered(bool hovered)
    {
        // Scale the whole object slightly
        transform.localScale = hovered ? originalScale * hoverScale : originalScale;

        // Glow is controlled by child object visibility
        if (glowObject != null)
        {
            glowObject.SetActive(hovered && !isHeld);
        }
    }
}