using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.Model
{
    [System.Serializable]
    public class TileData
    {
        public int tileID;
        public int typeID;
        public Vector3 visualPosition;
        public List<int> blockingTileIDs = new List<int>();
    }
}
