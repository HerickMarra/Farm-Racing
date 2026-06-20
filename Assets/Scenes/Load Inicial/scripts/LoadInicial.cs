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

    [Header("Loading Performance Settings")]
    [Tooltip("Priority during the background loading phase (while video is playing). Keep Low to prevent video lag.")]
    public ThreadPriority backgroundPriority = ThreadPriority.Low;
    [Tooltip("Priority during the scene activation phase (when video ends/skipped). Keep BelowNormal or Low to prevent freeze.")]
    public ThreadPriority activationPriority = ThreadPriority.BelowNormal;

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
        // Save original background loading priority and set to backgroundPriority to prevent video stuttering
        ThreadPriority originalPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = backgroundPriority;

        // 1. Begin loading both scenes additively in the background immediately
        Debug.Log("Starting background loading of '" + sceneMapName + "' with Low priority...");
        AsyncOperation loadMap = SceneManager.LoadSceneAsync(sceneMapName, LoadSceneMode.Additive);
        if (loadMap != null)
        {
            loadMap.allowSceneActivation = false;
        }

        Debug.Log("Starting background loading of '" + sceneMenuName + "' with Low priority...");
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(sceneMenuName, LoadSceneMode.Additive);
        if (loadMenu != null)
        {
            loadMenu.allowSceneActivation = false;
        }

        // 2. Wait until the video ends or player skips (scenes load up to 90% in background)
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

        // 3. Set background loading priority for activation to avoid main thread choke
        Application.backgroundLoadingPriority = activationPriority;

        // 4. Activate both scenes now that the video has ended or been skipped
        Debug.Log("Video finished. Activating scenes...");
        if (loadMenu != null) loadMenu.allowSceneActivation = true;
        if (loadMap != null) loadMap.allowSceneActivation = true;

        // 5. Wait for Menu UI scene to be fully loaded and active
        if (loadMenu != null)
        {
            while (!loadMenu.isDone)
            {
                yield return null;
            }
        }

        // 6. Set the active scene to the configured UI scene immediately so it displays and starts the radio
        string targetActiveScene = string.IsNullOrEmpty(activeSceneName) ? sceneMenuName : activeSceneName;
        Scene activeScene = SceneManager.GetSceneByName(targetActiveScene);
        if (activeScene.IsValid())
        {
            SceneManager.SetActiveScene(activeScene);
            Debug.Log("Successfully set active scene to: " + targetActiveScene);

            // Start the persistent radio music once the main menu is loaded and active
            if (RadioManager.Instance != null)
            {
                RadioManager.Instance.StartRadio();
            }
        }

        // Wait a frame
        yield return null;

        // 7. Wait for 3D Map scene to finish loading/activating (if not already done)
        if (loadMap != null)
        {
            while (!loadMap.isDone)
            {
                yield return null;
            }
        }

        // Restore original priority
        Application.backgroundLoadingPriority = originalPriority;

        // 6. Unload this Load Inicial scene to free memory
        Scene loadingScene = gameObject.scene;
        if (loadingScene.IsValid() && loadingScene.isLoaded)
        {
            Debug.Log("Unloading initial loading scene...");
            SceneManager.UnloadSceneAsync(loadingScene);
        }
    }
}
