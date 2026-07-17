using TileMatch.Model;

namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.LevelController"/> after successfully verifying that a tapped tile is unblocked and exists.
    /// Consumed by <see cref="Controller.OrderController"/> to evaluate it against active orders.
    /// </summary>
    public struct TileValidatedSignal
    {
        public TileData Tile;
    }
}
