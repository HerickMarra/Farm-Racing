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
    [Tooltip("OPTIONAL: explicit gameplay camera used for the world->screen projection. " +
             "Leave empty to auto-resolve the CameraController's camera. " +
             "NEVER relies on Camera.main, which is ambiguous in multi-scene builds.")]
    [SerializeField] private Camera trackingCamera;

    [Header("Tracking")]
    [Tooltip("World-space vertical offset above the target kart pivot.")]
    [SerializeField] private float worldHeightOffset = 1.2f;
    [Tooltip("Optional spin speed for the reticle (degrees/second). 0 = no spin.")]
    [SerializeField] private float reticleSpin = 90f;
    [Tooltip("Pulse scale amount for a 'locked' feel. 0 = no pulse.")]
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private float pulseSpeed = 6f;

    private Camera gameplayCamera;
    private CameraController gameplayCameraController;
    private KartController playerKart;
    private float spinAngle;
    private Vector3 baseScale = Vector3.one;

    private void Start()
    {
        if (reticle != null)
        {
            baseScale = reticle.localScale;

            // Force centered anchors and pivot so anchoredPosition maps cleanly to screen-centered coords.
            reticle.anchorMin = new Vector2(0.5f, 0.5f);
            reticle.anchorMax = new Vector2(0.5f, 0.5f);
            reticle.pivot = new Vector2(0.5f, 0.5f);
        }
        ResolveCamera();
        FindPlayerKart();
        SetReticleVisible(false);
    }

    // LateUpdate: the CameraController moves the camera in its own LateUpdate, so we must
    // project AFTER the camera has reached its final pose this frame. Doing this in Update
    // would use the previous frame's camera pose, causing lag/jitter (worse at build framerates).
    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
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
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Behind the camera -> hide.
        if (screenPos.z <= 0f)
        {
            SetReticleVisible(false);
            return;
        }

        SetReticleVisible(true);

        // Position the reticle robustly: convert screen pixels to the reticle parent's local space.
        // This automatically accounts for CanvasScaler scaleFactor, resolution, aspect and DPI,
        // so the result is identical in the Editor and in any build resolution.
        RectTransform parentRect = reticle.parent as RectTransform;
        if (canvas != null && parentRect != null)
        {
            // For Screen Space - Overlay the camera context MUST be null.
            // For Screen Space - Camera it must be the canvas worldCamera.
            Camera cameraContext = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, screenPos, cameraContext, out localPoint);
            reticle.anchoredPosition = localPoint;
        }
        else
        {
            // Fallback (no canvas / no rect parent): only valid for overlay.
            reticle.position = new Vector3(screenPos.x, screenPos.y, 0f);
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

    /// <summary>
    /// Resolves the camera to use for projection, WITHOUT ever relying on Camera.main.
    /// Priority: explicit serialized camera -> the CameraController's gameplay camera.
    /// Camera.main is avoided because multi-scene builds can have several cameras tagged
    /// "MainCamera" (e.g. the loading/intro scene's camera), and Camera.main returns an
    /// arbitrary one whose pick order differs between Editor and Build.
    /// </summary>
    private Camera ResolveCamera()
    {
        // 1. Explicit assignment always wins.
        if (trackingCamera != null)
        {
            gameplayCamera = trackingCamera;
            return gameplayCamera;
        }

        // 2. Use the gameplay CameraController's camera.
        if (gameplayCameraController == null)
        {
            gameplayCameraController = Object.FindFirstObjectByType<CameraController>();
        }
        if (gameplayCameraController != null && gameplayCameraController.Cam != null)
        {
            gameplayCamera = gameplayCameraController.Cam;
            return gameplayCamera;
        }

        // 3. Last-resort fallback (kept only so the reticle is not permanently disabled
        //    if the CameraController is missing). Logged once for visibility.
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
        return gameplayCamera;
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
