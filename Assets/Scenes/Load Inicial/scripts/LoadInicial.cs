using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.InputSystem;

public class LoadInicial : MonoBehaviour
{
    [Header("Video Settings")]
    [Tooltip("The VideoPlayer component responsible for playing the splash intro video.")]
    public VideoPlayer videoPlayer;
    [Tooltip("If true, pressing Space, Enter, or Escape skips the video.")]
    public bool allowSkip = true;

    [Header("Scene Loading Config")]
    [Tooltip("Name of the 3D Map scene (e.g. Fazenda Veloz).")]
    public string sceneMapName = "Fazenda Veloz";
    [Tooltip("Name of the HUD/UI Menu scene (e.g. Menu Inicial).")]
    public string sceneMenuName = "Menu Inicial";
    [Tooltip("Which scene should be set as active after both are loaded.")]
    public string activeSceneName = "Menu Inicial";

    private bool videoFinished = false;

    private void Start()
    {
        // Auto-find VideoPlayer on the same GameObject if not assigned in inspector
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        // Play the video if configured, otherwise transition immediately
        if (videoPlayer != null && (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url)))
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            videoPlayer.Play();
        }
        else
        {
            videoFinished = true;
            Debug.LogWarning("VideoPlayer is missing a video clip or URL. Transitioning immediately.");
        }

        // Start loading the scenes additively in the background
        StartCoroutine(LoadScenesRoutine());
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        videoFinished = true;
        Debug.Log("Video finished playing naturally.");
    }

    private IEnumerator LoadScenesRoutine()
    {
        // 1. Wait until the video ends or player skips
        while (!videoFinished)
        {
            if (allowSkip && Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || 
                    Keyboard.current.enterKey.wasPressedThisFrame || 
                    Keyboard.current.escapeKey.wasPressedThisFrame || 
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    videoFinished = true;
                    if (videoPlayer != null && videoPlayer.isPlaying)
                    {
                        videoPlayer.Stop();
                    }
                    Debug.Log("Video skipped by player input.");
                    break;
                }
            }
            yield return null;
        }

        // 2. Begin loading the scenes additively AFTER the video finishes
        Debug.Log("Video finished. Starting additive loading of '" + sceneMapName + "' and '" + sceneMenuName + "'...");
        AsyncOperation loadMap = SceneManager.LoadSceneAsync(sceneMapName, LoadSceneMode.Additive);
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(sceneMenuName, LoadSceneMode.Additive);

        // 3. Wait for the loading operations to complete
        while ((loadMap != null && !loadMap.isDone) || (loadMenu != null && !loadMenu.isDone))
        {
            yield return null;
        }

        // 4. Set the active scene to the configured UI or Map scene
        Scene activeScene = SceneManager.GetSceneByName(activeSceneName);
        if (activeScene.IsValid())
        {
            SceneManager.SetActiveScene(activeScene);
            Debug.Log("Successfully set active scene to: " + activeSceneName);
        }

        // 5. Unload this Load Inicial scene to free memory
        Scene loadingScene = gameObject.scene;
        if (loadingScene.IsValid() && loadingScene.isLoaded)
        {
            Debug.Log("Unloading initial loading scene...");
            SceneManager.UnloadSceneAsync(loadingScene);
        }
    }
}
