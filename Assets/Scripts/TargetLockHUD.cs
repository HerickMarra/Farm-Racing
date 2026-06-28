using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space reticle that appears over the enemy kart locked by the player's
/// KartTargetingSystem (missile lock indicator). Fully decoupled: it only reads
/// the current target and projects its world position to the HUD canvas.
/// The reticle is a simple placeholder Image for now and can later be swapped
/// for an animated lock-on effect.
/// </summary>
public class TargetLockHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RectTransform of the reticle image that follows the target on screen.")]
    [SerializeField] private RectTransform reticle;
    [Tooltip("Canvas that hosts the reticle (used for the RenderMode / scaling).")]
    [SerializeField] private Canvas canvas;

    [Header("Tracking")]
    [Tooltip("World-space vertical offset above the target kart pivot.")]
    [SerializeField] private float worldHeightOffset = 1.2f;
    [Tooltip("Optional spin speed for the reticle (degrees/second). 0 = no spin.")]
    [SerializeField] private float reticleSpin = 90f;
    [Tooltip("Pulse scale amount for a 'locked' feel. 0 = no pulse.")]
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private float pulseSpeed = 6f;

    private Camera mainCamera;
    private KartController playerKart;
    private RectTransform canvasRect;
    private float spinAngle;
    private Vector3 baseScale = Vector3.one;

    private void Start()
    {
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }
        if (reticle != null)
        {
            baseScale = reticle.localScale;
            
            // Force centered anchors and pivot to guarantee perfect mathematical scaling at any resolution
            reticle.anchorMin = new Vector2(0.5f, 0.5f);
            reticle.anchorMax = new Vector2(0.5f, 0.5f);
            reticle.pivot = new Vector2(0.5f, 0.5f);
        }
        FindPlayerKart();
        SetReticleVisible(false);
    }

    private void Update()
    {
        // Always query Camera.main dynamically to avoid stale cached cameras (e.g. from load/loading scenes)
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            SetReticleVisible(false);
            return;
        }

        if (playerKart == null)
        {
            FindPlayerKart();
            if (playerKart == null) { SetReticleVisible(false); return; }
        }

        KartTargetingSystem targeting = playerKart.TargetingSystem;
        KartController target = targeting != null ? targeting.CurrentTarget : null;

        // Hide if no lock, no special, or the target was destroyed.
        if (target == null || !playerKart.hasSpecial || reticle == null)
        {
            SetReticleVisible(false);
            return;
        }

        Vector3 worldPos = target.transform.position + Vector3.up * worldHeightOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // Behind the camera -> hide.
        if (screenPos.z <= 0f)
        {
            SetReticleVisible(false);
            return;
        }

        SetReticleVisible(true);

        // 1. If in ScreenSpaceCamera, guarantee the Canvas is bound to the active main camera in builds
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            if (canvas.worldCamera == null || canvas.worldCamera != mainCamera)
            {
                canvas.worldCamera = mainCamera;
            }
        }

        // 2. Position the reticle robustly by converting screen pixels to its direct parent's local space
        if (canvas != null && reticle != null)
        {
            RectTransform parentRect = reticle.parent as RectTransform;
            if (parentRect != null)
            {
                Vector2 localPoint;
                Camera cameraContext = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPos, cameraContext, out localPoint);

                reticle.anchoredPosition = localPoint;
            }
            else
            {
                reticle.position = screenPos;
            }
        }
        else if (reticle != null)
        {
            reticle.position = screenPos;
        }

        // Visual flair: spin + pulse to feel like an active missile lock.
        if (reticleSpin != 0f)
        {
            spinAngle += reticleSpin * Time.deltaTime;
            reticle.localRotation = Quaternion.Euler(0f, 0f, spinAngle);
        }
        if (pulseAmount > 0f)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            reticle.localScale = baseScale * pulse;
        }
    }

    private void SetReticleVisible(bool visible)
    {
        if (reticle != null && reticle.gameObject.activeSelf != visible)
        {
            reticle.gameObject.SetActive(visible);
        }
    }

    private void FindPlayerKart()
    {
        var karts = Object.FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
        foreach (var kart in karts)
        {
            if (kart.isPlayer)
            {
                playerKart = kart;
                break;
            }
        }
    }
}
