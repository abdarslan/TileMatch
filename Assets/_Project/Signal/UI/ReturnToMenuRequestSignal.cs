namespace TileMatch.Signal.UI
{
    /// <summary>
    /// Fired by <see cref="ResultScreenView"/> when the user clicks 'Continue' or 'Return to Menu'.
    /// Consumed by <see cref="Controller.GameplayController"/> to transition back to the main menu.
    /// </summary>
    public struct ReturnToMenuRequestSignal { }
}
