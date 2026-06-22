using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("The main menu panel or canvas that should be deactivated when starting the game.")]
    [SerializeField] private GameObject menuToDeactivate;
    [Tooltip("Optional: A panel or text that will be activated while scenes are loading (e.g. Loading Screen).")]
    [SerializeField] private GameObject loadingPanel;

    [Header("Scene Loading Settings")]
    [Tooltip("List of scene names to load additively in the background.")]
    [SerializeField] private string[] scenesToLoad = new string[] { "Fazenda Veloz" };

    [Tooltip("Optional: Name of the scene to set active after loading. If empty, the first scene in the array is set active.")]
    [SerializeField] private string activeSceneName = "Fazenda Veloz";

    private bool isStarting = false;

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
