using TileMatch.Model;

namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.OrderController"/> when a tapped tile matches an active order.
    /// Consumed by <see cref="View.VisualView"/> to animate the tile flying from the board to the order tray.
    /// </summary>
    public struct TileRoutedToTraySignal
    {
        public TileData Tile;
        public int TargetTrayIndex;
        public int TargetItemIndex;
    }
}
