using UnityEngine;
using System.Collections;           // ← This fixes the IEnumerator error

[RequireComponent(typeof(SpriteRenderer))]
public class Pickupable2D : MonoBehaviour
{
    [Header("Hover Glow")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.6f, 1f, 1f, 1f);
    public float hoverScale = 1.15f;
    [Range(4f, 20f)]
    public float smoothSpeed = 10f;

    [Header("Auto-Destroy After Placement")]
    [Tooltip("Time in seconds before this object deletes itself after being dropped/placed")]
    [Range(0.1f, 60f)]
    public float destroyAfterPlaced = 3f;

    private SpriteRenderer spriteRend;
    private Vector3 originalScale;
    private bool isHeld = false;
    private bool targetHovered = false;

    void Awake()
    {
        spriteRend = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    void Start()
    {
        ResetVisuals();
    }

    void Update()
    {
        SmoothTransition();
    }

    public bool CanBePickedUp()
    {
        return !isHeld;
    }

    public void OnPickup()
    {
        isHeld = true;
        SetTargetHovered(false);
    }

    public void OnDrop()
    {
        isHeld = false;
        ResetVisuals();

        // Start self-destruct timer when dropped/placed
        if (destroyAfterPlaced > 0f)
        {
            StartCoroutine(DestroyAfterDelay(destroyAfterPlaced));
        }
    }

    public void SetHovered(bool hovered)
    {
        SetTargetHovered(hovered && !isHeld);
    }

    private void SetTargetHovered(bool hovered)
    {
        targetHovered = hovered;
    }

    private void SmoothTransition()
    {
        if (spriteRend == null) return;

        Color targetC = targetHovered ? hoverColor : normalColor;
        spriteRend.color = Color.Lerp(spriteRend.color, targetC, smoothSpeed * Time.deltaTime);

        Vector3 targetS = targetHovered ? originalScale * hoverScale : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetS, smoothSpeed * Time.deltaTime);
    }

    private void ResetVisuals()
    {
        if (spriteRend != null)
        {
            spriteRend.color = normalColor;
            transform.localScale = originalScale;
        }
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
        Debug.Log($"{name} auto-destroyed after {delay}s (placed down)");
    }
}