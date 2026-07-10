using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("The main menu panel or canvas that should be deactivated when starting the game.")]
    [SerializeField] private GameObject menuToDeactivate;
    [Tooltip("Optional: A panel or text that will be activated while scenes are loading (e.g. Loading Screen).")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Sub-Panels")]
    [SerializeField] private GameObject mainMenuArea;
    [SerializeField] private GameObject meusCarrosArea;
    [SerializeField] private GameObject settingsArea;

    [Header("Menu Buttons")]
    [SerializeField] private Button jogarButton;
    [SerializeField] private Button meusCarrosButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button meusCarrosBackButton;
    [SerializeField] private Button settingsBackButton;

    [Header("Settings Components")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown screenModeDropdown;
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Scene Loading Settings")]
    [Tooltip("List of scene names to load additively in the background.")]
    [SerializeField] private string[] scenesToLoad = new string[] { "Fazenda Veloz" };

    [Tooltip("Optional: Name of the scene to set active after loading. If empty, the first scene in the array is set active.")]
    [SerializeField] private string activeSceneName = "Fazenda Veloz";

    private bool isStarting = false;
    private List<Resolution> resolutionsList = new List<Resolution>();

    private void Start()
    {
        // Wire up main menu buttons
        if (jogarButton != null)
        {
            jogarButton.onClick.AddListener(PlayGame);
        }

        if (meusCarrosButton != null)
        {
            meusCarrosButton.onClick.AddListener(OpenMeusCarros);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (meusCarrosBackButton != null)
        {
            meusCarrosBackButton.onClick.AddListener(CloseMeusCarros);
        }

        if (settingsBackButton != null)
        {
            settingsBackButton.onClick.AddListener(CloseSettings);
        }

        // Initialize and wire up settings options
        InitializeSettings();

        // Set initial panels state
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
        if (meusCarrosArea != null) meusCarrosArea.SetActive(false);
        if (settingsArea != null) settingsArea.SetActive(false);
    }

    public void PlayGame()
    {
        if (isStarting) return;
        isStarting = true;

        // 1. Deactivate the menu as requested
        if (menuToDeactivate != null)
        {
            menuToDeactivate.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        // Show loading panel if assigned
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // 2. Load configured scenes additively in the background
        StartCoroutine(LoadScenesRoutine());
    }

    public void QuitGame()
    {
        Debug.Log("Sair button clicked. Quitting game...");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    #region Panel Navigation

    public void OpenMeusCarros()
    {
        if (mainMenuArea != null) mainMenuArea.SetActive(false);
        if (meusCarrosArea != null) meusCarrosArea.SetActive(true);
    }

    public void CloseMeusCarros()
    {
        if (meusCarrosArea != null) meusCarrosArea.SetActive(false);
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
    }

    public void OpenSettings()
    {
        if (mainMenuArea != null) mainMenuArea.SetActive(false);
        if (settingsArea != null) settingsArea.SetActive(true);
        
        // Refresh values in case they were modified elsewhere
        LoadSettingsValues();
    }

    public void CloseSettings()
    {
        if (settingsArea != null) settingsArea.SetActive(false);
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
    }

    #endregion

    #region Settings Logic

    private void InitializeSettings()
    {
        // 1. Populate Resolutions
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            resolutionsList.Clear();
            
            Resolution[] systemResolutions = Screen.resolutions;
            List<string> options = new List<string>();
            int currentResolutionIndex = 0;

            // Simple filter to avoid too many duplicate/refresh-rate-only resolutions
            HashSet<string> uniqueResStrings = new HashSet<string>();

            for (int i = 0; i < systemResolutions.Length; i++)
            {
                string resOption = $"{systemResolutions[i].width} x {systemResolutions[i].height}";
                if (!uniqueResStrings.Contains(resOption))
                {
                    uniqueResStrings.Add(resOption);
                    resolutionsList.Add(systemResolutions[i]);
                    options.Add(resOption);

                    if (systemResolutions[i].width == Screen.currentResolution.width &&
                        systemResolutions[i].height == Screen.currentResolution.height)
                    {
                        currentResolutionIndex = options.Count - 1;
                    }
                }
            }

            resolutionDropdown.AddOptions(options);
            
            // Load saved resolution or default to current
            int savedResIndex = PlayerPrefs.GetInt("Settings_Resolution", currentResolutionIndex);
            if (savedResIndex >= 0 && savedResIndex < resolutionsList.Count)
            {
                resolutionDropdown.value = savedResIndex;
            }
            else
            {
                resolutionDropdown.value = currentResolutionIndex;
            }
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        // 2. Populate Screen Modes
        if (screenModeDropdown != null)
        {
            screenModeDropdown.ClearOptions();
            List<string> modes = new List<string> { "Tela Cheia", "Janela", "Janela Sem Bordas" };
            screenModeDropdown.AddOptions(modes);

            int savedScreenMode = PlayerPrefs.GetInt("Settings_ScreenMode", 0); // 0 = Fullscreen, 1 = Windowed, 2 = Borderless Window
            screenModeDropdown.value = savedScreenMode;
            screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        }

        // 3. Populate Quality
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            string[] qualityNames = QualitySettings.names;
            List<string> options = new List<string>(qualityNames);
            qualityDropdown.AddOptions(options);

            int savedQuality = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
            qualityDropdown.value = savedQuality;
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // 4. Wire sliders
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = PlayerPrefs.GetFloat("Settings_MasterVolume", 1f);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = PlayerPrefs.GetFloat("Settings_MusicVolume", 0.8f);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("Settings_SFXVolume", 0.8f);
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = PlayerPrefs.GetFloat("Settings_Sensitivity", 0.5f);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        // Apply initial visual settings on start
        ApplyScreenSettings();
    }

    private void LoadSettingsValues()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.value = PlayerPrefs.GetFloat("Settings_MasterVolume", 1f);
        if (musicVolumeSlider != null) musicVolumeSlider.value = PlayerPrefs.GetFloat("Settings_MusicVolume", 0.8f);
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = PlayerPrefs.GetFloat("Settings_SFXVolume", 0.8f);
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat("Settings_Sensitivity", 0.5f);
        if (qualityDropdown != null) qualityDropdown.value = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
    }

    private void ApplyScreenSettings()
    {
        int modeIdx = PlayerPrefs.GetInt("Settings_ScreenMode", 0);
        FullScreenMode fsm = FullScreenMode.FullScreenWindow;
        if (modeIdx == 1) fsm = FullScreenMode.Windowed;
        else if (modeIdx == 2) fsm = FullScreenMode.MaximizedWindow;

        int resIdx = PlayerPrefs.GetInt("Settings_Resolution", -1);
        if (resIdx >= 0 && resIdx < resolutionsList.Count)
        {
            Resolution res = resolutionsList[resIdx];
            Screen.SetResolution(res.width, res.height, fsm);
        }
        else
        {
            Screen.fullScreenMode = fsm;
        }

        int qualityIdx = PlayerPrefs.GetInt("Settings_Quality", QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(qualityIdx, true);
    }

    private void OnResolutionChanged(int index)
    {
        PlayerPrefs.SetInt("Settings_Resolution", index);
        PlayerPrefs.Save();
        ApplyScreenSettings();
    }

    private void OnScreenModeChanged(int index)
    {
        PlayerPrefs.SetInt("Settings_ScreenMode", index);
        PlayerPrefs.Save();
        ApplyScreenSettings();
    }

    private void OnQualityChanged(int index)
    {
        PlayerPrefs.SetInt("Settings_Quality", index);
        PlayerPrefs.Save();
        QualitySettings.SetQualityLevel(index, true);
        Debug.Log($"Quality level set to: {QualitySettings.names[index]}");
    }

    private void OnMasterVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Settings_MasterVolume", value);
        PlayerPrefs.Save();
        Debug.Log($"Master Volume changed to: {value}");
        // Here you would normally set the audio mixer parameter, e.g.
        // AudioListener.volume = value; or AudioMixer.SetFloat("MasterVol", Log10(value) * 20);
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Settings_MusicVolume", value);
        PlayerPrefs.Save();
        Debug.Log($"Music Volume changed to: {value}");
    }

    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("Settings_SFXVolume", value);
        PlayerPrefs.Save();
        Debug.Log($"SFX Volume changed to: {value}");
    }

    private void OnSensitivityChanged(float value)
    {
        PlayerPrefs.SetFloat("Settings_Sensitivity", value);
        PlayerPrefs.Save();
        Debug.Log($"Sensitivity changed to: {value}");
    }

    #endregion

    private IEnumerator LoadScenesRoutine()
    {
        // Save original background loading priority and set to BelowNormal to prevent thread lag
        ThreadPriority originalPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;

        List<AsyncOperation> asyncOps = new List<AsyncOperation>();

        // Start loading all scenes additively in background, reloading them if they are already loaded
        foreach (string sceneName in scenesToLoad)
        {
            if (string.IsNullOrEmpty(sceneName)) continue;

            // Check if the scene is already loaded in the SceneManager
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Debug.Log($"Scene {sceneName} is already loaded. Unloading it first to load a fresh instance...");
                AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
                if (unloadOp != null)
                {
                    while (!unloadOp.isDone)
                    {
                        yield return null;
                    }
                }
                Debug.Log($"Finished unloading scene: {sceneName}");
            }

            Debug.Log($"Starting background loading of scene: {sceneName}");
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                // Load in background up to 90% without activating (prevents freezing the menu gameplay)
                op.allowSceneActivation = false;
                asyncOps.Add(op);
            }
        }

        // Wait for all scenes to finish background loading (reach 0.9f progress)
        bool allLoadedToReady = false;
        while (!allLoadedToReady)
        {
            allLoadedToReady = true;
            foreach (var op in asyncOps)
            {
                if (op.progress < 0.9f)
                {
                    allLoadedToReady = false;
                    break;
                }
            }
            yield return null;
        }

        // Now activate the loaded scenes
        foreach (var op in asyncOps)
        {
            op.allowSceneActivation = true;
        }

        // Wait for activation to complete (op.isDone is true)
        foreach (var op in asyncOps)
        {
            while (!op.isDone)
            {
                yield return null;
            }
        }

        // Set active scene if specified
        string targetActiveScene = string.IsNullOrEmpty(activeSceneName) && scenesToLoad.Length > 0 ? scenesToLoad[0] : activeSceneName;
        if (!string.IsNullOrEmpty(targetActiveScene))
        {
            Scene loadedScene = SceneManager.GetSceneByName(targetActiveScene);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                Debug.Log($"Set active scene to: {targetActiveScene}");
            }
        }

        // Hide loading panel when done
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        // Restore original priority
        Application.backgroundLoadingPriority = originalPriority;
        Debug.Log("Finished loading all additive scenes.");
    }
}
