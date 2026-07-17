using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.Model
{
    /// <summary>
    /// Pure data struct representing a single tile on the board.
    /// Used both for Editor serialization and runtime state tracking.
    /// </summary>
    [System.Serializable]
    public struct TileData
    {
        public int tileID;
        public int typeID;
        public Vector3 visualPosition;
        public List<int> blockingTileIDs;
    }
}
