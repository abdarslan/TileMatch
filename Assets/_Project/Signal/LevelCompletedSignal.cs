namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.OrderController"/> when the final pending order is fulfilled.
    /// Consumed by <see cref="Controller.GameplayController"/> (transitions to Won state) 
    /// and <see cref="View.VisualView"/> (triggers win animations).
    /// </summary>
    public struct LevelCompletedSignal { }
}
