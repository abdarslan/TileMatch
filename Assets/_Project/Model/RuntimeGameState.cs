using System.Collections.Generic;

namespace TileMatch.Model
{
    public class RuntimeGameState
    {
        public const int RackCapacity = 6;
        public const int MaxActiveOrders = 2;

        public int[] Rack { get; } = new int[RackCapacity];
        public List<OrderData> ActiveOrders { get; } = new List<OrderData>(MaxActiveOrders);
        public Queue<OrderData> PendingOrders { get; } = new Queue<OrderData>();
        public Dictionary<int, TileData> RuntimeTiles { get; } = new Dictionary<int, TileData>();
    }
}
