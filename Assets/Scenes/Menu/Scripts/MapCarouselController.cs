using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class MapCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Data Source")]
    [SerializeField] private MapList mapList;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform cardsContainer;

    [Header("Layout Settings")]
    [SerializeField] private float cardSpacing = 280f;
    [SerializeField] private float minScale = 0.75f;
    [SerializeField] private float maxScale = 1.15f;
    [SerializeField] private float minAlpha = 0.5f;
    [SerializeField] private float maxAlpha = 1.0f;

    [Header("Animation Settings")]
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private float autoScrollSpeed = 10f;

    [Header("UI Controls")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button jogarButton;
    [SerializeField] private Button voltarButton;
    [SerializeField] private TextMeshProUGUI selectedMapNameText;

    [Header("References to Main Menu Panels")]
    [SerializeField] private GameObject mainMenuArea;
    [SerializeField] private GameObject selectMapArea;

    private List<MapCardUI> spawnedCards = new List<MapCardUI>();
    
    private float currentCenterIndex = 0f;
    private float targetCenterIndex = 0f;
    private float centerIndexVelocity = 0f;
    private bool isDragging = false;
    private float dragStartCenterIndex = 0f;
    private float dragStartMouseX = 0f;

    // Cooldown for keyboard/gamepad navigation
    private float inputCooldownTimer = 0f;
    private const float INPUT_COOLDOWN = 0.25f;

    private void Start()
    {
        InitializeCarousel();
        SetupButtons();
    }

    private void OnEnable()
    {
        // Focus/Reset target when screen is opened
        targetCenterIndex = 0f;
        currentCenterIndex = 0f;
        UpdateCardsLayout(true); // Force immediate update without smoothing
    }

    private void InitializeCarousel()
    {
        if (mapList == null || mapList.maps == null || mapList.maps.Count == 0)
        {
            Debug.LogError("MapCarouselController: MapList is missing or empty!");
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("MapCarouselController: Card Prefab is not assigned!");
            return;
        }

        // Clean existing children first
        foreach (Transform child in cardsContainer)
        {
            Destroy(child.gameObject);
        }
        spawnedCards.Clear();

        // Spawn a card for each map
        for (int i = 0; i < mapList.maps.Count; i++)
        {
            GameObject cardGo = Instantiate(cardPrefab, cardsContainer);
            cardGo.name = $"Card_Map_{i}_{mapList.maps[i].mapName}";
            
            MapCardUI cardUI = cardGo.GetComponent<MapCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(mapList.maps[i]);
                spawnedCards.Add(cardUI);

                // Add button component to card to allow centering by clicking
                Button cardButton = cardGo.GetComponent<Button>();
                if (cardButton == null)
                {
                    cardButton = cardGo.AddComponent<Button>();
                }
                
                int index = i;
                cardButton.onClick.AddListener(() => OnCardClicked(index));
            }
        }

        UpdateCardsLayout(true);
    }

    private void SetupButtons()
    {
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(NavigateLeft);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NavigateRight);
        }

        if (jogarButton != null)
        {
            jogarButton.onClick.AddListener(PlaySelectedMap);
        }

        if (voltarButton != null)
        {
            voltarButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void Update()
    {
        HandleKeyboardGamepadInput();
        
        if (!isDragging)
        {
            // Smoothly lerp towards target center index
            currentCenterIndex = Mathf.SmoothDamp(currentCenterIndex, targetCenterIndex, ref centerIndexVelocity, smoothTime);
            
            // Clean up infinite wrap values once we are stable/close to target
            if (Mathf.Abs(currentCenterIndex - targetCenterIndex) < 0.001f)
            {
                currentCenterIndex = GetWrappedIndex(targetCenterIndex);
                targetCenterIndex = currentCenterIndex;
                centerIndexVelocity = 0f;
            }
        }

        UpdateCardsLayout(false);
    }

    private void HandleKeyboardGamepadInput()
    {
        if (inputCooldownTimer > 0f)
        {
            inputCooldownTimer -= Time.deltaTime;
            return;
        }

        bool leftPressed = false;
        bool rightPressed = false;

        // Use UnityEngine.InputSystem
        if (Keyboard.current != null)
        {
            leftPressed |= Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
            rightPressed |= Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
        }

        if (Gamepad.current != null)
        {
            leftPressed |= Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame;
            rightPressed |= Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame;
        }

        if (leftPressed)
        {
            NavigateLeft();
            inputCooldownTimer = INPUT_COOLDOWN;
        }
        else if (rightPressed)
        {
            NavigateRight();
            inputCooldownTimer = INPUT_COOLDOWN;
        }
    }

    private void NavigateLeft()
    {
        targetCenterIndex -= 1f;
    }

    private void NavigateRight()
    {
        targetCenterIndex += 1f;
    }

    private void OnCardClicked(int cardIndex)
    {
        if (isDragging) return;

        // Calculate the shortest direction in infinite scroll
        int N = spawnedCards.Count;
        float diff = cardIndex - GetWrappedIndex(targetCenterIndex);
        diff = Mathf.Repeat(diff + N / 2f, N) - N / 2f;

        targetCenterIndex += diff;
    }

    private void UpdateCardsLayout(bool forceImmediate)
    {
        int N = spawnedCards.Count;
        if (N == 0) return;

        for (int i = 0; i < N; i++)
        {
            MapCardUI card = spawnedCards[i];
            if (card == null) continue;

            // Compute relative offset with infinite wrap-around
            float diff = i - currentCenterIndex;
            diff = Mathf.Repeat(diff + N / 2f, N) - N / 2f;

            // Set Position
            float posX = diff * cardSpacing;
            card.RectTransform.anchoredPosition = new Vector2(posX, 0f);

            // Set Scale (closer to 0 diff = larger scale)
            float absDiff = Mathf.Abs(diff);
            float factor = Mathf.Clamp01(1f - absDiff); // 1 at center, 0 at >=1 offset
            
            // We can shape the factor with a smoother curve
            float smoothFactor = Mathf.SmoothStep(0f, 1f, factor);

            float scale = Mathf.Lerp(minScale, maxScale, smoothFactor);
            card.RectTransform.localScale = new Vector3(scale, scale, 1f);

            // Set Opacity (closer to center = more visible/brighter)
            if (card.CanvasGroup != null)
            {
                card.CanvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, smoothFactor);
            }

            // Set sibling index to draw centered elements on top of others
            // High factor elements should be drawn last (rendered in front)
            int targetSiblingIndex = Mathf.Clamp((int)(factor * N), 0, N - 1);
            card.transform.SetSiblingIndex(targetSiblingIndex);
        }

        // Update top title text of currently selected map
        int selectedIndex = Mathf.RoundToInt(GetWrappedIndex(currentCenterIndex));
        if (selectedIndex >= 0 && selectedIndex < N)
        {
            MapData currentMap = spawnedCards[selectedIndex].MapData;
            if (selectedMapNameText != null && currentMap != null)
            {
                selectedMapNameText.text = currentMap.mapName;
            }

            // Manage Jogar button interactability based on lock status
            if (jogarButton != null && currentMap != null)
            {
                jogarButton.interactable = currentMap.IsUnlocked();
            }
        }
    }

    private float GetWrappedIndex(float index)
    {
        int N = spawnedCards.Count;
        if (N == 0) return 0f;
        return Mathf.Repeat(index, N);
    }

    #region Drag and Swipe Support

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartCenterIndex = currentCenterIndex;
        dragStartMouseX = eventData.position.x;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        float deltaX = eventData.position.x - dragStartMouseX;
        // Convert screen delta X to map index units
        // Spacing is in canvas units, so we scale by reference resolution or canvas scale
        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        if (scaleFactor == 0f) scaleFactor = 1f;

        float indexDelta = -deltaX / (cardSpacing * scaleFactor);
        currentCenterIndex = dragStartCenterIndex + indexDelta;
        targetCenterIndex = currentCenterIndex;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        
        // Snap to nearest whole index
        targetCenterIndex = Mathf.Round(currentCenterIndex);
    }

    #endregion

    #region Navigation Operations

    public void PlaySelectedMap()
    {
        int selectedIndex = Mathf.RoundToInt(GetWrappedIndex(targetCenterIndex));
        if (selectedIndex >= 0 && selectedIndex < spawnedCards.Count)
        {
            MapData selectedMap = spawnedCards[selectedIndex].MapData;
            if (selectedMap != null)
            {
                if (!selectedMap.isUnlocked)
                {
                    Debug.LogWarning("MapCarouselController: Map is locked!");
                    return;
                }

                Debug.Log($"MapCarouselController: Starting map '{selectedMap.mapName}' via SceneLoader...");
                if (selectedMap.scenesToLoad != null && selectedMap.scenesToLoad.Length > 0)
                {
                    SceneLoader.LoadScenes(selectedMap.scenesToLoad, selectedMap.activeSceneName);
                }
                else
                {
                    Debug.LogError("MapCarouselController: Selected map has no configured scenes!");
                }
            }
        }
    }

    public void ReturnToMainMenu()
    {
        if (selectMapArea != null) selectMapArea.SetActive(false);
        if (mainMenuArea != null) mainMenuArea.SetActive(true);
    }

    #endregion
}
