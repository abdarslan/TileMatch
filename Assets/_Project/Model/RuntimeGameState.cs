using System.Collections.Generic;

namespace TileMatch.Model
{
    /// <summary>
    /// Holds the fully mutable, current logical state of the gameplay board.
    /// Deep-copied from <see cref="LevelData"/> at level start. Modifying this
    /// object drives all controller logic without dirtying the underlying ScriptableObject.
    /// </summary>
    public class RuntimeGameState
    {
        public const int RackCapacity = 6;
        public const int MaxActiveOrders = 2;

        public int[] Rack { get; } = new int[RackCapacity];
        public OrderData[] ActiveOrders { get; } = new OrderData[MaxActiveOrders];
        public Queue<OrderData> PendingOrders { get; } = new Queue<OrderData>();
        public Dictionary<int, TileData> RuntimeTiles { get; } = new Dictionary<int, TileData>();
    }
}
