using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    public enum RaceState { Countdown, Racing, Finished }

    [Header("Race Config")]
    public int totalLaps = 3;
    public float countdownDuration = 3f;

    [Header("State")]
    public RaceState currentState = RaceState.Countdown;

    private List<KartController> karts = new List<KartController>();
    private KartController playerKart;

    // UI Elements (Created dynamically)
    private Canvas canvas;
    private bool useLegacyUI = false;

    // TMPro Components
    private TextMeshProUGUI countdownText;
    private TextMeshProUGUI playerPosText;
    private TextMeshProUGUI lapText;
    private TextMeshProUGUI leaderboardText;
    private TextMeshProUGUI finishWinnerText;

    // Legacy UI Components
    private Text countdownTextLegacy;
    private Text playerPosTextLegacy;
    private Text lapTextLegacy;
    private Text leaderboardTextLegacy;
    private Text finishWinnerTextLegacy;

    private GameObject finishPanel;

    private void Awake()
    {
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

        // 3. Detect if TextMeshPro can load its default font asset without throwing
        try
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                useLegacyUI = true;
                Debug.LogWarning("TextMeshPro default font is null. Falling back to legacy UI.Text.");
            }
        }
        catch (System.Exception ex)
        {
            useLegacyUI = true;
            Debug.LogWarning("TextMeshPro default font not imported or threw exception: " + ex.Message + ". Falling back to legacy UI.Text.");
        }

        // 4. Create UI
        CreateUIElements();
    }

    private void Start()
    {
        StartCoroutine(RaceCountdownRoutine());
    }

    private void Update()
    {
        if (currentState == RaceState.Racing)
        {
            UpdateRaceProgress();
        }
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

            if (useLegacyUI)
            {
                if (countdownTextLegacy != null)
                {
                    countdownTextLegacy.text = val;
                    countdownTextLegacy.transform.localScale = scale;
                }
            }
            else
            {
                if (countdownText != null)
                {
                    countdownText.text = val;
                    countdownText.transform.localScale = scale;
                }
            }
            yield return null;
            timer -= Time.deltaTime;
        }

        if (useLegacyUI)
        {
            if (countdownTextLegacy != null)
            {
                countdownTextLegacy.text = "GO!";
                countdownTextLegacy.color = Color.green;
                countdownTextLegacy.transform.localScale = Vector3.one * 1.5f;
            }
        }
        else
        {
            if (countdownText != null)
            {
                countdownText.text = "GO!";
                countdownText.color = Color.green;
                countdownText.transform.localScale = Vector3.one * 1.5f;
            }
        }

        // Start the race!
        currentState = RaceState.Racing;
        foreach (var kart in karts)
        {
            kart.controlsEnabled = true;
        }

        yield return new WaitForSeconds(1.2f);

        if (useLegacyUI)
        {
            if (countdownTextLegacy != null)
            {
                countdownTextLegacy.gameObject.SetActive(false);
            }
        }
        else
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(false);
            }
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

            if (useLegacyUI)
            {
                if (playerPosTextLegacy != null) playerPosTextLegacy.text = posStr;
            }
            else
            {
                if (playerPosText != null) playerPosText.text = posStr;
            }

            // Lap
            int pLap = Mathf.Min(playerKart.CurrentLap, totalLaps);
            string lapStr = $"VOLTA: {pLap} / {totalLaps}";

            if (useLegacyUI)
            {
                if (lapTextLegacy != null) lapTextLegacy.text = lapStr;
            }
            else
            {
                if (lapText != null) lapText.text = lapStr;
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
        if (leaderboardText == null && leaderboardTextLegacy == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (!useLegacyUI)
        {
            sb.AppendLine("<size=28><color=#FFDD22><b>CLASSIFICAÇÃO</b></color></size>");
        }
        else
        {
            sb.AppendLine("<b><color=#FFDD22>CLASSIFICAÇÃO</color></b>");
        }
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

        if (useLegacyUI)
        {
            leaderboardTextLegacy.text = sb.ToString();
        }
        else
        {
            leaderboardText.text = sb.ToString();
        }
    }

    private void FinishRace()
    {
        currentState = RaceState.Finished;
        
        if (playerKart != null)
        {
            playerKart.controlsEnabled = false;
        }

        if (finishPanel != null)
        {
            finishPanel.SetActive(true);
            if (playerKart != null)
            {
                int finalPos = playerKart.currentPosition;
                string posWord = finalPos == 1 ? "CAMPEÃO!" : $"{finalPos}º Lugar";
                string displayText = $"FIM DE CORRIDA!\n\nVocê chegou em {posWord}";

                if (useLegacyUI)
                {
                    if (finishWinnerTextLegacy != null) finishWinnerTextLegacy.text = displayText;
                }
                else
                {
                    if (finishWinnerText != null) finishWinnerText.text = displayText;
                }
            }
        }
    }

    public void RestartRace()
    {
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

    private void CreateUIElements()
    {
        // Create Canvas Container
        GameObject canvasGo = new GameObject("RaceHUD_Canvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Color panelBgColor = new Color(0.1f, 0.1f, 0.1f, 0.72f);
        Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. COUNTDOWN TEXT (Center Screen)
        GameObject countGo = new GameObject("CountdownText");
        countGo.transform.SetParent(canvasGo.transform, false);
        RectTransform countRect = countGo.AddComponent<RectTransform>();
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(600, 300);
        
        if (!useLegacyUI)
        {
            countdownText = countGo.AddComponent<TextMeshProUGUI>();
            countdownText.fontSize = 130;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.color = Color.yellow;
            countdownText.fontStyle = FontStyles.Bold;
        }
        else
        {
            countdownTextLegacy = countGo.AddComponent<Text>();
            countdownTextLegacy.font = legacyFont;
            countdownTextLegacy.fontSize = 120;
            countdownTextLegacy.alignment = TextAnchor.MiddleCenter;
            countdownTextLegacy.color = Color.yellow;
        }

        // 2. PLAYER POSITION DISPLAY (Bottom-Left)
        GameObject posGroup = new GameObject("PositionHUDGroup");
        posGroup.transform.SetParent(canvasGo.transform, false);
        RectTransform posGroupRect = posGroup.AddComponent<RectTransform>();
        posGroupRect.anchorMin = new Vector2(0f, 0f);
        posGroupRect.anchorMax = new Vector2(0f, 0f);
        posGroupRect.pivot = new Vector2(0f, 0f);
        posGroupRect.anchoredPosition = new Vector2(60, 60);
        posGroupRect.sizeDelta = new Vector2(250, 150);

        // Position Background Panel
        GameObject posBg = new GameObject("Bg");
        posBg.transform.SetParent(posGroup.transform, false);
        RectTransform posBgRect = posBg.AddComponent<RectTransform>();
        posBgRect.anchorMin = Vector2.zero;
        posBgRect.anchorMax = Vector2.one;
        posBgRect.sizeDelta = Vector2.zero;
        Image posBgImg = posBg.AddComponent<Image>();
        posBgImg.color = panelBgColor;

        // Big Position Number (e.g. 1º)
        GameObject posNumGo = new GameObject("PositionNumber");
        posNumGo.transform.SetParent(posGroup.transform, false);
        RectTransform posNumRect = posNumGo.AddComponent<RectTransform>();
        posNumRect.anchorMin = new Vector2(0.05f, 0.05f);
        posNumRect.anchorMax = new Vector2(0.95f, 0.75f);
        posNumRect.sizeDelta = Vector2.zero;
        
        if (!useLegacyUI)
        {
            playerPosText = posNumGo.AddComponent<TextMeshProUGUI>();
            playerPosText.fontSize = 80;
            playerPosText.alignment = TextAlignmentOptions.Center;
            playerPosText.color = Color.yellow;
            playerPosText.fontStyle = FontStyles.Bold;
            playerPosText.text = "1º";
        }
        else
        {
            playerPosTextLegacy = posNumGo.AddComponent<Text>();
            playerPosTextLegacy.font = legacyFont;
            playerPosTextLegacy.fontSize = 70;
            playerPosTextLegacy.alignment = TextAnchor.MiddleCenter;
            playerPosTextLegacy.color = Color.yellow;
            playerPosTextLegacy.text = "1º";
        }

        // Label above the position
        GameObject posLblGo = new GameObject("PositionLabel");
        posLblGo.transform.SetParent(posGroup.transform, false);
        RectTransform posLblRect = posLblGo.AddComponent<RectTransform>();
        posLblRect.anchorMin = new Vector2(0.1f, 0.75f);
        posLblRect.anchorMax = new Vector2(0.9f, 0.95f);
        posLblRect.sizeDelta = Vector2.zero;
        
        if (!useLegacyUI)
        {
            var posLbl = posLblGo.AddComponent<TextMeshProUGUI>();
            posLbl.fontSize = 18;
            posLbl.alignment = TextAlignmentOptions.Left;
            posLbl.color = Color.white;
            posLbl.text = "POSIÇÃO";
        }
        else
        {
            var posLbl = posLblGo.AddComponent<Text>();
            posLbl.font = legacyFont;
            posLbl.fontSize = 14;
            posLbl.alignment = TextAnchor.MiddleLeft;
            posLbl.color = Color.white;
            posLbl.text = "POSIÇÃO";
        }

        // 3. LAP DISPLAY (Top-Left)
        GameObject lapGroup = new GameObject("LapHUDGroup");
        lapGroup.transform.SetParent(canvasGo.transform, false);
        RectTransform lapGroupRect = lapGroup.AddComponent<RectTransform>();
        lapGroupRect.anchorMin = new Vector2(0f, 1f);
        lapGroupRect.anchorMax = new Vector2(0f, 1f);
        lapGroupRect.pivot = new Vector2(0f, 1f);
        lapGroupRect.anchoredPosition = new Vector2(60, -60);
        lapGroupRect.sizeDelta = new Vector2(300, 75);

        // Background
        GameObject lapBg = new GameObject("Bg");
        lapBg.transform.SetParent(lapGroup.transform, false);
        RectTransform lapBgRect = lapBg.AddComponent<RectTransform>();
        lapBgRect.anchorMin = Vector2.zero;
        lapBgRect.anchorMax = Vector2.one;
        lapBgRect.sizeDelta = Vector2.zero;
        Image lapBgImg = lapBg.AddComponent<Image>();
        lapBgImg.color = panelBgColor;

        // Text
        GameObject lapTextGo = new GameObject("LapText");
        lapTextGo.transform.SetParent(lapGroup.transform, false);
        RectTransform lapTextRect = lapTextGo.AddComponent<RectTransform>();
        lapTextRect.anchorMin = new Vector2(0.05f, 0.1f);
        lapTextRect.anchorMax = new Vector2(0.95f, 0.9f);
        lapTextRect.sizeDelta = Vector2.zero;

        if (!useLegacyUI)
        {
            lapText = lapTextGo.AddComponent<TextMeshProUGUI>();
            lapText.fontSize = 32;
            lapText.alignment = TextAlignmentOptions.Center;
            lapText.color = Color.white;
            lapText.fontStyle = FontStyles.Bold;
            lapText.text = "VOLTA: 1 / 3";
        }
        else
        {
            lapTextLegacy = lapTextGo.AddComponent<Text>();
            lapTextLegacy.font = legacyFont;
            lapTextLegacy.fontSize = 26;
            lapTextLegacy.alignment = TextAnchor.MiddleCenter;
            lapTextLegacy.color = Color.white;
            lapTextLegacy.text = "VOLTA: 1 / 3";
        }

        // 4. LEADERBOARD PANEL (Top-Right)
        GameObject leaderGroup = new GameObject("LeaderboardHUDGroup");
        leaderGroup.transform.SetParent(canvasGo.transform, false);
        RectTransform leaderGroupRect = leaderGroup.AddComponent<RectTransform>();
        leaderGroupRect.anchorMin = new Vector2(1f, 1f);
        leaderGroupRect.anchorMax = new Vector2(1f, 1f);
        leaderGroupRect.pivot = new Vector2(1f, 1f);
        leaderGroupRect.anchoredPosition = new Vector2(-60, -60);
        leaderGroupRect.sizeDelta = new Vector2(350, 260);

        // Background
        GameObject leaderBg = new GameObject("Bg");
        leaderBg.transform.SetParent(leaderGroup.transform, false);
        RectTransform leaderBgRect = leaderBg.AddComponent<RectTransform>();
        leaderBgRect.anchorMin = Vector2.zero;
        leaderBgRect.anchorMax = Vector2.one;
        leaderBgRect.sizeDelta = Vector2.zero;
        Image leaderBgImg = leaderBg.AddComponent<Image>();
        leaderBgImg.color = panelBgColor;

        // Leaderboard Text
        GameObject leaderTextGo = new GameObject("LeaderboardText");
        leaderTextGo.transform.SetParent(leaderGroup.transform, false);
        RectTransform leaderTextRect = leaderTextGo.AddComponent<RectTransform>();
        leaderTextRect.anchorMin = new Vector2(0.05f, 0.05f);
        leaderTextRect.anchorMax = new Vector2(0.95f, 0.95f);
        leaderTextRect.sizeDelta = Vector2.zero;

        if (!useLegacyUI)
        {
            leaderboardText = leaderTextGo.AddComponent<TextMeshProUGUI>();
            leaderboardText.fontSize = 18;
            leaderboardText.text = "";
        }
        else
        {
            leaderboardTextLegacy = leaderTextGo.AddComponent<Text>();
            leaderboardTextLegacy.font = legacyFont;
            leaderboardTextLegacy.fontSize = 15;
            leaderboardTextLegacy.alignment = TextAnchor.UpperLeft;
            leaderboardTextLegacy.color = Color.white;
            leaderboardTextLegacy.text = "";
        }

        // 5. FINISH OVERLAY PANEL (Center, Hidden by default)
        finishPanel = new GameObject("FinishPanel");
        finishPanel.transform.SetParent(canvasGo.transform, false);
        RectTransform finishRect = finishPanel.AddComponent<RectTransform>();
        finishRect.anchorMin = Vector2.zero;
        finishRect.anchorMax = Vector2.one;
        finishRect.sizeDelta = Vector2.zero;
        finishPanel.SetActive(false);

        // Full Screen transparent black overlay
        Image finishOverlayImg = finishPanel.AddComponent<Image>();
        finishOverlayImg.color = new Color(0f, 0f, 0f, 0.85f);

        // Inner Box Container
        GameObject innerFinishGo = new GameObject("MessageBox");
        innerFinishGo.transform.SetParent(finishPanel.transform, false);
        RectTransform innerFinishRect = innerFinishGo.AddComponent<RectTransform>();
        innerFinishRect.anchoredPosition = Vector2.zero;
        innerFinishRect.sizeDelta = new Vector2(500, 400);
        Image innerFinishImg = innerFinishGo.AddComponent<Image>();
        innerFinishImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Title and Info
        GameObject winTextGo = new GameObject("WinnerText");
        winTextGo.transform.SetParent(innerFinishGo.transform, false);
        RectTransform winTextRect = winTextGo.AddComponent<RectTransform>();
        winTextRect.anchorMin = new Vector2(0.05f, 0.35f);
        winTextRect.anchorMax = new Vector2(0.95f, 0.95f);
        winTextRect.sizeDelta = Vector2.zero;

        if (!useLegacyUI)
        {
            finishWinnerText = winTextGo.AddComponent<TextMeshProUGUI>();
            finishWinnerText.fontSize = 28;
            finishWinnerText.alignment = TextAlignmentOptions.Center;
            finishWinnerText.color = Color.white;
            finishWinnerText.text = "FIM DE CORRIDA!";
        }
        else
        {
            finishWinnerTextLegacy = winTextGo.AddComponent<Text>();
            finishWinnerTextLegacy.font = legacyFont;
            finishWinnerTextLegacy.fontSize = 24;
            finishWinnerTextLegacy.alignment = TextAnchor.MiddleCenter;
            finishWinnerTextLegacy.color = Color.white;
            finishWinnerTextLegacy.text = "FIM DE CORRIDA!";
        }

        // Restart Button
        GameObject restartBtnGo = new GameObject("RestartButton");
        restartBtnGo.transform.SetParent(innerFinishGo.transform, false);
        RectTransform restartBtnRect = restartBtnGo.AddComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.2f, 0.1f);
        restartBtnRect.anchorMax = new Vector2(0.8f, 0.28f);
        restartBtnRect.sizeDelta = Vector2.zero;

        Image btnImg = restartBtnGo.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.75f, 0.25f, 1f); // Vibrant green
        Button btnComp = restartBtnGo.AddComponent<Button>();
        btnComp.onClick.AddListener(RestartRace);

        // Button Text
        GameObject btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(restartBtnGo.transform, false);
        RectTransform btnTextRect = btnTextGo.AddComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        if (!useLegacyUI)
        {
            var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnText.fontSize = 22;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;
            btnText.text = "CORRER DE NOVO";
        }
        else
        {
            var btnText = btnTextGo.AddComponent<Text>();
            btnText.font = legacyFont;
            btnText.fontSize = 18;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = Color.white;
            btnText.text = "CORRER DE NOVO";
        }
    }
}
