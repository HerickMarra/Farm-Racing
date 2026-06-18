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
        // Save original background loading priority and set to Low to prevent video stuttering
        ThreadPriority originalPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;

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

        // 2. Wait until the video ends or player skips
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

        // 3. Restore background loading priority to High for fast activation
        Application.backgroundLoadingPriority = ThreadPriority.High;

        // 4. Activate the 3D Map scene first and wait for completion
        if (loadMap != null)
        {
            Debug.Log("Activating Map scene...");
            loadMap.allowSceneActivation = true;
            while (!loadMap.isDone)
            {
                yield return null;
            }
        }

        // 5. Then activate the Menu UI scene and wait for completion
        if (loadMenu != null)
        {
            Debug.Log("Activating Menu scene...");
            loadMenu.allowSceneActivation = true;
            while (!loadMenu.isDone)
            {
                yield return null;
            }
        }

        // Restore original priority
        Application.backgroundLoadingPriority = originalPriority;

        // 6. Set the active scene to the configured UI or Map scene
        Scene activeScene = SceneManager.GetSceneByName(activeSceneName);
        if (activeScene.IsValid())
        {
            SceneManager.SetActiveScene(activeScene);
            Debug.Log("Successfully set active scene to: " + activeSceneName);
        }

        // 7. Unload this Load Inicial scene to free memory
        Scene loadingScene = gameObject.scene;
        if (loadingScene.IsValid() && loadingScene.isLoaded)
        {
            Debug.Log("Unloading initial loading scene...");
            SceneManager.UnloadSceneAsync(loadingScene);
        }
    }
}
