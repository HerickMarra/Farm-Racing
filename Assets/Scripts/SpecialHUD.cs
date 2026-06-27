using UnityEngine;
using UnityEngine.UI;

public class SpecialHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UnityEngine.UI.Image specialIconImage;
    [SerializeField] private GameObject hudContainer; // To hide/show the entire item container/border
    [SerializeField] private Sprite defaultPlaceholderIcon; // Default sprite if item has no icon

    private KartController playerKart;

    private void Start()
    {
        FindPlayerKart();
        
        // Hide on start
        if (hudContainer != null)
        {
            hudContainer.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerKart == null)
        {
            FindPlayerKart();
            if (playerKart == null) return;
        }

        bool hasSpecial = playerKart.hasSpecial;

        if (hudContainer != null)
        {
            if (hudContainer.activeSelf != hasSpecial)
            {
                hudContainer.SetActive(hasSpecial);
            }
        }

        if (hasSpecial && specialIconImage != null)
        {
            if (playerKart.currentSpecial != null && playerKart.currentSpecial.hudIcon != null)
            {
                if (specialIconImage.sprite != playerKart.currentSpecial.hudIcon)
                {
                    specialIconImage.sprite = playerKart.currentSpecial.hudIcon;
                }
            }
            else
            {
                if (specialIconImage.sprite != defaultPlaceholderIcon)
                {
                    specialIconImage.sprite = defaultPlaceholderIcon;
                }
            }
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
