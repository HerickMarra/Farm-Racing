using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    /// <summary>
    /// List of scene names that need to be loaded during the next transition.
    /// </summary>
    public static string[] ScenesToLoad { get; private set; }

    /// <summary>
    /// The name of the scene that should be set active once all scenes are loaded.
    /// </summary>
    public static string ActiveSceneName { get; private set; }

    /// <summary>
    /// Transition to a single target scene via the intermediary LoadingScene.
    /// </summary>
    /// <param name="sceneName">The name of the target scene.</param>
    public static void LoadScene(string sceneName)
    {
        LoadScenes(new string[] { sceneName }, sceneName);
    }

    /// <summary>
    /// Transition to multiple target scenes (e.g. Map + HUD additively) via the LoadingScene.
    /// </summary>
    /// <param name="scenes">Array of scene names to load.</param>
    /// <param name="activeSceneName">The scene that should be marked as active when loaded.</param>
    public static void LoadScenes(string[] scenes, string activeSceneName = null)
    {
        ScenesToLoad = scenes;
        ActiveSceneName = string.IsNullOrEmpty(activeSceneName) && scenes.Length > 0 ? scenes[0] : activeSceneName;

        Debug.Log($"SceneLoader: Transitioning to Load scene to load {scenes.Length} target scene(s)...");
        SceneManager.LoadScene("Load");
    }
}
