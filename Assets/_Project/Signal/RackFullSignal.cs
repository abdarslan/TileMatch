namespace TileMatch.Signal
{
    /// <summary>
    /// Fired by <see cref="Controller.RackController"/> when the final slot is filled.
    /// Consumed by <see cref="Controller.GameplayController"/> (transitions to Failed state)
    /// and <see cref="View.VisualView"/> (triggers shake/fail animation).
    /// </summary>
    public struct RackFullSignal { }
}
