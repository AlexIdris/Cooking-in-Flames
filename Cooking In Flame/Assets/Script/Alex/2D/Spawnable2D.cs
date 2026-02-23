using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Spawnable2D : MonoBehaviour
{
    [Header("Spawn Prefab (What to create)")]
    public GameObject spawnPrefab;  // Drag sliced tomato prefab here!

    [Header("Hover Glow")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.6f, 1f, 1f, 1f);  // Cyan glow
    public float hoverScale = 1.15f;

    [Header("Spawn Settings")]
    public LayerMask spawnLayer = -1;  // Layers where spawning is allowed (e.g., tables)
    public float spawnOffset = 0.1f;   // Slight offset above surface if needed

    private SpriteRenderer spriteRend;
    private Collider2D myCollider;
    private Vector3 originalScale;
    private bool isSpawning = false;   // Prevents multiple spawns during one hold

    void Awake()
    {
        spriteRend = GetComponent<SpriteRenderer>();
        myCollider = GetComponent<Collider2D>();
        originalScale = transform.localScale;
    }

    void Start()
    {
        ResetVisuals();
    }

    void Update()
    {
        // Only respond to input if cursor is hovering this object
        if (!IsHovered()) return;

        // Hold LMB → Spawn once
        if (Mouse.current.leftButton.isPressed && !isSpawning)
        {
            SpawnPrefab();
        }

        // Release LMB → Reset spawn flag
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isSpawning = false;
        }
    }

    bool IsHovered()
    {
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        return myCollider.OverlapPoint(mouseWorld);
    }

    void SpawnPrefab()
    {
        isSpawning = true;  // Lock – only one spawn per hold

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        // Optional: Raycast down to surface (e.g., table)
        RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.down, 2f, spawnLayer);
        Vector2 spawnPos = hit.collider != null ? hit.point + Vector2.up * spawnOffset : mouseWorld;

        // Instantiate single prefab at cursor pos
        Instantiate(spawnPrefab, spawnPos, Quaternion.identity);

        ResetVisuals();  // Brief flash or keep glow during hold
        Debug.Log($"Spawned {spawnPrefab.name} at cursor position!");
    }

    public void SetHovered(bool hovered)
    {
        if (hovered)
        {
            spriteRend.color = hoverColor;
            transform.localScale = originalScale * hoverScale;
        }
        else
        {
            ResetVisuals();
        }
    }

    private void ResetVisuals()
    {
        spriteRend.color = normalColor;
        transform.localScale = originalScale;
    }
}