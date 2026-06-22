using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuArea;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Settings Components")]
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private TMP_Dropdown qualityDropdown;

    [Header("Scene Loading")]
    [SerializeField] private string menuSceneName = "Menu Inicial";

    private RaceManager raceManager;

    private void Awake()
    {
        // Find RaceManager in the scene
        raceManager = Object.FindAnyObjectByType<RaceManager>();
    }

    private void Start()
    {
        // Wire up buttons
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.AddListener(ReturnToMenu);
        }

        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(CloseSettings);
        }

        // Initialize Quality Dropdown
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            
            // Populate quality levels
            string[] qualityNames = QualitySettings.names;
            System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>(qualityNames);
            qualityDropdown.AddOptions(options);

            // Set current quality level selection
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // Ensure proper initial state
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        // Reset panels when the pause menu is opened
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Update dropdown value in case it changed elsewhere
        if (qualityDropdown != null)
        {
            qualityDropdown.value = QualitySettings.GetQualityLevel();
        }
    }

    public void ResumeGame()
    {
        if (raceManager != null)
        {
            raceManager.TogglePause();
        }
        else
        {
            // Fallback if RaceManager is missing
            Time.timeScale = 1f;
            gameObject.SetActive(false);
        }
    }

    public void OpenSettings()
    {
        if (mainMenuArea != null) mainMenuArea.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
    }

    public void OnQualityChanged(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
        Debug.Log($"Quality changed to: {QualitySettings.names[index]}");
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f; // Make sure time scale is reset before loading scene!
        SceneManager.LoadScene(menuSceneName);
    }
}
