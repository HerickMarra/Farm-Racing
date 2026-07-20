using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMapList", menuName = "Farm Racing/Map List")]
public class MapList : ScriptableObject
{
    public List<MapData> maps = new List<MapData>();
}
