using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingSceneController : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("ProgressBar slider component to display loading progress.")]
    [SerializeField] private Slider progressBar;
    
    [Tooltip("Text component to display progress percentage.")]
    [SerializeField] private TextMeshProUGUI progressText;
    
    [Tooltip("Status text to display current load status (e.g. Loading, Activating).")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("Image component used for the background of the loading screen.")]
    [SerializeField] private UnityEngine.UI.Image backgroundImage;

    [Tooltip("Array of background sprites that will be selected randomly for each load.")]
    [SerializeField] private Sprite[] backgroundSprites;

    [Header("Transition Settings")]
    [Tooltip("Minimum time (in seconds) the loading screen should stay active to prevent quick flashes.")]
    [SerializeField] private float minimumTransitionTime = 1.0f;

    private void Start()
    {
        // Choose and display a random background image if configured
        if (backgroundImage != null && backgroundSprites != null && backgroundSprites.Length > 0)
        {
            int randomIndex = Random.Range(0, backgroundSprites.Length);
            backgroundImage.sprite = backgroundSprites[randomIndex];
            Debug.Log($"LoadingSceneController: Set background to random sprite '{backgroundSprites[randomIndex].name}'");
        }

        // Force Time.timeScale to 1 so the loading coroutine update loop isn't paused
        Time.timeScale = 1f;

        // Initialize UI components
        if (progressBar != null) progressBar.value = 0f;
        if (progressText != null) progressText.text = "0%";
        if (statusText != null) statusText.text = "Carregando...";

        StartCoroutine(LoadTargetScenesRoutine());
    }

    private IEnumerator LoadTargetScenesRoutine()
    {
        float startTime = Time.realtimeSinceStartup;

        // Fetch target scenes from SceneLoader
        string[] scenesToLoad = SceneLoader.ScenesToLoad;
        string activeSceneName = SceneLoader.ActiveSceneName;

        // Fallback if scenesToLoad is null or empty
        if (scenesToLoad == null || scenesToLoad.Length == 0)
        {
            Debug.LogWarning("LoadingSceneController: ScenesToLoad is null or empty! Fallback to Menu Inicial.");
            scenesToLoad = new string[] { "Menu Inicial" };
            activeSceneName = "Menu Inicial";
        }

        List<AsyncOperation> loadingOps = new List<AsyncOperation>();

        // Load all target scenes additively on top of the LoadingScene
        foreach (string sceneName in scenesToLoad)
        {
            if (string.IsNullOrEmpty(sceneName)) continue;

            Debug.Log($"LoadingSceneController: Loading scene '{sceneName}' asynchronously...");
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                op.allowSceneActivation = false;
                loadingOps.Add(op);
            }
        }

        // Wait for all scene loading operations to reach 90% (allowSceneActivation = false keeps them at 0.9)
        bool allLoadedToReady = false;
        while (!allLoadedToReady)
        {
            allLoadedToReady = true;
            float sumProgress = 0f;

            foreach (var op in loadingOps)
            {
                // Normalize 0.0 - 0.9 raw progress to 0.0 - 1.0 range
                float normalizedProgress = Mathf.Clamp01(op.progress / 0.9f);
                sumProgress += normalizedProgress;

                if (op.progress < 0.9f)
                {
                    allLoadedToReady = false;
                }
            }

            float averageProgress = loadingOps.Count > 0 ? (sumProgress / loadingOps.Count) : 1f;

            // Update UI components
            if (progressBar != null) progressBar.value = averageProgress;
            if (progressText != null) progressText.text = $"{(int)(averageProgress * 100f)}%";

            yield return null;
        }

        // Ensure minimum visual duration is met to prevent abrupt layout pops
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minimumTransitionTime)
        {
            float waitRemaining = minimumTransitionTime - elapsed;
            float visualWaitStart = Time.realtimeSinceStartup;
            if (progressBar != null) progressBar.value = 1f;
            if (progressText != null) progressText.text = "100%";

            while (Time.realtimeSinceStartup - visualWaitStart < waitRemaining)
            {
                yield return null;
            }
        }

        // Update status text before activating scenes
        if (statusText != null) statusText.text = "Ativando cena...";

        // Now activate the loaded scenes
        foreach (var op in loadingOps)
        {
            op.allowSceneActivation = true;
        }

        // Wait for activation to complete
        foreach (var op in loadingOps)
        {
            while (!op.isDone)
            {
                yield return null;
            }
        }

        // Set specified scene as active
        if (!string.IsNullOrEmpty(activeSceneName))
        {
            Scene loadedScene = SceneManager.GetSceneByName(activeSceneName);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                SceneManager.SetActiveScene(loadedScene);
                Debug.Log($"LoadingSceneController: Set active scene to: '{activeSceneName}'");
            }
            else
            {
                Debug.LogWarning($"LoadingSceneController: Active scene '{activeSceneName}' is not loaded or valid!");
            }
        }

        // Unload the loading scene itself
        Scene loadingScene = gameObject.scene;
        if (loadingScene.IsValid() && loadingScene.isLoaded)
        {
            Debug.Log("LoadingSceneController: Unloading LoadingScene...");
            SceneManager.UnloadSceneAsync(loadingScene);
        }
    }
}
