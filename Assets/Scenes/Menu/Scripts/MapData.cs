using UnityEngine;

[CreateAssetMenu(fileName = "NewMapData", menuName = "Farm Racing/Map Data")]
public class MapData : ScriptableObject
{
    // Static reference to track which map is currently being played
    public static MapData CurrentMap { get; set; }

    [Header("Map Details")]
    public string mapName;
    public Sprite thumbnail;
    
    [Header("Scene Loading")]
    public string[] scenesToLoad = new string[] { "Fazenda Veloz", "Fase01" };
    public string activeSceneName = "Fazenda Veloz";
    
    [Header("Progression")]
    public bool isUnlocked = true; // This is the default/editor state
    [Range(0, 3)]
    public int starsCount = 0; // Default stars

    [Tooltip("The map that will be unlocked when the player wins (top 3) this map.")]
    public MapData nextMapToUnlock;

    public bool IsUnlocked()
    {
        if (isUnlocked) return true;
        return PlayerPrefs.GetInt("Map_Unlock_" + mapName, 0) == 1;
    }

    public void Unlock()
    {
        PlayerPrefs.SetInt("Map_Unlock_" + mapName, 1);
        PlayerPrefs.Save();
    }

    public int GetStarsCount()
    {
        return PlayerPrefs.GetInt("Map_Stars_" + mapName, starsCount);
    }

    public void SetStarsCount(int stars)
    {
        int currentMax = GetStarsCount();
        if (stars > currentMax)
        {
            PlayerPrefs.SetInt("Map_Stars_" + mapName, stars);
            PlayerPrefs.Save();
        }
    }
}
