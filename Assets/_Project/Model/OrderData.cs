using System.Collections.Generic;

namespace TileMatch.Model
{
    [System.Serializable]
    public class OrderData
    {
        public List<int> requiredTypeIDs = new List<int>();
        public int CurrentItemIndex { get; internal set; }
    }
}
