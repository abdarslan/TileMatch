namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.OrderController"/> when an active order successfully claims a tile that was sitting in the rack.
    /// Consumed by <see cref="View.VisualView"/> to animate a ghost tile from the rack to the tray.
    /// </summary>
    public struct TileRoutedFromRackToTraySignal
    {
        public int SourceRackIndex;
        public int TargetTrayIndex;
        public int TargetItemIndex;
        public int TypeID;
    }
}
