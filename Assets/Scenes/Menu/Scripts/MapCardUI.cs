using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapCardUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UnityEngine.UI.Image borderImage;
    [SerializeField] private UnityEngine.UI.Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject[] stars; // Expected length 3 for 0-3 stars

    private MapData mapData;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            return rectTransform;
        }
    }

    public CanvasGroup CanvasGroup
    {
        get
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            return canvasGroup;
        }
    }

    public MapData MapData => mapData;

    public void Setup(MapData data)
    {
        mapData = data;
        
        if (nameText != null)
        {
            nameText.text = data.mapName;
        }

        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = data.thumbnail;
            // Hide thumbnail if no sprite is assigned
            thumbnailImage.gameObject.SetActive(data.thumbnail != null);
        }

        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!data.IsUnlocked());
        }

        if (stars != null)
        {
            int currentStars = data.GetStarsCount();
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    stars[i].SetActive(i < currentStars);
                }
            }
        }
    }
}
