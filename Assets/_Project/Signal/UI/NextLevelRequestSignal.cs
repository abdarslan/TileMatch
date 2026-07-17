namespace TileMatch.Signal.UI
{
    /// <summary>
    /// Fired by <see cref="ResultScreenView"/> when the user clicks 'Continue' after winning.
    /// Consumed by <see cref="Controller.GameplayController"/> to load the next level in the sequence.
    /// </summary>
    public struct NextLevelRequestSignal { }
}
