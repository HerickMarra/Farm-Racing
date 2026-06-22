using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }

    [Header("Playlist Config")]
    [Tooltip("List of AudioClips representing the radio stations/songs.")]
    public AudioClip[] playlist;
    [Tooltip("Target playback volume for the music.")]
    [Range(0f, 1f)]
    public float maxVolume = 0.4f;
    [Tooltip("Duration of the crossfade transition in seconds.")]
    public float crossfadeDuration = 3.5f;

    [Header("Muffle Effect (Low Pass Pause)")]
    [Tooltip("Cutoff frequency when the music is muffled (lower values = more muffled, e.g., 1000f).")]
    public float muffleCutoffFrequency = 1000f;
    [Tooltip("If true, manually muffles the music regardless of pause state.")]
    public bool manualMuffle = false;

    [Header("Notification UI Animation")]
    [Tooltip("Offset from the original position where the notification starts sliding from.")]
    public Vector2 slideOffset = new Vector2(-450f, 0f);
    [Tooltip("Duration of the slide-in and slide-out animations in seconds.")]
    public float slideDuration = 0.6f;
    [Tooltip("How long the notification stays fully visible on screen (seconds).")]
    public float notificationDisplayTime = 3.5f;

    [Header("Audio Sources (Optional Assignment)")]
    public AudioSource audioSourceA;
    public AudioSource audioSourceB;

    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private AudioLowPassFilter lowPassFilterA;
    private AudioLowPassFilter lowPassFilterB;
    private int currentTrackIndex = -1;
    private bool isTransitioning = false;
    private bool isRadioStarted = false;
    private Coroutine notificationCoroutine;

    private Vector2 originalNotificationPosition;
    private bool hasStoredOriginalPosition = false;
    private GameObject lastNotificationObj;
    private bool cachedOptionsOpen = false;
    private int lastOptionsCheckFrame = -1;

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad to persist between scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Dynamically add AudioSource components if not assigned in the inspector
        if (audioSourceA == null) audioSourceA = gameObject.AddComponent<AudioSource>();
        if (audioSourceB == null) audioSourceB = gameObject.AddComponent<AudioSource>();

        ConfigureSource(audioSourceA, out lowPassFilterA);
        ConfigureSource(audioSourceB, out lowPassFilterB);
    }

    private void ConfigureSource(AudioSource source, out AudioLowPassFilter filter)
    {
        source.spatialBlend = 0f; // 2D Stereo Sound (perfect for background music)
        source.playOnAwake = false;
        source.loop = false; // We handle track loop and transitions manually

        // Check or add Low Pass Filter for Paused/Muffled state
        filter = source.GetComponent<AudioLowPassFilter>();
        if (filter == null)
        {
            filter = source.gameObject.AddComponent<AudioLowPassFilter>();
        }
        filter.cutoffFrequency = 22000f; // Start fully open (unmuffled)
    }

    private void Start()
    {
        activeSource = audioSourceA;
        inactiveSource = audioSourceB;
    }

    private void Update()
    {
        if (!isRadioStarted) return;

        // Smoothly interpolate cutoff frequency using unscaledDeltaTime (to work while Time.timeScale = 0)
        UpdateMuffleEffect();

        // Dynamically adjust volume to maxVolume if not in transition (supports real-time volume sliders)
        if (!isTransitioning && activeSource != null && activeSource.isPlaying)
        {
            activeSource.volume = maxVolume;
        }

        if (playlist == null || playlist.Length == 0) return;

        // Check if current song is close to ending to trigger a crossfade
        if (activeSource != null && activeSource.isPlaying && !isTransitioning)
        {
            float remainingTime = activeSource.clip.length - activeSource.time;
            if (remainingTime <= crossfadeDuration)
            {
                StartCoroutine(CrossfadeToNextTrackRoutine());
            }
        }
        // Fallback if the active song stopped playing unexpectedly
        else if (activeSource != null && !activeSource.isPlaying && !isTransitioning)
        {
            StartCoroutine(CrossfadeToNextTrackRoutine());
        }
    }

    private bool IsOptionsPanelActive()
    {
        if (Time.frameCount == lastOptionsCheckFrame)
        {
            return cachedOptionsOpen;
        }
        lastOptionsCheckFrame = Time.frameCount;

        // Perform check only once every 10 frames to optimize performance
        if (Time.frameCount % 10 != 0)
        {
            return cachedOptionsOpen;
        }

        GameObject optionsPanel = GameObject.Find("OptionsMenu");
        if (optionsPanel == null) optionsPanel = GameObject.Find("OptionsPanel");
        if (optionsPanel == null) optionsPanel = GameObject.Find("PainelOpcoes");

        cachedOptionsOpen = (optionsPanel != null && optionsPanel.activeInHierarchy);
        return cachedOptionsOpen;
    }

    private void UpdateMuffleEffect()
    {
        bool shouldMuffle = (Time.timeScale == 0f) || manualMuffle || IsOptionsPanelActive();
        float targetFrequency = shouldMuffle ? muffleCutoffFrequency : 22000f;

        if (lowPassFilterA != null && lowPassFilterB != null)
        {
            // Smoothly sweep the frequency (analog filter effect)
            float newCutoff = Mathf.Lerp(lowPassFilterA.cutoffFrequency, targetFrequency, 10f * Time.unscaledDeltaTime);
            
            lowPassFilterA.cutoffFrequency = newCutoff;
            lowPassFilterB.cutoffFrequency = newCutoff;
        }
    }

    private int GetRandomTrackIndex()
    {
        if (playlist == null || playlist.Length == 0) return -1;
        if (playlist.Length == 1) return 0;

        int nextIndex;
        do
        {
            nextIndex = Random.Range(0, playlist.Length);
        } while (nextIndex == currentTrackIndex);

        return nextIndex;
    }

    private IEnumerator CrossfadeToNextTrackRoutine()
    {
        isTransitioning = true;

        int nextIndex = GetRandomTrackIndex();
        if (nextIndex == -1)
        {
            isTransitioning = false;
            yield break;
        }

        currentTrackIndex = nextIndex;
        AudioClip nextClip = playlist[nextIndex];
        Debug.Log("Radio crossfading to: " + nextClip.name);

        // Swap active and inactive sources
        AudioSource prevActive = activeSource;
        activeSource = (activeSource == audioSourceA) ? audioSourceB : audioSourceA;
        inactiveSource = prevActive;

        // Setup and play new active track
        activeSource.clip = nextClip;
        activeSource.volume = 0f;
        activeSource.Play();

        // Trigger UI pop-up notification
        ShowNowPlayingNotification(nextClip.name);

        float timer = 0f;
        float startInactiveVolume = inactiveSource.volume;

        while (timer < crossfadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / crossfadeDuration;

            // Interpolate volumes smoothly
            activeSource.volume = Mathf.Lerp(0f, maxVolume, t);
            if (inactiveSource != null && inactiveSource.isPlaying)
            {
                inactiveSource.volume = Mathf.Lerp(startInactiveVolume, 0f, t);
            }

            yield return null;
        }

        // Lock final volume settings
        activeSource.volume = maxVolume;
        if (inactiveSource != null)
        {
            inactiveSource.volume = 0f;
            inactiveSource.Stop();
        }

        isTransitioning = false;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private float EaseInCubic(float t)
    {
        return t * t * t;
    }

    private void ShowNowPlayingNotification(string trackName)
    {
        // Dynamically find a GameObject named "RadioNowPlaying" in the active scene
        GameObject notificationObj = GameObject.Find("RadioNowPlaying");
        if (notificationObj != null)
        {
            // Cache the original position if this is a new GameObject instance (e.g. new scene)
            if (lastNotificationObj != notificationObj)
            {
                lastNotificationObj = notificationObj;
                RectTransform rect = notificationObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    originalNotificationPosition = rect.anchoredPosition;
                    hasStoredOriginalPosition = true;
                }
                else
                {
                    hasStoredOriginalPosition = false;
                }
            }

            if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
            notificationCoroutine = StartCoroutine(AnimateNotificationRoutine(notificationObj, trackName));
        }
    }

    private IEnumerator AnimateNotificationRoutine(GameObject panel, string trackName)
    {
        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        // Find TextMeshProUGUI or standard Text in panel's children
        TMPro.TextMeshProUGUI textMesh = panel.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        UnityEngine.UI.Text legacyText = null;
        if (textMesh == null) legacyText = panel.GetComponentInChildren<UnityEngine.UI.Text>();

        // Format clean display name
        string displayName = trackName.Replace(".mp3", "").Replace(".wav", "").Replace(".ogg", "");
        string textContent = "♪ Tocando Agora: " + displayName;

        if (textMesh != null) textMesh.text = textContent;
        else if (legacyText != null) legacyText.text = textContent;

        // Get or add CanvasGroup for smooth fading
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();

        Vector2 targetPos = hasStoredOriginalPosition ? originalNotificationPosition : (rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero);
        Vector2 startPos = targetPos + slideOffset;

        // Reset state
        if (rectTransform != null) rectTransform.anchoredPosition = startPos;
        group.alpha = 0f;
        panel.SetActive(true);

        // Slide In & Fade In
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float tPos = EaseOutBack(t);
            float tAlpha = Mathf.Clamp01(t * 1.5f); // Fade in slightly faster than sliding

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, tPos);
            }
            group.alpha = tAlpha;
            yield return null;
        }
        if (rectTransform != null) rectTransform.anchoredPosition = targetPos;
        group.alpha = 1f;

        // Wait display duration (unscaled to support show during pause)
        float displayTimer = 0f;
        while (displayTimer < notificationDisplayTime)
        {
            displayTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        // Slide Out & Fade Out
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float tPos = EaseInCubic(t);
            float tAlpha = 1f - t;

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(targetPos, startPos, tPos);
            }
            group.alpha = tAlpha;
            yield return null;
        }
        if (rectTransform != null) rectTransform.anchoredPosition = startPos;
        group.alpha = 0f;
        panel.SetActive(false);
    }

    /// <summary>
    /// Starts the radio playback (typically called once the player reaches the main menu).
    /// </summary>
    public void StartRadio()
    {
        if (isRadioStarted) return;
        isRadioStarted = true;

        if (playlist != null && playlist.Length > 0)
        {
            currentTrackIndex = GetRandomTrackIndex();
            if (currentTrackIndex != -1)
            {
                activeSource.clip = playlist[currentTrackIndex];
                activeSource.volume = maxVolume;
                activeSource.Play();
                Debug.Log("Radio started playing at menu entrance: " + playlist[currentTrackIndex].name);

                // Trigger UI pop-up notification
                ShowNowPlayingNotification(playlist[currentTrackIndex].name);
            }
        }
    }

    /// <summary>
    /// Forcefully skips to a new random track in the playlist.
    /// </summary>
    public void SkipToNextTrack()
    {
        if (playlist == null || playlist.Length <= 1) return;
        if (!isRadioStarted) return;
        StopAllCoroutines();
        StartCoroutine(CrossfadeToNextTrackRoutine());
    }

    /// <summary>
    /// Forcefully sets the muffled low-pass filter state manually.
    /// </summary>
    /// <param name="muffle">True to muffle, False to restore normal sound.</param>
    public void SetMuffled(bool muffle)
    {
        manualMuffle = muffle;
    }

    /// <summary>
    /// Safely unloads and reloads a scene additively, keeping other scenes (like Menu Inicial) intact.
    /// Since RadioManager is DontDestroyOnLoad, it won't be destroyed during the reload process.
    /// </summary>
    public void ReloadSceneAdditive(string sceneName)
    {
        StartCoroutine(ReloadSceneAdditiveRoutine(sceneName));
    }

    private IEnumerator ReloadSceneAdditiveRoutine(string sceneName)
    {
        Time.timeScale = 1f;

        // 1. Unload the scene if loaded
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            Debug.Log($"RadioManager: Unloading scene '{sceneName}' additively...");
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            while (unloadOp != null && !unloadOp.isDone)
            {
                yield return null;
            }
            Debug.Log($"RadioManager: Unloaded scene '{sceneName}'.");
        }

        // 2. Load the scene additively
        Debug.Log($"RadioManager: Loading scene '{sceneName}' additively...");
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (loadOp != null && !loadOp.isDone)
        {
            yield return null;
        }

        // 3. Set the reloaded scene as active
        Scene loadedScene = SceneManager.GetSceneByName(sceneName);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
        {
            SceneManager.SetActiveScene(loadedScene);
            Debug.Log($"RadioManager: Set active scene to '{sceneName}'.");
        }
    }
}
