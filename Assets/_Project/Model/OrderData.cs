using System.Collections.Generic;

namespace TileMatch.Model
{
    /// <summary>
    /// Pure data struct representing a single customer order.
    /// Tracks the sequential requirements and current fulfillment progress.
    /// </summary>
    [System.Serializable]
    public class OrderData
    {
        public List<int> requiredTypeIDs = new List<int>();
        public int CurrentItemIndex { get; internal set; }
    }
}
