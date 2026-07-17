using TileMatch.Model;

namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.RackController"/> when a tapped tile matches no active orders and is assigned to the rack.
    /// Consumed by <see cref="View.VisualView"/> to animate the tile flying from the board to the rack.
    /// </summary>
    public struct TileRoutedToRackSignal
    {
        public TileData Tile;
        public int TargetRackIndex;
    }
}
