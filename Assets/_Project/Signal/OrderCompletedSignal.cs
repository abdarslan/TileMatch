namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.OrderController"/> when an active order's requirements are fully met.
    /// Consumed by <see cref="View.VisualView"/> to animate the tray slide-out sequence.
    /// </summary>
    public struct OrderCompletedSignal
    {
        public int TrayIndex;
    }
}
