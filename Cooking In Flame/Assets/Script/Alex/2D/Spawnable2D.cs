using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Spawnable2D : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("The prefab that will be spawned when interacting with this object")]
    public GameObject spawnPrefab;

    [Tooltip("Spawn on single click (true) or once per hold (false)")]
    public bool spawnOnSingleClick = true;

    [Tooltip("Plane Z where spawning happens (usually 0 for 2D)")]
    public float spawnPlaneZ = 0f;

  
  

    [Header("Hover Glow")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.6f, 1f, 1f, 1f);
    public float hoverScale = 1.15f;
    [Range(4f, 20f)]
    public float smoothSpeed = 10f;

    private Collider2D col;
    private SpriteRenderer spriteRend;
    private Vector3 originalScale;
    private bool isHovered = false;
    private bool targetGlow = false;
    private bool hasSpawnedThisInteraction = false;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        spriteRend = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;

        if (col == null || spriteRend == null)
        {
            Debug.LogError($"{name}: Missing Collider2D or SpriteRenderer!", this);
            enabled = false;
        }
    }

    void Start()
    {
        ResetVisuals();
    }

    void Update()
    {
        Vector2 mouseWorld = GetMouseWorldPosition();
        isHovered = col.OverlapPoint(mouseWorld);

        targetGlow = spawnPrefab != null && isHovered && !hasSpawnedThisInteraction;

        SmoothTransition();

        if (!targetGlow)
        {
            hasSpawnedThisInteraction = false;
            return;
        }

        bool shouldSpawn = false;

        if (spawnOnSingleClick)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                shouldSpawn = true;
        }
        else
        {
            if (Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame)
                shouldSpawn = true;
        }

        if (shouldSpawn)
        {
            SpawnPrefab(mouseWorld);
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, spawnPlaneZ));
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Camera.main.nearClipPlane));
    }

    private void SpawnPrefab(Vector2 position)
    {
        if (spawnPrefab == null) return;

        // Spawn the object
        GameObject spawned = Instantiate(spawnPrefab, position, Quaternion.identity);
        hasSpawnedThisInteraction = true;

     
    }

    private void SmoothTransition()
    {
        if (spriteRend == null) return;

        Color targetC = targetGlow ? hoverColor : normalColor;
        spriteRend.color = Color.Lerp(spriteRend.color, targetC, smoothSpeed * Time.deltaTime);

        Vector3 targetS = targetGlow ? originalScale * hoverScale : originalScale;
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
}