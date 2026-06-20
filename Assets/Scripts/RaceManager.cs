using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    public enum RaceState { Intro, Countdown, Racing, Finished }

    [Header("Race Config")]
    public int totalLaps = 3;
    public float countdownDuration = 3f;

    [Header("State")]
    public RaceState currentState = RaceState.Intro;

    [Header("UI References")]
    [SerializeField] private UnityEngine.Canvas canvas;
    [SerializeField] private TMPro.TextMeshProUGUI countdownText;
    [SerializeField] private TMPro.TextMeshProUGUI playerPosText;
    [SerializeField] private TMPro.TextMeshProUGUI lapText;
    [SerializeField] private TMPro.TextMeshProUGUI leaderboardText;
    [SerializeField] private TMPro.TextMeshProUGUI finishWinnerText;
    [SerializeField] private GameObject introText;
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private UnityEngine.UI.Button restartButton;
    [SerializeField] private GameObject pausePanel;

    private List<KartController> karts = new List<KartController>();
    private KartController playerKart;
    private bool isPaused = false;

    private void Awake()
    {
        Time.timeScale = 1f; // Reset time scale in case we reloaded from pause state
        currentState = RaceState.Intro;

        // 1. Find all karts in the scene
        karts = new List<KartController>(Object.FindObjectsByType<KartController>(FindObjectsSortMode.None));
        foreach (var kart in karts)
        {
            if (kart.isPlayer)
            {
                playerKart = kart;
            }
            // Freeze controls on start
            kart.controlsEnabled = false;
        }

        if (playerKart == null && karts.Count > 0)
        {
            playerKart = karts[0]; // Fallback if no player marked
        }

        // 2. Setup EventSystem if missing
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // 3. Configure Restart Button Listener
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartRace);
        }

        // Ensure proper initial UI visibility
        if (introText != null) introText.SetActive(true);
        if (countdownText != null) countdownText.gameObject.SetActive(true);
        if (finishPanel != null) finishPanel.SetActive(false);
    }

    private void Start()
    {
        // Karts start frozen, waiting in Intro state
        foreach (var kart in karts)
        {
            kart.controlsEnabled = false;
        }
    }

    private void Update()
    {
        // Toggle pause when Escape or P is pressed during countdown or racing
        if (currentState == RaceState.Countdown || currentState == RaceState.Racing)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && 
                (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame || 
                 UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame))
            {
                TogglePause();
            }
        }

        if (isPaused) return;

        if (currentState == RaceState.Intro)
        {
            // Pulse the start text
            float pulse = 1f + Mathf.PingPong(Time.time * 2f, 0.15f);
            if (introText != null)
            {
                introText.transform.localScale = Vector3.one * pulse;
            }

            // Detect space key press to start the race
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartRaceCountdown();
            }
        }
        else if (currentState == RaceState.Racing)
        {
            UpdateRaceProgress();
        }
    }

    public void TogglePause()
    {
        if (currentState != RaceState.Countdown && currentState != RaceState.Racing) return;

        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
        else
        {
            // Dynamic fallback if no pause panel is assigned in inspector
            GameObject dynamicPausePanel = GameObject.Find("PausePanel");
            if (dynamicPausePanel != null)
            {
                dynamicPausePanel.SetActive(isPaused);
            }
        }

        Debug.Log(isPaused ? "Game Paused (TimeScale = 0)" : "Game Resumed (TimeScale = 1)");
    }

    public void StartRaceCountdown()
    {
        if (currentState != RaceState.Intro) return;
        currentState = RaceState.Countdown;

        if (introText != null) introText.gameObject.SetActive(false);

        // Tell Camera to snap to target and end intro flyby
        CameraController cam = Object.FindAnyObjectByType<CameraController>();
        if (cam != null)
        {
            cam.EndIntroMode();
        }

        StartCoroutine(RaceCountdownRoutine());
    }

    private IEnumerator RaceCountdownRoutine()
    {
        currentState = RaceState.Countdown;
        foreach (var kart in karts)
        {
            kart.controlsEnabled = false;
        }

        float timer = countdownDuration;
        while (timer > 0f)
        {
            string val = Mathf.Ceil(timer).ToString("F0");
            Vector3 scale = Vector3.one * (1f + (timer % 1f) * 0.4f);

            if (countdownText != null)
            {
                countdownText.text = val;
                countdownText.transform.localScale = scale;
            }
            yield return null;
            timer -= Time.deltaTime;
        }

        if (countdownText != null)
        {
            countdownText.text = "GO!";
            countdownText.color = Color.green;
            countdownText.transform.localScale = Vector3.one * 1.5f;
        }

        // Start the race!
        currentState = RaceState.Racing;
        foreach (var kart in karts)
        {
            kart.controlsEnabled = true;
        }

        yield return new WaitForSeconds(1.2f);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    private void UpdateRaceProgress()
    {
        if (karts.Count == 0) return;

        // Sort karts by current progress (descending)
        karts.Sort((a, b) => b.GetRaceProgress().CompareTo(a.GetRaceProgress()));

        // Assign positions
        for (int i = 0; i < karts.Count; i++)
        {
            karts[i].currentPosition = i + 1;
        }

        // Update player HUD elements
        if (playerKart != null)
        {
            // Position
            int pPos = playerKart.currentPosition;
            string posStr = $"{pPos}{GetPositionSuffix(pPos)}";

            if (playerPosText != null)
            {
                playerPosText.text = posStr;
            }

            // Lap
            int pLap = Mathf.Min(playerKart.CurrentLap, totalLaps);
            string lapStr = $"VOLTA: {pLap} / {totalLaps}";

            if (lapText != null)
            {
                lapText.text = lapStr;
            }

            // Check if player has finished the final lap
            if (playerKart.CurrentLap > totalLaps)
            {
                FinishRace();
            }
        }

        // Update Leaderboard panel
        UpdateLeaderboardUI();
    }

    private void UpdateLeaderboardUI()
    {
        if (leaderboardText == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=28><color=#FFDD22><b>CLASSIFICAÇÃO</b></color></size>");
        sb.AppendLine("--------------------------");

        for (int i = 0; i < karts.Count; i++)
        {
            KartController k = karts[i];
            string kName = GetCleanName(k.gameObject.name);
            string posPrefix = $"{i + 1}º ";

            // Format line
            string colorTag = k.isPlayer ? "<color=#00FF44>" : "<color=#FFFFFF>";
            string endColor = "</color>";

            string lapStr = k.CurrentLap > totalLaps ? "FINALIZOU" : $"V{Mathf.Min(k.CurrentLap, totalLaps)}";

            sb.AppendLine($"{colorTag}{posPrefix}{kName.PadRight(15)} ({lapStr}){endColor}");
        }

        leaderboardText.text = sb.ToString();
    }

    private void FinishRace()
    {
        currentState = RaceState.Finished;
        
        if (playerKart != null)
        {
            playerKart.isPlayer = false; // Switch control to AI
            playerKart.controlsEnabled = true; // Keep controls active so the AI can drive
            playerKart.aiDifficulty = KartController.AIDifficulty.Medio;
        }

        // Activate cinematic camera mode
        CameraController cam = Object.FindAnyObjectByType<CameraController>();
        if (cam != null)
        {
            cam.StartCinemaMode(karts);
        }

        if (finishPanel != null)
        {
            finishPanel.SetActive(true);
            if (playerKart != null)
            {
                int finalPos = playerKart.currentPosition;
                string posWord = finalPos == 1 ? "CAMPEÃO!" : $"{finalPos}º Lugar";
                string displayText = $"FIM DE CORRIDA!\n\nVocê chegou em {posWord}";

                if (finishWinnerText != null)
                {
                    finishWinnerText.text = displayText;
                }
            }
        }
    }

    public void RestartRace()
    {
        Time.timeScale = 1f; // Reset time scale on restart
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private string GetPositionSuffix(int position)
    {
        return "º";
    }

    private string GetCleanName(string rawName)
    {
        switch (rawName.ToLower())
        {
            case "milk": return "Milk (Você)";
            case "pig": return "Porco";
            case "touro": return "Touro";
            case "fazendeiro": return "Fazendeiro";
            default: return rawName;
        }
    }
}

