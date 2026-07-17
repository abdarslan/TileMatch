namespace TileMatch.Signal.UI
{
    /// <summary>
    /// Fired by <see cref="GameplayHUDView"/> when the user clicks the Restart button.
    /// Consumed by <see cref="Controller.GameplayController"/> to reload the current level.
    /// </summary>
    public struct RestartLevelRequestSignal { }
}
