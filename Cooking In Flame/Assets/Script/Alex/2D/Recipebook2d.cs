using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a UI Button in the Canvas that represents the recipe book.
///
/// BEHAVIOUR
/// ──────────
/// • While the shop is open (PlayerHand2D.IsInteractionEnabled) the button glows
///   to hoverColor on pointer enter and returns to normalColor on pointer exit.
/// • Clicking the button opens the recipe panel (activates it) and freezes the game.
/// • A separate close button inside the panel closes it and restores time.
///
/// SETUP
/// ──────
/// 1. Create a UI Button in your Canvas for the recipe book icon.
/// 2. Add this script to that Button GameObject.
/// 3. Assign recipePanel, closeButton, and optionally playerHand in the Inspector.
/// 4. The Button's own onClick can remain empty — this script handles the click via
///    IPointerClickHandler so there is no double-firing.
/// 5. Remove any existing onClick listeners on the Button to avoid duplicates.
/// </summary>
[RequireComponent(typeof(Button))]
public class RecipeBook2D : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visuals")]
    [Tooltip("Button image tint while the cursor is NOT over it (shop open).")]
    public Color normalColor = Color.white;
    [Tooltip("Button image tint while the cursor IS hovering it.")]
    public Color hoverColor  = new Color(1f, 0.92f, 0.6f, 1f);
    [Tooltip("How quickly the tint lerps between normal and hover.")]
    [Range(4f, 24f)] public float smoothSpeed = 10f;
    [Tooltip("Scale multiplier applied to the button while hovered. 1 = no change.")]
    [Range(0.9f, 1.5f)] public float hoverScale = 1.08f;

    [Header("Recipe Panel")]
    [Tooltip("The Canvas Panel that contains the recipe pages.\n" +
             "Activated when the book is opened, deactivated when closed.")]
    public GameObject recipePanel;

    [Tooltip("A Button inside the recipe panel that closes the book (e.g. an X button).")]
    public Button closeButton;

    [Header("Audio")]
    [Tooltip("Optional sound played when the book opens or closes.")]
    public AudioClip openCloseSound;
    [Range(0f, 1f)] public float openCloseVolume = 1f;

    [Header("Scene References")]
    [Tooltip("Auto-found if left blank.")]
    public PlayerHand2D playerHand;

    // ── Private ───────────────────────────────────────────────────────────────

    private Button           button;
    private Image            buttonImage;
    private Vector3          originalScale;
    private bool             isHovered;
    private bool             isOpen;
    private RecipePageFlipper pageFlipper;
    private AudioSource      audioSource;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        button        = GetComponent<Button>();
        buttonImage   = GetComponent<Image>();
        originalScale = transform.localScale;
        audioSource   = GetComponent<AudioSource>();

        // Disable the Button's built-in colour transitions — we drive colour manually
        ColorBlock cb = button.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = Color.white;
        cb.pressedColor     = Color.white;
        cb.selectedColor    = Color.white;
        cb.disabledColor    = Color.white;
        cb.colorMultiplier  = 1f;
        button.colors = cb;
        button.transition = Selectable.Transition.None;
    }

    void Start()
    {
        if (playerHand == null) playerHand = FindObjectOfType<PlayerHand2D>();

        if (recipePanel != null)
        {
            pageFlipper = recipePanel.GetComponentInChildren<RecipePageFlipper>(true);
            recipePanel.SetActive(false);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseBook);
        else
            Debug.LogWarning($"[RecipeBook2D] {name}: No close button assigned.", this);

        if (buttonImage != null) buttonImage.color = normalColor;
        transform.localScale = originalScale;
    }

    void Update()
    {
        // Lerp colour and scale every frame using unscaled time so hover
        // animation works while the game is paused (timeScale = 0).
        bool shopOpen = playerHand != null && playerHand.IsInteractionEnabled;
        Color   tCol  = (isHovered && shopOpen) ? hoverColor : normalColor;
        Vector3 tScl  = (isHovered && shopOpen) ? originalScale * hoverScale : originalScale;
        float   dt    = Time.unscaledDeltaTime;

        if (buttonImage != null)
            buttonImage.color = Color.Lerp(buttonImage.color, tCol, smoothSpeed * dt);
        transform.localScale  = Vector3.Lerp(transform.localScale, tScl, smoothSpeed * dt);
    }

    // ── Pointer event handlers ────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _) => isHovered = true;
    public void OnPointerExit(PointerEventData _)  => isHovered = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (isOpen)                                                 return;
        if (playerHand == null || !playerHand.IsInteractionEnabled) return;  // shop must be open
        if (playerHand.IsHoldingItem)                               return;

        OpenBook();
    }

    // ── Open / Close ──────────────────────────────────────────────────────────

    private void OpenBook()
    {
        isOpen = true;

        if (recipePanel != null)
        {
            recipePanel.SetActive(true);
            pageFlipper?.ResetToFirstPage();
        }

        // Disable all spawners and pickupable items while the book is open
        // so the player cannot interact with the game world during reading.
        PauseManager.SetGameWorldInteractable(false);

        // Freeze all customer order displays so their letter-reveal and
        // leave-polling pause while the recipe book is open.
        CustomerOrderDisplay.FreezeAll();

        Time.timeScale = 0f;

        PlaySound();
        Debug.Log("[RecipeBook2D] Recipe book opened — game paused.");
    }

    /// <summary>
    /// Closes the recipe panel and restores normal time.
    /// Wired to the close button via onClick.AddListener in Start().
    /// </summary>
    public void CloseBook()
    {
        if (!isOpen) return;
        isOpen    = false;
        isHovered = false;

        if (recipePanel != null)
            recipePanel.SetActive(false);

        // Re-enable spawners and pickupable items now that the book is closed.
        PauseManager.SetGameWorldInteractable(true);

        // Resume all customer order displays.
        CustomerOrderDisplay.UnfreezeAll();

        Time.timeScale = 1f;

        PlaySound();
        Debug.Log("[RecipeBook2D] Recipe book closed — game resumed.");
    }

    // SetGameWorldInteractable removed — delegated to PauseManager.SetGameWorldInteractable(bool)
    // which is static and centralises the logic for all pause systems in one place.

    private void PlaySound()
    {
        if (openCloseSound == null) return;
        if (audioSource != null)
            audioSource.PlayOneShot(openCloseSound, openCloseVolume);
        else
            AudioSource.PlayClipAtPoint(openCloseSound, transform.position, openCloseVolume);
    }

    void OnDestroy()
    {
        if (isOpen) Time.timeScale = 1f;
    }
}