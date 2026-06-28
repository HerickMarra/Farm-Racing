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

        // Position the reticle based on Canvas RenderMode.
        // For ScreenSpaceOverlay, setting reticle.position directly to screenPos is 100% robust and bypasses scaling bugs in builds.
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            reticle.position = screenPos;
        }
        else if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvasRect != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvas.worldCamera, out localPoint);
            reticle.anchoredPosition = localPoint;
        }
        else
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
