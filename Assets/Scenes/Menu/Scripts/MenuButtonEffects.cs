using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Hover Effect")]
    [Tooltip("The multiplier applied to the scale on hover.")]
    [SerializeField] private float hoverScaleMultiplier = 1.15f;
    [Tooltip("Duration of the transition animation in seconds.")]
    [SerializeField] private float transitionDuration = 0.15f;

    [Header("Cursor Settings")]
    [Tooltip("Custom pointing hand cursor texture.")]
    [SerializeField] private Texture2D cursorTexture;
    [Tooltip("Hotspot for the custom cursor (usually where the finger tip is, e.g. top-left).")]
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;

    private Vector3 initialScale;
    private Coroutine scaleCoroutine;
    private bool isHovered = false;

    private void Awake()
    {
        initialScale = transform.localScale;
    }

    private void OnDisable()
    {
        // Reset scale and cursor immediately when disabled
        transform.localScale = initialScale;
        if (isHovered)
        {
            ResetCursor();
            isHovered = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        StartScaleAnimation(initialScale * hoverScaleMultiplier);
        SetCustomCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        StartScaleAnimation(initialScale);
        ResetCursor();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Add a slight click indentation / shrink effect for extra polish
        StartScaleAnimation(initialScale * (hoverScaleMultiplier * 0.95f));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovered)
        {
            StartScaleAnimation(initialScale * hoverScaleMultiplier);
        }
        else
        {
            StartScaleAnimation(initialScale);
        }
    }

    private void StartScaleAnimation(Vector3 targetScale)
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaledDeltaTime so hover works if game is paused
            float t = elapsed / transitionDuration;
            // Smooth step interpolation
            t = t * t * (3f - 2f * t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
        scaleCoroutine = null;
    }

    private void SetCustomCursor()
    {
        if (cursorTexture != null)
        {
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
        }
    }

    private void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
