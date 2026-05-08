using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attached to each customer prefab. Shows the order text, icon, speech bubble,
/// and an anger fill bar that drains while the customer waits. When the bar empties
/// FailOrder() is called on the mover.
///
/// FREEZE BEHAVIOUR
/// ─────────────────
/// The display freezes (anger drain paused, typing paused) when either:
///   • SetFrozen(true) is called externally (recipe book open, pause menu)
///   • Time.timeScale == 0 (secondary fallback)
///
/// RecipeBook2D calls CustomerOrderDisplay.FreezeAll() on open and UnfreezeAll()
/// on close. PauseManager does the same. While frozen the display is visible but
/// nothing updates — anger stays at its current value and typing halts mid-word.
/// </summary>
public class CustomerOrderDisplay : MonoBehaviour
{
    [Header("UI")]
    public TextMeshPro orderText;
    public Image       orderIcon;
    public Image       angerFill;
    public GameObject  speechBubble;

    [Header("Anger")]
    [Tooltip("How quickly the anger bar drains per second. 0 = no drain.")]
    public float drainSpeed = 0.2f;

    [Header("Typing")]
    [Tooltip("Seconds between each character appearing.")]
    public float typingSpeed = 0.05f;

    // ── Private ───────────────────────────────────────────────────────────────

    private CustomerMover2   mover;
    private CustomerSpawner2 spawner;
    private float            anger = 1f;
    private Coroutine        typingCoroutine;
    private bool             isFrozen;

    // Static registry — FreezeAll / UnfreezeAll reach every active display
    private static readonly List<CustomerOrderDisplay> allDisplays =
        new List<CustomerOrderDisplay>();

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>Freezes every active CustomerOrderDisplay (anger, typing, leave poll).</summary>
    public static void FreezeAll()
    {
        foreach (CustomerOrderDisplay d in allDisplays)
            if (d != null) d.SetFrozen(true);
    }

    /// <summary>Unfreezes every active CustomerOrderDisplay.</summary>
    public static void UnfreezeAll()
    {
        foreach (CustomerOrderDisplay d in allDisplays)
            if (d != null) d.SetFrozen(false);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()  => allDisplays.Add(this);
    void OnDisable() => allDisplays.Remove(this);

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Init(CustomerSpawner2 s, CustomerMover2 m)
    {
        spawner = s;
        mover   = m;

        anger = 1f;
        if (angerFill != null) angerFill.fillAmount = 1f;

        // Subscribe to OnReachPoint so typing starts when the customer arrives
        mover.OnReachPoint += DisplayOrderTextLetterByLetter;
    }

    // ── Freeze control ────────────────────────────────────────────────────────

    /// <summary>
    /// Pauses or resumes anger drain and typing.
    /// While frozen the current UI state is preserved exactly as-is.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (spawner == null || mover == null) return;

        bool isFront  = spawner.customers.Count > 0 && spawner.customers[0] == mover;
        bool showUI   = isFront && mover.hasReachedPoint;

        // Show/hide UI elements based on queue position and arrival state
        if (orderText   != null) orderText.gameObject.SetActive(showUI);
        if (orderIcon   != null) orderIcon.gameObject.SetActive(showUI);
        if (speechBubble != null) speechBubble.SetActive(showUI);

        if (showUI)
        {
            // Set icon once when it first becomes visible
            if (orderIcon != null && orderIcon.sprite == null)
                orderIcon.sprite = spawner.GetFoodIcon(mover.orderedFood);

            // Anger drain — skip while frozen or timeScale is zero
            if (!isFrozen && Time.timeScale != 0f)
            {
                anger -= drainSpeed * Time.deltaTime;
                anger  = Mathf.Clamp01(anger);

                if (angerFill != null)
                    angerFill.fillAmount = anger;

                if (anger <= 0f)
                    mover.FailOrder();
            }
        }
        else
        {
            // Customer is not at the front or hasn't arrived yet — reset typing
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
                if (orderText != null) orderText.text = "";
            }

            // Reset icon so the next customer re-fetches correctly
            if (orderIcon != null) orderIcon.sprite = null;
        }
    }

    // ── Typing ────────────────────────────────────────────────────────────────

    public void DisplayOrderTextLetterByLetter(CustomerMover2 targetMover)
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeText(targetMover.orderedFood.ToString()));
    }

    private IEnumerator TypeText(string fullText)
    {
        // Insert spaces before capital letters (e.g. "HealthyBurger" → "Healthy Burger")
        string readable = System.Text.RegularExpressions.Regex.Replace(fullText, "(\\B[A-Z])", " $1");

        if (orderText != null) orderText.text = "";

        foreach (char c in readable)
        {
            // Pause typing mid-character while frozen or game is paused
            while (isFrozen || Time.timeScale == 0f)
                yield return null;

            if (orderText != null) orderText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        typingCoroutine = null;
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    public void ClearDisplay()
    {
        if (orderText != null) orderText.text = "";
        if (orderIcon != null) orderIcon.enabled = false;
    }

    // ── UpdateOrderDisplay (compatibility with CustomerSpawner2) ──────────────

    public void UpdateOrderDisplay()
    {
        if (mover == null || isFrozen) return;
        if (orderText != null) orderText.text = mover.orderedFood.ToString();
        if (orderIcon != null && spawner != null)
        {
            Sprite icon      = spawner.GetFoodIcon(mover.orderedFood);
            orderIcon.sprite  = icon;
            orderIcon.enabled = icon != null;
        }
    }
}