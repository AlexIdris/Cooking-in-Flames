using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the pause menu via Escape key AND two UI buttons.
///
/// BUTTONS
/// ────────
/// openPauseButton  — shown during normal play. Clicking it opens the pause panel.
///                    Hide this button inside the pause panel; keep it in your HUD.
/// resumeButton     — inside the pause panel. Clicking it resumes the game.
/// Both are also wired to Pause() and Resume() so you can assign them in the
/// Inspector without touching code.
///
/// FREEZE ON PAUSE
/// ────────────────
/// • Time.timeScale = 0
/// • All Spawnable2D and Pickupable2D components disabled
/// • CustomerOrderDisplay.FreezeAll()
///
/// STATIC HELPERS
/// ───────────────
/// PauseManager.SetGameWorldInteractable(bool) is static so RecipeBook2D and any
/// other script can call it without a direct reference.
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root panel shown while the game is paused.")]
    public GameObject pausePanel;

    [Header("Buttons")]
    [Tooltip("HUD button that opens the pause menu. Optional — Escape key always works.")]
    public Button openPauseButton;

    [Tooltip("Button inside the pause panel that resumes the game.")]
    public Button resumeButton;

    private bool isPaused = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (openPauseButton != null) openPauseButton.onClick.AddListener(Pause);
        if (resumeButton    != null) resumeButton.onClick.AddListener(Resume);

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void Update()
    {
        // Escape key works regardless of button assignments
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else          Pause();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Pause()
    {
        if (isPaused) return;

        isPaused = true;

        if (pausePanel     != null) pausePanel.SetActive(true);
        if (openPauseButton != null) openPauseButton.gameObject.SetActive(false);

        Time.timeScale = 0f;
        CustomerOrderDisplay.FreezeAll();
        SetGameWorldInteractable(false);
    }

    public void Resume()
    {
        if (!isPaused) return;

        isPaused = false;

        if (pausePanel      != null) pausePanel.SetActive(false);
        if (openPauseButton != null) openPauseButton.gameObject.SetActive(true);

        Time.timeScale = 1f;
        CustomerOrderDisplay.UnfreezeAll();
        SetGameWorldInteractable(true);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        CustomerOrderDisplay.UnfreezeAll();
        SetGameWorldInteractable(true);
        SceneManager.LoadScene("MainMenu");
    }

    // ── Static helper (called by RecipeBook2D and any other pause system) ─────

    /// <summary>
    /// Enables or disables all Spawnable2D and Pickupable2D components in the scene.
    /// Clears hover visuals on disable so nothing stays glowing while inactive.
    /// Static so it can be called without a PauseManager instance reference.
    /// </summary>
    public static void SetGameWorldInteractable(bool active)
    {
        foreach (Spawnable2D s in FindObjectsOfType<Spawnable2D>())
            if (s != null) s.enabled = active;

        foreach (Pickupable2D p in FindObjectsOfType<Pickupable2D>())
        {
            if (p == null) continue;
            p.enabled = active;
            if (!active) p.SetHovered(false);
        }
    }
}