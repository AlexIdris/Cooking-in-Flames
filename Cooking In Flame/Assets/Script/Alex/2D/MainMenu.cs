using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [Tooltip("Name of the scene to load when Play is clicked.")]
    public string gameSceneName = "KitchenScene";

    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public Button          playButton;
    public Button          quitButton;

    [Header("Day Select Button")]
    [Tooltip("Button that opens the day selection panel.")]
    public Button          dayButton;

    [Tooltip("Panel that is activated when the day button is clicked.")]
    public GameObject      daySelectPanel;

    [Tooltip("GameObject that is deactivated when the day panel opens\n" +
             "(e.g. the main menu panel or background object).")]
    public GameObject      objectToDeactivate;

    [Header("Custom Text")]
    public string title           = "Cooking in Flames";
    public string playButtonText  = "Play";
    public string quitButtonText  = "Quit";
    public string dayButtonText   = "Select Day";

    [Header("Colors")]
    public Color titleColor             = Color.white;
    public Color buttonNormalColor      = Color.white;
    public Color buttonHighlightColor   = new Color(1f, 0.8f, 0.3f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        SetupUI();

        // Ensure the day panel starts hidden
        if (daySelectPanel != null) daySelectPanel.SetActive(false);
    }

    // ── UI Setup ──────────────────────────────────────────────────────────────

    private void SetupUI()
    {
        if (titleText != null)
        {
            titleText.text  = title;
            titleText.color = titleColor;
        }

        SetupButton(playButton, playButtonText, OnPlayClicked);
        SetupButton(quitButton, quitButtonText, OnQuitClicked);
        SetupButton(dayButton,  dayButtonText,  OnDayClicked);
    }

    private void SetupButton(Button btn, string label, UnityEngine.Events.UnityAction callback)
    {
        if (btn == null) return;
        TextMeshProUGUI txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = label;
        btn.onClick.AddListener(callback);
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    private void OnPlayClicked()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
            Debug.LogWarning("[MainMenu] Game Scene Name is empty — set it in the Inspector.");
    }

    private void OnQuitClicked()
    {
        Debug.Log("[MainMenu] Quit pressed.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnDayClicked()
    {
        // Activate the day selection panel
        if (daySelectPanel != null)
            daySelectPanel.SetActive(true);

        // Deactivate the specified object (e.g. main menu panel or background)
        if (objectToDeactivate != null)
            objectToDeactivate.SetActive(false);
    }

    // ── Hover effects ─────────────────────────────────────────────────────────

    public void OnPlayHover()  => ApplyHighlight(playButton);
    public void OnQuitHover()  => ApplyHighlight(quitButton);
    public void OnDayHover()   => ApplyHighlight(dayButton);

    private void ApplyHighlight(Button btn)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = buttonHighlightColor;
        btn.colors = colors;
    }
}