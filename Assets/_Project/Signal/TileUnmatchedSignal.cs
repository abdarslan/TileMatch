using TileMatch.Model;

namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.OrderController"/> when a tapped tile does not match any active order requirements.
    /// Consumed by <see cref="Controller.RackController"/> to attempt to allocate the tile into the rack.
    /// </summary>
    public struct TileUnmatchedSignal
    {
        public TileData Tile;
    }
}
