using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class InventoryPickupController : MonoBehaviour
{
    [Header("Pickup / Place")]
    public float pickupRange = 3f;
    public LayerMask pickupMask = -1;
    public float placementRange = 3f;
    public LayerMask placementMask = 1;

    [Header("Hold Position")]
    public Vector3 pickupPointOffset = new Vector3(0.3f, -0.3f, 1.2f);
    public float pushbackDistance = 0.1f;
    public LayerMask wallMask = -1;
    public float objectRadius = 0.3f;

    [Header("Layers")]
    public int playerLayerIndex = 8;
    public int heldLayerIndex = 10;

    [Header("UI – Crosshair")]
    public Image crosshairImage;
    public Color defaultCrosshairColor = Color.white;
    public Color hoverCrosshairColor = Color.cyan;
    public Color placeCrosshairColor = Color.green;

    [Header("UI – Inventory Slots (Drag these!)")]
    public Image[] slotFrames = new Image[3];
    public TextMeshProUGUI[] slotLabels = new TextMeshProUGUI[3];
    public Image[] slotIcons = new Image[3];

    [Header("Slot Visuals")]
    public Sprite emptyIcon;
    public Color emptyIconColor = new Color(1, 1, 1, 0.5f);
    public Color filledIconColor = Color.white;

    [Header("Inventory Colors")]
    public Color emptyColor = Color.gray;
    public Color itemColor = Color.white;
    public Color currentSlotColor = Color.yellow;

    [Header("Scroll Wheel Settings")]
    [Tooltip("How sensitive the scroll wheel is. Higher = faster switching.")]
    public float scrollSensitivity = 1f;

    [Tooltip("Minimum scroll delta needed to register a scroll step")]
    public float scrollThreshold = 0.1f;

    private Camera playerCamera;
    private Transform pickupPoint;
    private Pickupable[] slots = new Pickupable[3];
    private int currentSlotIndex = 0;
    private Pickupable currentHeldItem;
    private Pickupable hoveredObject;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("InventoryPickupController: Add Main Camera as child of Player!");
            return;
        }

        // Create pickup point
        GameObject handGO = new GameObject("PickupPoint");
        handGO.transform.SetParent(playerCamera.transform);
        pickupPoint = handGO.transform;
        pickupPoint.localPosition = pickupPointOffset;
        pickupPoint.localRotation = Quaternion.identity;

        // Exclude player & held layers from raycasts
        int exclude = (1 << playerLayerIndex) | (1 << heldLayerIndex);
        pickupMask &= ~exclude;
        placementMask &= ~exclude;
        wallMask &= ~exclude;

        if (crosshairImage != null) crosshairImage.color = defaultCrosshairColor;
        UpdateInventoryUI();

        Debug.Log("InventoryPickupController ready. Scroll sensitivity = " + scrollSensitivity);
    }

    void LateUpdate()
    {
        if (currentHeldItem == null || playerCamera == null) return;

        Vector3 targetPos = pickupPoint.position;
        Vector3 flatForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up);

        // Anti-clip ray
        Vector3 dirToTarget = (targetPos - currentHeldItem.transform.position).normalized;
        if (Physics.Raycast(currentHeldItem.transform.position, dirToTarget, out RaycastHit wallHit, objectRadius * 2f, wallMask))
        {
            targetPos = wallHit.point - wallHit.normal * (objectRadius + pushbackDistance);
        }

        currentHeldItem.transform.position = targetPos;
        currentHeldItem.transform.rotation = targetRot;
    }

    void Update()
    {
        if (playerCamera == null || Mouse.current == null) return;

        HandleScrollAndKeys();
        HandleRaycastHoverInput();
    }

    void HandleScrollAndKeys()
    {
        // ──────────────────────────────── Scroll wheel ────────────────────────────────
        Vector2 scrollDelta = Mouse.current.scroll.ReadValue();
        float scrollY = scrollDelta.y;

        if (Mathf.Abs(scrollY) > scrollThreshold)
        {
            // Calculate movement
            float raw = scrollY * scrollSensitivity;

            // Round to nearest integer
            int steps = Mathf.RoundToInt(raw);

            // Prevent zero movement when there was actual input
            if (steps == 0)
            {
                steps = scrollY > 0 ? 1 : -1;
            }

            currentSlotIndex += steps;
            currentSlotIndex = Mathf.Clamp(currentSlotIndex, 0, 2);

            SwitchSlot();
            UpdateInventoryUI();
        }

        // ──────────────────────────────── Number keys 1–3 ────────────────────────────────
        if (Keyboard.current?.digit1Key.wasPressedThisFrame == true) SetSlot(0);
        if (Keyboard.current?.digit2Key.wasPressedThisFrame == true) SetSlot(1);
        if (Keyboard.current?.digit3Key.wasPressedThisFrame == true) SetSlot(2);
    }

    void SetSlot(int index)
    {
        if (index == currentSlotIndex) return;
        currentSlotIndex = index;
        SwitchSlot();
        UpdateInventoryUI();
    }

    void SwitchSlot()
    {
        StoreHeldItem();

        if (slots[currentSlotIndex] != null)
        {
            HoldItem(slots[currentSlotIndex]);
        }
    }

    void HoldItem(Pickupable item)
    {
        if (item == null) return;

        currentHeldItem = item;
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        item.gameObject.layer = heldLayerIndex;
        item.gameObject.SetActive(true);
        item.Pickup();

        currentHeldItem.transform.position = pickupPoint.position;
        Vector3 flatForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        currentHeldItem.transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
    }

    void StoreHeldItem()
    {
        if (currentHeldItem != null)
        {
            currentHeldItem.gameObject.SetActive(false);
            currentHeldItem = null;
        }
    }

    void HandleRaycastHoverInput()
    {
        // Clear previous hover
        if (hoveredObject != null)
        {
            hoveredObject.SetHovered(false);
            hoveredObject = null;
        }

        if (crosshairImage != null) crosshairImage.color = defaultCrosshairColor;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        bool isHolding = currentHeldItem != null;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, isHolding ? placementRange : pickupRange, isHolding ? placementMask : pickupMask))
        {
            if (isHolding)
            {
                if (crosshairImage != null) crosshairImage.color = placeCrosshairColor;
            }
            else
            {
                Pickupable target = hit.collider.GetComponent<Pickupable>();
                if (target != null && target.canPickup && slots[currentSlotIndex] == null)
                {
                    hoveredObject = target;
                    target.SetHovered(true);
                    if (crosshairImage != null) crosshairImage.color = hoverCrosshairColor;
                }
            }
        }

        // Left mouse button input
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isHolding)
            {
                RaycastHit placeHit;
                if (Physics.Raycast(ray, out placeHit, placementRange, placementMask))
                {
                    currentHeldItem.Place(placeHit.point, placeHit.normal);
                }
                else
                {
                    Vector3 vel = playerCamera.transform.forward * 8f + Vector3.up * 2f;
                    currentHeldItem.Drop(vel);
                }

                slots[currentSlotIndex] = null;
                currentHeldItem = null;
                UpdateInventoryUI(); // Show empty slot immediately
            }
            else if (hoveredObject != null)
            {
                HoldItem(hoveredObject);
                slots[currentSlotIndex] = hoveredObject;
                UpdateInventoryUI();
            }
        }
    }

    void UpdateInventoryUI()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slotIcons[i] != null)
            {
                if (slots[i] != null && slots[i].itemIcon != null)
                {
                    slotIcons[i].sprite = slots[i].itemIcon;
                    slotIcons[i].color = filledIconColor;
                }
                else
                {
                    slotIcons[i].sprite = emptyIcon;
                    slotIcons[i].color = emptyIconColor;
                }
            }

            if (slotLabels[i] != null)
            {
                if (slots[i] != null)
                {
                    slotLabels[i].text = slots[i].name.Replace("(Clone)", "").Replace(" (", "");
                    slotLabels[i].color = itemColor;
                }
                else
                {
                    slotLabels[i].text = "Empty";
                    slotLabels[i].color = emptyColor;
                }
            }

            if (slotFrames[i] != null)
            {
                slotFrames[i].color = (i == currentSlotIndex) ? currentSlotColor : Color.white;
            }
        }
    }
}