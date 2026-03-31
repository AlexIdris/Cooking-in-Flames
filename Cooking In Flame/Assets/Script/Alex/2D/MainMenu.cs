using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [Tooltip("Name of the scene to load when Play is clicked")]
    public string gameSceneName = "KitchenScene";

    [Header("UI Elements (Drag from Hierarchy)")]
    public TextMeshProUGUI titleText;
    public Button playButton;
    public Button quitButton;

    [Header("Custom Text")]
    public string title = "Cooking in Flames";
    public string playButtonText = "Play";
    public string quitButtonText = "Quit";

    [Header("Colors")]
    public Color titleColor = Color.white;
    public Color buttonNormalColor = Color.white;
    public Color buttonHighlightColor = new Color(1f, 0.8f, 0.3f);

    void Start()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        // Title
        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = titleColor;
        }

        // Play Button
        if (playButton != null)
        {
            TextMeshProUGUI playText = playButton.GetComponentInChildren<TextMeshProUGUI>();
            if (playText != null) playText.text = playButtonText;

            playButton.onClick.AddListener(OnPlayClicked);
        }

        // Quit Button
        if (quitButton != null)
        {
            TextMeshProUGUI quitText = quitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (quitText != null) quitText.text = quitButtonText;

            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void OnPlayClicked()
    {
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogWarning("Game Scene Name is empty! Set it in the Inspector.");
        }
    }

    private void OnQuitClicked()
    {
        Debug.Log("Quit button pressed - exiting game.");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stops play mode in Editor
#endif
    }

    // Optional: Add hover effects if you want more polish
    public void OnPlayHover()
    {
        if (playButton != null)
        {
            var colors = playButton.colors;
            colors.normalColor = buttonHighlightColor;
            playButton.colors = colors;
        }
    }

    public void OnQuitHover()
    {
        if (quitButton != null)
        {
            var colors = quitButton.colors;
            colors.normalColor = buttonHighlightColor;
            quitButton.colors = colors;
        }
    }
}