namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.GameplayController"/> (at start) and <see cref="Controller.OrderController"/> 
    /// when a new order is pulled from the pending queue into an active tray.
    /// Consumed by <see cref="View.VisualView"/> to instantiate the tray visuals.
    /// </summary>
    public struct OrderPromotedSignal
    {
        public Model.OrderData Order;
        public int TrayIndex;
    }
}
