using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.Model
{
    [CreateAssetMenu(fileName = "LevelData", menuName = "TileMatch/Level Data")]
    public class LevelData : ScriptableObject
    {
        public List<TileSaveData> activeTiles = new List<TileSaveData>();
        public List<OrderData> pendingOrders = new List<OrderData>();
    }
}
