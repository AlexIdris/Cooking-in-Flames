using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Controls the pages of a recipe book panel in the Canvas.
///
/// HIERARCHY EXPECTED
/// ───────────────────
/// RecipePanel (root — toggled by RecipeBook2D)
///   └── PageParent_0   (first page — active by default, Prev hidden)
///   └── PageParent_1
///   └── PageParent_2   (last page — Next hidden)
///   └── NextButton     ← assign in Inspector
///   └── PrevButton     ← assign in Inspector (hidden on page 0)
///   └── CloseButton    ← assign in RecipeBook2D Inspector
///
/// BUTTON VISIBILITY RULES
/// ────────────────────────
/// PrevButton — hidden on the first page, shown on all others.
/// NextButton — hidden on the last page, shown on all others.
/// Both are updated instantly every time a page turn completes.
///
/// PAGE TURN ANIMATION
/// ────────────────────
/// On Next or Prev: fade out current page → swap → fade in new page.
/// Page-flip audio plays at the start of each turn.
/// All animations use Time.unscaledDeltaTime so they work while the
/// game is paused (Time.timeScale = 0) via RecipeBook2D.
///
/// SETUP
/// ──────
/// 1. Place this script on the RecipePanel or any persistent child.
/// 2. Assign all page-group GameObjects to pages[] in order.
/// 3. Assign nextButton, prevButton, and pageFlipClip.
/// 4. RecipeBook2D calls ResetToFirstPage() each time the book opens.
/// </summary>
public class RecipePageFlipper : MonoBehaviour
{
    [Header("Pages")]
    [Tooltip("Ordered list of page-group parent GameObjects.\n" +
             "Each entry is one 'page' — activate/deactivate as a unit.")]
    public GameObject[] pages;

    [Header("Buttons")]
    public Button nextButton;
    public Button prevButton;

    [Header("Page Turn Audio")]
    [Tooltip("Sound played at the start of each page turn.")]
    public AudioClip pageFlipClip;
    [Range(0f, 1f)] public float pageFlipVolume = 1f;

    [Header("Fade Timing")]
    [Tooltip("Seconds the current page fades out. Uses unscaled time (works while paused).")]
    [Range(0f, 1f)] public float fadeOutDuration = 0.15f;
    [Tooltip("Seconds the new page fades in. Uses unscaled time (works while paused).")]
    [Range(0f, 1f)] public float fadeInDuration = 0.2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private int         currentPage = 0;
    private bool        isFlipping  = false;
    private AudioSource audioSource;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (pages == null || pages.Length == 0)
        { Debug.LogWarning($"[RecipePageFlipper] {name}: No pages assigned.", this); return; }

        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (prevButton != null) prevButton.onClick.AddListener(OnPrevClicked);

        ShowPage(0, animate: false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Snaps to page 0 instantly. Called by RecipeBook2D every time the book opens.
    /// </summary>
    public void ResetToFirstPage()
    {
        if (pages == null || pages.Length == 0) return;
        StopAllCoroutines();
        isFlipping  = false;
        currentPage = 0;
        ShowPage(0, animate: false);
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    private void OnNextClicked()
    {
        if (isFlipping || pages == null) return;
        if (currentPage >= pages.Length - 1) return;
        StartCoroutine(FlipTo(currentPage + 1));
    }

    private void OnPrevClicked()
    {
        if (isFlipping || pages == null) return;
        if (currentPage <= 0) return;
        StartCoroutine(FlipTo(currentPage - 1));
    }

    // ── Page flip (unscaled — runs while game is paused) ──────────────────────

    private IEnumerator FlipTo(int targetIndex)
    {
        isFlipping = true;
        PlayFlipSound();

        // 1. Fade out current page
        if (fadeOutDuration > 0f)
            yield return StartCoroutine(FadePage(pages[currentPage], fadeOut: true, fadeOutDuration));

        // 2. Swap pages
        pages[currentPage].SetActive(false);
        currentPage = targetIndex;
        pages[currentPage].SetActive(true);
        SetPageAlpha(pages[currentPage], 0f);

        UpdateButtons();

        // 3. Fade in new page
        if (fadeInDuration > 0f)
            yield return StartCoroutine(FadePage(pages[currentPage], fadeOut: false, fadeInDuration));

        SetPageAlpha(pages[currentPage], 1f);
        isFlipping = false;
    }

    // ── Instant switch ────────────────────────────────────────────────────────

    private void ShowPage(int index, bool animate)
    {
        if (pages == null || index < 0 || index >= pages.Length) return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null) continue;
            bool active = i == index;
            pages[i].SetActive(active);
            if (active) SetPageAlpha(pages[i], 1f);
        }

        currentPage = index;
        UpdateButtons();
    }

    // ── Button visibility ─────────────────────────────────────────────────────

    private void UpdateButtons()
    {
        if (pages == null) return;
        // Prev: hidden on first page (no previous page to go to)
        if (prevButton != null) prevButton.gameObject.SetActive(currentPage > 0);
        // Next: hidden on last page (no further pages exist)
        if (nextButton != null) nextButton.gameObject.SetActive(currentPage < pages.Length - 1);
    }

    // ── Fade (unscaled time) ──────────────────────────────────────────────────

    private IEnumerator FadePage(GameObject page, bool fadeOut, float duration)
    {
        if (page == null) yield break;

        CanvasRenderer[] renderers = page.GetComponentsInChildren<CanvasRenderer>(true);
        float startAlpha = fadeOut ? 1f : 0f;
        float endAlpha   = fadeOut ? 0f : 1f;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            // unscaledDeltaTime — works correctly when Time.timeScale == 0
            elapsed += Time.unscaledDeltaTime;
            float a  = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            foreach (CanvasRenderer cr in renderers)
                if (cr != null) cr.SetAlpha(a);
            yield return null;
        }

        foreach (CanvasRenderer cr in renderers)
            if (cr != null) cr.SetAlpha(endAlpha);
    }

    private void SetPageAlpha(GameObject page, float alpha)
    {
        if (page == null) return;
        foreach (CanvasRenderer cr in page.GetComponentsInChildren<CanvasRenderer>(true))
            if (cr != null) cr.SetAlpha(alpha);
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    private void PlayFlipSound()
    {
        if (pageFlipClip == null) return;
        if (audioSource != null)
            audioSource.PlayOneShot(pageFlipClip, pageFlipVolume);
        else
            AudioSource.PlayClipAtPoint(pageFlipClip, transform.position, pageFlipVolume);
    }
}