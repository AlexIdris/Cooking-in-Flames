using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a machine or dispenser. On hover + LMB click, instantiates a prefab at
/// the cursor position and immediately hands it to PlayerHand2D for carrying.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Spawnable2D : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Prefab to spawn. Should have a Pickupable2D component.")]
    public GameObject spawnPrefab;
    [Tooltip("True = one spawn per click. False = continuous while LMB held.")]
    public bool  spawnOnSingleClick = true;
    [Tooltip("Z depth of the spawn plane (usually 0).")]
    public float spawnPlaneZ = 0f;

    [Header("Cooldown")]
    [Range(0.1f, 10f)]
    [Tooltip("Seconds before the next spawn is allowed.")]
    public float cooldownDuration = 1.5f;

    [Header("Hover Glow")]
    public Color normalColor = Color.white;
    public Color hoverColor  = new Color(0.6f, 1f, 1f, 1f);
    public float hoverScale  = 1.15f;
    [Range(4f, 20f)] public float smoothSpeed = 10f;

    private Collider2D     col;
    private SpriteRenderer spriteRend;
    private Vector3        originalScale;
    private PlayerHand2D   playerHand;

    private bool  isOnCooldown;
    private bool  hasSpawnedThisInteraction;
    private float cooldownTimer;

    void Awake()
    {
        col           = GetComponent<Collider2D>();
        spriteRend    = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    void Start()
    {
        ResetVisuals();
        playerHand = FindObjectOfType<PlayerHand2D>();
        if (playerHand == null)
            Debug.LogWarning($"[Spawnable2D] {name}: No PlayerHand2D found — items won't auto-pickup.", this);
    }

    void Update()
    {
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f) { isOnCooldown = false; hasSpawnedThisInteraction = false; }
        }

        // Interaction is locked until ShopToggle opens the shop
        bool shopOpen = playerHand != null && playerHand.IsInteractionEnabled;

        Vector2 mouseWorld = GetMouseWorldPosition();
        bool    hovered    = shopOpen && col.OverlapPoint(mouseWorld);
        bool    canGlow    = spawnPrefab != null && hovered && !isOnCooldown && !hasSpawnedThisInteraction;

        SmoothTransition(canGlow);

        if (!canGlow) { hasSpawnedThisInteraction = false; return; }

        bool shouldSpawn = spawnOnSingleClick
            ? Mouse.current.leftButton.wasPressedThisFrame
            : Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame;

        if (shouldSpawn) SpawnAndPickUp(mouseWorld);
    }

    private void SpawnAndPickUp(Vector2 position)
    {
        // Block spawn if the player is already holding an item — they must drop
        // their current ingredient before picking up a new one from a dispenser.
        if (playerHand != null && playerHand.IsHoldingItem)
        {
            Debug.Log("[Spawnable2D] Hand is full — drop the current item before spawning a new one.");
            return;
        }

        GameObject   spawned = Instantiate(spawnPrefab, new Vector3(position.x, position.y, spawnPlaneZ), Quaternion.identity);
        Pickupable2D pickup  = spawned.GetComponent<Pickupable2D>();

        SpawnCleanupManager.RegisterSpawnedObject(spawned);

        if (pickup != null && playerHand != null)
        {
            if (!playerHand.ForcePickUp(pickup))
            {
                // ForcePickUp returned false (hand became full between the check and
                // the call — race condition safety). Destroy the spawned object so
                // it does not become a floating orphan.
                SpawnCleanupManager.MarkAsHeld(spawned);
                Destroy(spawned);
                return;
            }
        }
        else if (pickup == null)
        {
            Debug.LogWarning($"[Spawnable2D] '{spawnPrefab.name}' has no Pickupable2D.", this);
        }

        hasSpawnedThisInteraction = true;
        isOnCooldown              = true;
        cooldownTimer             = cooldownDuration;
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        Ray     ray    = Camera.main.ScreenPointToRay(screen);
        Plane   plane  = new Plane(Vector3.forward, new Vector3(0f, 0f, spawnPlaneZ));
        if (plane.Raycast(ray, out float enter)) return ray.GetPoint(enter);
        Vector3 fb = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Camera.main.nearClipPlane));
        return fb;
    }

    private void SmoothTransition(bool glow)
    {
        Color   tc = glow ? hoverColor            : normalColor;
        Vector3 ts = glow ? originalScale * hoverScale : originalScale;
        spriteRend.color     = Color.Lerp(spriteRend.color,     tc, smoothSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, ts, smoothSpeed * Time.deltaTime);
    }

    private void ResetVisuals()
    {
        spriteRend.color     = normalColor;
        transform.localScale = originalScale;
    }
}