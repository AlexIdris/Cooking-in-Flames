using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the shop open/close panel and gates all gameplay interaction.
///
/// BEHAVIOUR
/// ──────────
/// On scene load:
///   • Panel is fully visible, button reads "Close".
///   • PlayerHand2D interaction is DISABLED — items cannot be picked up or
///     dropped, and Spawnable2D dispensers will not glow or spawn.
///
/// When the player clicks "Close":
///   • Button label flips to "Open".
///   • The entire panel (background + button) fades out.
///   • Once the fade completes, DayNightCycle5Min.StartDay() is called and
///     PlayerHand2D interaction is ENABLED.
///
/// When DayNightCycle5Min reaches 8 PM it calls ShopToggle.SetClosed():
///   • PlayerHand2D interaction is DISABLED immediately.
///   • The panel fades back in with the "Close" label.
///   • Once fully visible, the button becomes interactable for a new day.
///
/// SETUP
/// ──────
/// 1. UI Panel — add a CanvasGroup component to the Panel root.
/// 2. Place the toggle Button as a child of the Panel.
/// 3. Add ShopToggle to any persistent GameObject.
/// 4. Assign in Inspector: panelCanvasGroup, toggleButton, buttonLabel.
///    dayNightCycle and playerHand can be auto-found if left blank.
/// </summary>
public class ShopToggle : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("CanvasGroup on the root Panel. Controls alpha for the entire panel\n" +
             "including its background image and all children.")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("If true and panelCanvasGroup is null at runtime, a CanvasGroup is\n" +
             "added to this GameObject as a fallback.")]
    public bool addCanvasGroupIfMissing = true;

    [Header("Button")]
    [Tooltip("The button the player clicks to open the shop.")]
    public Button toggleButton;

    [Tooltip("The TextMeshProUGUI label on the toggle button.")]
    public TextMeshProUGUI buttonLabel;

    [Header("Labels")]
    [Tooltip("Shown while shop is CLOSED. Clicking opens the shop and starts the day.")]
    public string closedLabel = "Close";

    [Tooltip("Shown briefly while fading out after the shop is opened.")]
    public string openLabel = "Open";

    [Header("Fade")]
    [Tooltip("Seconds for the panel to fade out on open, and fade in on close.")]
    [Range(0.05f, 3f)]
    public float fadeDuration = 0.5f;

    [Header("Scene References (auto-found if left blank)")]
    public DayNightCycle5Min dayNightCycle;
    public PlayerHand2D      playerHand;

    // ── Private ───────────────────────────────────────────────────────────────

    private bool      isOpen = false;
    private Coroutine fadeCo;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (dayNightCycle == null) dayNightCycle = FindObjectOfType<DayNightCycle5Min>();
        if (playerHand    == null) playerHand    = FindObjectOfType<PlayerHand2D>();

        if (dayNightCycle == null) Debug.LogWarning("[ShopToggle] No DayNightCycle5Min found.", this);
        if (playerHand    == null) Debug.LogWarning("[ShopToggle] No PlayerHand2D found.", this);

        // Resolve or auto-create panel CanvasGroup
        if (panelCanvasGroup == null && addCanvasGroupIfMissing)
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            Debug.LogWarning("[ShopToggle] panelCanvasGroup was null — using CanvasGroup on this " +
                             "GameObject. Assign the Panel's CanvasGroup in the Inspector.", this);
        }

        if (panelCanvasGroup == null) { Debug.LogError("[ShopToggle] panelCanvasGroup not assigned.", this); return; }
        if (toggleButton     == null) { Debug.LogError("[ShopToggle] toggleButton not assigned.",     this); return; }

        toggleButton.onClick.AddListener(OnButtonClicked);

        // Initial state: visible, closed label, interaction locked
        SetPanelAlpha(1f, interactable: true);
        ApplyLabel(open: false);
        playerHand?.SetInteractionEnabled(false);
    }

    // ── Button callback ───────────────────────────────────────────────────────

    private void OnButtonClicked()
    {
        if (isOpen) return;
        OpenShop();
    }

    // ── State transitions ─────────────────────────────────────────────────────

    private void OpenShop()
    {
        isOpen = true;

        // Flip label to "Open" before fading so it reads correctly as it disappears
        ApplyLabel(open: true);

        // Lock the panel immediately — no double-clicks during fade
        SetPanelAlpha(panelCanvasGroup.alpha, interactable: false);

        // Fade out, then start the day and enable interaction
        StartFade(0f, onComplete: () =>
        {
            playerHand?.SetInteractionEnabled(true);
            dayNightCycle?.StartDay(this);
            Debug.Log("[ShopToggle] Shop open — interaction enabled, day started.");
        });
    }

    /// <summary>
    /// Called by DayNightCycle5Min at 8 PM.
    /// Locks interaction immediately, then fades the panel back in.
    /// </summary>
    public void SetClosed()
    {
        isOpen = false;

        // Disable interaction the moment the day ends — before the panel is visible
        playerHand?.SetInteractionEnabled(false);

        // Set label before fading in so it reads "Close" as the panel appears
        ApplyLabel(open: false);

        // Fade in, then restore button interactivity
        StartFade(1f, onComplete: () =>
        {
            SetPanelAlpha(1f, interactable: true);
            Debug.Log("[ShopToggle] Shop closed — panel visible, ready for a new day.");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetPanelAlpha(float alpha, bool interactable)
    {
        panelCanvasGroup.alpha          = alpha;
        panelCanvasGroup.interactable   = interactable;
        panelCanvasGroup.blocksRaycasts = interactable;
    }

    private void ApplyLabel(bool open)
    {
        if (buttonLabel != null)
            buttonLabel.text = open ? openLabel : closedLabel;
    }

    // ── Fade ─────────────────────────────────────────────────────────────────

    private void StartFade(float targetAlpha, System.Action onComplete = null)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(targetAlpha, onComplete));
    }

    private IEnumerator FadeTo(float target, System.Action onComplete)
    {
        float start   = panelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed                += Time.deltaTime;
            panelCanvasGroup.alpha  = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = target;
        fadeCo                 = null;
        onComplete?.Invoke();
    }
}